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
            
            var payment = new
            {
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