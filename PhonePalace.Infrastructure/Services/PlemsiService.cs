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
        private readonly ILogger<PlemsiService> _logger;

        public PlemsiService(HttpClient httpClient, IOptions<PlemsiConfig> config, ILogger<PlemsiService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;

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
            // Validar que el consecutivo esté dentro del rango autorizado
            if (sale.Invoice.InvoiceID < _config.StartRange || sale.Invoice.InvoiceID > _config.EndRange)
            {
                return new PlemsiResponse
                {
                    Success = false,
                    ErrorMessage = $"El consecutivo {sale.Invoice.InvoiceID} está fuera del rango autorizado ({_config.StartRange} - {_config.EndRange}). Verifique la configuración."
                };
            }

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
                        return new PlemsiResponse
                        {
                            Success = true,
                            Cufe = result.Data?.Cufe,
                            Number = $"{result.Data?.Prefix}{result.Data?.Number}",
                            QrUrl = result.Data?.QrUrl,
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
                    
                    // Loguear el payload para depuración de errores genéricos
                    var jsonPayload = JsonSerializer.Serialize(payload);
                    _logger.LogError("Plemsi Error. Payload enviado: {Payload}", jsonPayload);
                    _logger.LogError("Plemsi API Error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                    
                    string friendlyMessage = $"Error HTTP {response.StatusCode}";
                    try
                    {
                        // Intentar parsear los diferentes formatos de error de Plemsi
                        using (JsonDocument doc = JsonDocument.Parse(errorContent))
                        {
                            var root = doc.RootElement;
                            string? detailedError = null;

                            // 1. Buscar detalles específicos en 'data'
                            if (root.TryGetProperty("data", out var data))
                            {
                                // Caso: Respuesta de la DIAN (Objeto con StatusDescription) - AQUÍ ESTÁ TU ERROR ACTUAL
                                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("StatusDescription", out var statusDesc))
                                {
                                    detailedError = statusDesc.GetString();
                                }
                                // Caso: Errores de validación (Array de strings)
                                else if (data.ValueKind == JsonValueKind.Array)
                                {
                                    detailedError = string.Join("; ", data.EnumerateArray().Select(x => x.GetString()));
                                }
                            }
                            
                            // 2. Obtener mensaje principal ('info' o 'message')
                            if (root.TryGetProperty("info", out var info))
                            {
                                friendlyMessage = info.GetString() ?? friendlyMessage;
                            }
                            else if (root.TryGetProperty("message", out var message))
                            {
                                friendlyMessage = message.GetString() ?? friendlyMessage;
                            }

                            // 3. Combinar mensaje principal con detalle
                            if (!string.IsNullOrEmpty(detailedError))
                            {
                                friendlyMessage = $"{friendlyMessage}: {detailedError}";
                            }

                            // 4. Sugerencias automáticas
                            if (friendlyMessage.Contains("EFVE001") || friendlyMessage.Contains("Software yet"))
                            {
                                friendlyMessage += ". IMPORTANTE: Debe configurar el TestSetID en el panel de Plemsi.";
                            }
                            if (friendlyMessage.Contains("resolution is not available"))
                            {
                                friendlyMessage += $". Verifique en Plemsi que exista una resolución ACTIVA con el prefijo '{_config.Prefix}'.";
                            }
                            if (friendlyMessage.Contains("no autorizado a enviar documentos"))
                            {
                                friendlyMessage += ". ERROR DE HABILITACIÓN: El emisor no ha autorizado al proveedor tecnológico (Plemsi) en el portal de la DIAN.";
                            }
                        }
                    }
                    catch { /* Si falla el parseo, devolvemos el default */ }

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

        private object BuildPayload(Sale sale)
        {
            var client = sale.Client;
            var invoice = sale.Invoice;

            // Mapeo de Cliente
            var customer = new
            {
                identification_number = GetDocumentNumber(client),
                name = client.DisplayName,
                email = client.Email ?? "consumidorfinal@correo.com", // Email obligatorio
                phone = client.PhoneNumber ?? "3000000000",
                address = client.StreetAddress ?? "Ciudad",
                municipality_id = client.MunicipalityID ?? "11001", // Default Bogotá si es nulo
                type_document_identification_id = GetDocumentTypeId(client),
                type_organization_id = client is NaturalPerson ? 2 : 1, // 1: Juridica, 2: Natural
                type_liability_id = 117, // 117: No responsable de IVA
                type_regime_id = client is NaturalPerson ? 2 : 1 // 1: Responsable de IVA, 2: No responsable
            };

            // Mapeo de Items
            var items = sale.Details.Select(d =>
            {
                // UnitPrice incluye IVA. Calculamos base.
                decimal baseUnitPrice = Math.Round(d.UnitPrice / 1.19m, 2);
                decimal lineExtensionAmount = Math.Round(baseUnitPrice * d.Quantity, 2);
                decimal taxAmount = Math.Round(lineExtensionAmount * 0.19m, 2);

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
                    base_quantity = 1,
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
            decimal totalTax = items.Sum(i => i.tax_totals[0].tax_amount);
            decimal invoiceTaxInclusiveTotal = invoiceBaseTotal + totalTax;

            // Mapeo de Pago (Payment)
            var firstPayment = invoice.Payments.FirstOrDefault();
            var paymentMethodId = firstPayment != null ? GetPaymentMethodId(firstPayment.PaymentMethod) : 10;
            
            // Determinar Forma de Pago (1: Contado, 2: Crédito)
            // Si el método es Crédito (30), la forma es Crédito (2). De lo contrario es Contado (1).
            int paymentFormId = (paymentMethodId == 30) ? 2 : 1;

            var payment = new
            {
                payment_form_id = paymentFormId,
                payment_method_id = paymentMethodId,
                payment_due_date = sale.SaleDate.ToString("yyyy-MM-dd"),
                duration_measure = 0
            };

            return new
            {
                number = invoice.InvoiceID,
                date = sale.SaleDate.ToString("yyyy-MM-dd"),
                time = sale.SaleDate.ToString("HH:mm:ss"),
                prefix = _config.Prefix,
                resolution = _config.ResolutionNumber,
                customer = customer,
                items = items,
                payment = payment,
                invoiceBaseTotal = invoiceBaseTotal,
                invoiceTaxExclusiveTotal = invoiceBaseTotal,
                invoiceTaxInclusiveTotal = invoiceTaxInclusiveTotal,
                totalToPay = invoiceTaxInclusiveTotal,
                notes = $"Venta #{invoice.InvoiceID} - PhonePalace"
            };
        }

        private string GetDocumentNumber(Client client)
        {
            if (client is NaturalPerson np) return np.DocumentNumber;
            if (client is LegalEntity le) return le.NIT.Split('-')[0]; // Enviar sin dígito de verificación usualmente
            return "222222222222"; // Consumidor final
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
            public string Prefix { get; set; }
            public string Number { get; set; }
            public string QrUrl { get; set; }
        }
    }
}