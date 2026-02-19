using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Configuration;

namespace PhonePalace.Infrastructure.Services
{
    public class PlemsiService : IPlemsiService
    {
        private readonly HttpClient _httpClient;
        private readonly PlemsiConfig _config;
        private readonly CompanySettings _companySettings;
        private readonly ILogger<PlemsiService> _logger;

        public PlemsiService(HttpClient httpClient, 
                             IOptions<PlemsiConfig> config, 
                             IOptions<CompanySettings> companySettings, 
                             ILogger<PlemsiService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            _companySettings = companySettings.Value;

            // Configuración básica del cliente si no viene inyectado configurado
            if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_config.BaseUrl))
            {
                var url = _config.BaseUrl;
                if (!url.EndsWith("/")) url += "/";
                _httpClient.BaseAddress = new Uri(url);
            }
            
            if (!_httpClient.DefaultRequestHeaders.Contains("Authorization") && !string.IsNullOrEmpty(_config.Token))
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.Token}");
        }

        public async Task<PlemsiResponse> SendInvoiceAsync(Sale sale)
        {
            try
            {
                var payload = BuildPayload(sale);
                
                // Enviar petición a Plemsi (Endpoint 'billing/invoice')
                var response = await _httpClient.PostAsJsonAsync("billing/invoice", payload);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PlemsiApiResponse>();
                    
                    if (result != null && result.Success)
                    {
                        // Validar que la respuesta contenga los datos necesarios (CUFE o CUDE)
                        var uniqueId = !string.IsNullOrEmpty(result.Data?.Cufe) ? result.Data.Cufe : result.Data?.Cude;

                        if (result.Data == null || string.IsNullOrEmpty(uniqueId))
                        {
                            return new PlemsiResponse
                            {
                                Success = false,
                                ErrorMessage = "La factura fue procesada, pero la respuesta no contiene el CUFE."
                            };
                        }

                        var qrUrl = !string.IsNullOrEmpty(result.Data?.QrUrl) ? result.Data.QrUrl : result.Data?.QRCode;

                        return new PlemsiResponse
                        {
                            Success = true,
                            Cufe = uniqueId,
                            Number = $"{result.Data?.Prefix}{result.Data?.Number}",
                            QrUrl = qrUrl ?? "", // Usar el valor encontrado o cadena vacía para evitar error de DB
                            Status = "Accepted"
                        };
                    }
                    else
                    {
                         return new PlemsiResponse
                        {
                            Success = false,
                            ErrorMessage = result?.Message ?? "Error desconocido en la respuesta de Plemsi."
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Plemsi API Error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    
                    string friendlyMessage = $"Error HTTP {response.StatusCode}";
                    try
                    {
                        // Intentar parsear los diferentes formatos de error de Plemsi
                        using (JsonDocument doc = JsonDocument.Parse(errorContent))
                        {
                            var root = doc.RootElement;
                            
                            // Caso 1: Error de negocio (ej. EFVE001)
                            if (root.TryGetProperty("info", out var info))
                            {
                                friendlyMessage = info.GetString() ?? friendlyMessage;
                            }
                            // Caso 2: Error de validación (Array de errores en 'data')
                            else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                            {
                                friendlyMessage = string.Join("; ", data.EnumerateArray().Select(x => x.GetString()));
                            }
                            // Caso 3: Error genérico
                            else if (root.TryGetProperty("message", out var message))
                            {
                                friendlyMessage = message.GetString() ?? friendlyMessage;
                            }
                        }
                    }
                    catch { /* Si falla el parseo, devolvemos el default */ }

                    // --- INICIO: Recuperación automática si ya existe ---
                    // Si Plemsi dice que ya existe, intentamos consultarla para obtener el CUFE
                    if (friendlyMessage.Contains("already emitted", StringComparison.OrdinalIgnoreCase) || 
                        friendlyMessage.Contains("ya fue emitida", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("La factura {InvoiceId} ya existe en Plemsi. Intentando recuperar datos...", sale.Invoice.InvoiceID);
                        var recovery = await GetInvoiceStatusAsync(sale.Invoice.InvoiceID);
                        if (recovery.Success)
                        {
                            return recovery;
                        }
                    }
                    // --- FIN: Recuperación automática ---

                    return new PlemsiResponse
                    {
                        Success = false,
                        ErrorMessage = friendlyMessage
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando factura a Plemsi");
                return new PlemsiResponse
                {
                    Success = false,
                    ErrorMessage = $"Excepción: {ex.Message}"
                };
            }
        }

        public async Task<PlemsiResponse> GetInvoiceStatusAsync(int invoiceId)
        {
            try
            {
                // Consultar la factura por número completo (Prefijo + Número)
                var fullNumber = $"{_companySettings.DianResolutionPrefix}{invoiceId}";
                var response = await _httpClient.GetAsync($"billing/invoice/one?by=number&value={fullNumber}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PlemsiApiResponse>();

                    var uniqueId = result?.Data != null ? (!string.IsNullOrEmpty(result.Data.Cufe) ? result.Data.Cufe : result.Data.Cude) : null;

                    if (result != null && result.Success && result.Data != null && !string.IsNullOrEmpty(uniqueId))
                    {
                        var qrUrl = !string.IsNullOrEmpty(result.Data.QrUrl) ? result.Data.QrUrl : result.Data.QRCode;

                        return new PlemsiResponse
                        {
                            Success = true,
                            Cufe = uniqueId,
                            Number = $"{result.Data.Prefix}{result.Data.Number}",
                            QrUrl = qrUrl ?? "", // Usar el valor encontrado o cadena vacía
                            Status = result.Data.Status ?? "Accepted" // Usar el estado de la API, o "Accepted" por defecto
                        };
                    }
                    else
                    {
                        return new PlemsiResponse { Success = false, ErrorMessage = result?.Message ?? "No se encontró la factura en el proveedor." };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error consultando estado de factura {InvoiceId}: {StatusCode} - {Content}", invoiceId, response.StatusCode, errorContent);
                    return new PlemsiResponse { Success = false, ErrorMessage = $"Error al consultar: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error intentando recuperar factura {InvoiceId}", invoiceId);
            }
            return new PlemsiResponse { Success = false, ErrorMessage = "Excepción al consultar estado." };
        }

       private object BuildPayload(Sale sale)
        {
            var client = sale.Client;
            var invoice = sale.Invoice;

            // Mapeo de Cliente (con dígito verificador)
            string nit;
            string dv = null;
            if (client is LegalEntity le && !string.IsNullOrEmpty(le.NIT))
            {
                var nitParts = le.NIT.Split('-');
                nit = nitParts[0];
                if (nitParts.Length > 1)
                {
                    dv = nitParts[1];
                }
            }
            else if (client is NaturalPerson np)
            {
                nit = np.DocumentNumber;
            }
            else
            {
                nit = "222222222222"; // Consumidor final
            }

            var customer = new
            {
                identification_number = nit,
                dv, // Dígito verificador
                name = client.DisplayName,
                email = string.IsNullOrWhiteSpace(client.Email) ? "consumidorfinal@correo.com" : client.Email,
                phone = client.PhoneNumber ?? "3000000000",
                address = client.StreetAddress ?? "Ciudad",
                // Priorizamos el código DANE del municipio si existe.
                municipality_code = client.MunicipalityID ?? "11001", // El ID de municipio en la BD es el código DANE.
                merchant_registration = "00000000", // Valor por defecto como en el ejemplo
                type_document_identification_id = GetDocumentTypeId(client),
                type_organization_id = client is NaturalPerson ? 2 : 1, // 1: Juridica, 2: Natural
                type_liability_id = 117, // 117: No responsable de IVA
                type_regime_id = client is NaturalPerson ? 2 : 1 // 1: Responsable de IVA, 2: No responsable
            };

            // Mapeo de Items
            var items = sale.Details.Select(d =>
            {
                // UnitPrice incluye IVA. Calculamos base.
                decimal taxRateFactor = 1.19m;
                decimal baseUnitPrice = Math.Round(d.UnitPrice / taxRateFactor, 2);
                decimal lineExtensionAmount = Math.Round(baseUnitPrice * d.Quantity, 2);
                decimal taxAmount = Math.Round(lineExtensionAmount * (taxRateFactor - 1), 2);

                return new
                {
                    unit_measure_id = 70, // Unidad
                    invoiced_quantity = d.Quantity,
                    line_extension_amount = lineExtensionAmount,
                    free_of_charge_indicator = false,
                    description = d.Product.Name,
                    code = d.Product.Code ?? d.Product.SKU ?? d.ProductID.ToString(),
                    type_item_identification_id = 4, // Estándar
                    price_amount = baseUnitPrice,
                    base_quantity = d.Quantity, // Corregido: debe ser igual a la cantidad facturada
                    tax_totals = new[]
                    {
                        new
                        {
                            tax_id = 1, // IVA
                            percent = 19.00,
                            tax_amount = taxAmount, 
                            taxable_amount = lineExtensionAmount
                        }
                    }
                };
            }).ToList();

            // Totales
            decimal invoiceBaseTotal = items.Sum(i => i.line_extension_amount);
            decimal totalTax = items.Sum(i => i.tax_totals.Sum(t => t.tax_amount));
            decimal invoiceTaxInclusiveTotal = invoiceBaseTotal + totalTax;

            // Mapeo de Pago (Payment)
            var validPayments = invoice.Payments?.Where(p => p.PaymentMethod != PaymentMethod.Credit).ToList();
            decimal totalPaid = validPayments?.Sum(p => p.Amount) ?? 0;
            bool isCredit = (invoice.Total - totalPaid) > 0.01m;
            
            // Seleccionar el método de pago predominante (el de mayor valor)
            // Si hay múltiples medios, la norma suele indicar reportar el de mayor cuantía.
            var predominantPayment = validPayments?.OrderByDescending(p => p.Amount).FirstOrDefault();
            int paymentMethodId = predominantPayment != null ? GetPaymentMethodId(predominantPayment.PaymentMethod) : (isCredit ? 30 : 10);

            var payment = new
            {
                payment_form_id = isCredit ? 2 : 1, // 1 -> Contado, 2 -> Crédito
                payment_method_id = paymentMethodId,
                payment_due_date = isCredit ? DateTime.Now.AddDays(30).ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd"),
                duration_measure = isCredit ? 30 : 0
            };

            // Validar que la configuración de la resolución exista en CompanySettings
            if (string.IsNullOrEmpty(_companySettings.DianResolutionPrefix) || string.IsNullOrEmpty(_companySettings.DianResolutionNumber))
            {
                _logger.LogError("Error de configuración: Los datos de la resolución DIAN (Prefijo y Número) no están configurados en la sección CompanySettings de appsettings.json.");
                throw new InvalidOperationException("Los datos de la resolución DIAN (Prefijo y Número) no están configurados en CompanySettings.");
            }

            // Agrupar todos los impuestos para el total
            var allTaxTotals = items
                .SelectMany(i => i.tax_totals)
                .GroupBy(t => t.tax_id)
                .Select(g => new
                {
                    tax_id = g.Key,
                    tax_amount = g.Sum(t => t.tax_amount),
                    percent = g.First().percent,
                    taxable_amount = g.Sum(t => t.taxable_amount)
                }).ToList();

            return new
            {
                number = invoice.InvoiceID,
                date = DateTime.Now.ToString("yyyy-MM-dd"), // Fecha de emisión (Hoy) para cumplir FAD09e
                time = DateTime.Now.ToString("HH:mm:ss"),   // Hora de emisión (Ahora)
                prefix = _companySettings.DianResolutionPrefix,
                resolution = _companySettings.DianResolutionNumber, // Corregido: 'resolution' en lugar de 'resolution_number'
                customer,
                items,
                payment,
                // Totales a nivel de factura
                invoiceBaseTotal, // Valor bruto antes de impuestos
                invoiceTaxExclusiveTotal = invoiceBaseTotal, // Valor base para impuestos (sin descuentos, es igual al base)
                invoiceTaxInclusiveTotal, // Valor bruto + impuestos
                totalToPay = invoiceTaxInclusiveTotal, // Valor final a pagar (sin retenciones, es igual al inclusivo)
                allTaxTotals, // Resumen de impuestos
                notes = $"Venta #{invoice.InvoiceID} - PhonePalace",
            };
        }

        private int GetDocumentTypeId(Client client)
        {
            if (client is NaturalPerson np)
            {
                return np.DocumentType switch
                {
                    DocumentType.CitizenshipCard => 3, // 3: Cédula de ciudadanía
                    DocumentType.ForeignerId => 5, // 5: Cédula de extranjería
                    DocumentType.Passport => 7, // 7: Pasaporte
                    _ => 3
                };
            }
            if (client is LegalEntity) return 6; // 6: NIT (o 31 según versión)
            return 1; 
        }

        private int GetPaymentMethodId(PaymentMethod method)
        {
            return method switch
            {
                PaymentMethod.Cash => 10,       // Efectivo
                PaymentMethod.CreditCard => 48, // Tarjeta Crédito
                PaymentMethod.DebitCard => 49,  // Tarjeta Débito
                PaymentMethod.Transfer => 47,   // Transferencia
                PaymentMethod.Credit => 30,     // Crédito
                _ => 10
            };
        }

        // Clases internas para deserializar respuesta de Plemsi
        private class PlemsiApiResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public PlemsiApiData Data { get; set; }
        }

        private class PlemsiApiData
        {
            public string Cufe { get; set; }
            public string Cude { get; set; }
            public string Prefix { get; set; }
            public string Number { get; set; }
            public string QrUrl { get; set; }
            public string QRCode { get; set; }
            public string? Status { get; set; }
        }
    }
}