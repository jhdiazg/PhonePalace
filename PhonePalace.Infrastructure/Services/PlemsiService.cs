using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public PlemsiService(HttpClient httpClient, 
                             IOptions<PlemsiConfig> config, 
                             IOptions<CompanySettings> companySettings, 
                             ILogger<PlemsiService> logger,
                             IConfiguration configuration)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            _companySettings = companySettings.Value;
            _configuration = configuration;

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
                
                // Serializar el payload a JSON para guardarlo en los logs (Depuración)
                string jsonDebug = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("Enviando Factura Electrónica #{InvoiceId}. JSON Payload:\n{JsonPayload}", sale.Invoice.InvoiceID, jsonDebug);
                
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

                        // Preferir el contenido del QR (URL DIAN) sobre la URL de la imagen
                        var qrContent = result.Data?.QRCode;
                        var qrImg = result.Data?.QrUrl;
                        var qrUrl = (!string.IsNullOrEmpty(qrContent) && qrContent.Contains("dian.gov.co")) ? qrContent : (qrImg ?? qrContent);

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

        public async Task<PlemsiResponse> SendCreditNoteAsync(Sale sale, string reason, string originalCufe)
        {
            try
            {
                var payload = BuildCreditNotePayload(sale, reason, originalCufe);
                
                string jsonDebug = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("Enviando Nota Crédito para Factura #{InvoiceId}. JSON Payload:\n{JsonPayload}", sale.Invoice.InvoiceID, jsonDebug);
                
                // Endpoint estándar para Notas Crédito en Plemsi según documentación
                var response = await _httpClient.PostAsJsonAsync("billing/credit", payload);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PlemsiApiResponse>();
                    
                    if (result != null && result.Success)
                    {
                        // Las notas crédito generan CUDE, no CUFE
                        var uniqueId = !string.IsNullOrEmpty(result.Data?.Cude) ? result.Data.Cude : result.Data?.Cufe;

                        return new PlemsiResponse
                        {
                            Success = true,
                            Cufe = uniqueId,
                            Number = $"{result.Data?.Prefix}{result.Data?.Number}",
                            QrUrl = result.Data?.QrUrl ?? "",
                            Status = "Accepted"
                        };
                    }
                    else
                    {
                         return new PlemsiResponse
                        {
                            Success = false,
                            ErrorMessage = result?.Message ?? "Error desconocido al emitir Nota Crédito."
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Plemsi API Error (Credit Note): {StatusCode} - {Content}", response.StatusCode, errorContent);

                    string friendlyMessage = $"Error HTTP {response.StatusCode}";
                    try
                    {
                        // Intentar parsear el error de Plemsi
                        using (JsonDocument doc = JsonDocument.Parse(errorContent))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("info", out var info)) friendlyMessage = info.GetString() ?? friendlyMessage;
                            else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String) friendlyMessage = data.GetString() ?? friendlyMessage;
                            else if (root.TryGetProperty("message", out var message)) friendlyMessage = message.GetString() ?? friendlyMessage;
                        }
                    }
                    catch { /* Si falla el parseo, devolvemos el default */ }

                    return new PlemsiResponse { Success = false, ErrorMessage = friendlyMessage };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando Nota Crédito a Plemsi");
                return new PlemsiResponse { Success = false, ErrorMessage = $"Excepción: {ex.Message}" };
            }
        }

        public async Task<PlemsiResponse> SendPartialCreditNoteAsync(Return returnEntity, Sale sale, string reason, string originalCufe)
        {
            try
            {
                var payload = BuildPartialCreditNotePayload(returnEntity, sale, reason, originalCufe);
                
                string jsonDebug = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                _logger.LogInformation("Enviando Nota Crédito Parcial para Factura #{InvoiceId}. JSON Payload:\n{JsonPayload}", sale.Invoice.InvoiceID, jsonDebug);
                
                var response = await _httpClient.PostAsJsonAsync("billing/credit", payload);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PlemsiApiResponse>();
                    
                    if (result != null && result.Success)
                    {
                        var uniqueId = !string.IsNullOrEmpty(result.Data?.Cude) ? result.Data.Cude : result.Data?.Cufe;

                        return new PlemsiResponse
                        {
                            Success = true,
                            Cufe = uniqueId,
                            Number = $"{result.Data?.Prefix}{result.Data?.Number}",
                            QrUrl = result.Data?.QrUrl ?? "",
                            Status = "Accepted"
                        };
                    }
                    else
                    {
                         return new PlemsiResponse
                        {
                            Success = false,
                            ErrorMessage = result?.Message ?? "Error desconocido al emitir Nota Crédito Parcial."
                        };
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Plemsi API Error (Partial Credit Note): {StatusCode} - {Content}", response.StatusCode, errorContent);
                    
                    string friendlyMessage = $"Error HTTP {response.StatusCode}";
                    try
                    {
                        // Intentar parsear el error de Plemsi
                        using (JsonDocument doc = JsonDocument.Parse(errorContent))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("info", out var info)) friendlyMessage = info.GetString() ?? friendlyMessage;
                            else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String) friendlyMessage = data.GetString() ?? friendlyMessage;
                            else if (root.TryGetProperty("message", out var message)) friendlyMessage = message.GetString() ?? friendlyMessage;
                        }
                    }
                    catch { /* Si falla el parseo, devolvemos el default */ }

                    return new PlemsiResponse { Success = false, ErrorMessage = friendlyMessage };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando Nota Crédito Parcial a Plemsi");
                return new PlemsiResponse { Success = false, ErrorMessage = $"Excepción: {ex.Message}" };
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
                        // Preferir el contenido del QR (URL DIAN) sobre la URL de la imagen
                        var qrContent = result.Data.QRCode;
                        var qrImg = result.Data.QrUrl;
                        var qrUrl = (!string.IsNullOrEmpty(qrContent) && qrContent.Contains("dian.gov.co")) ? qrContent : (qrImg ?? qrContent);

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

            // Construcción del texto de resolución legal
            string resolutionText = $"Autorización de Facturación No. {_companySettings.DianResolutionNumber} de {_companySettings.DianResolutionDate} Prefijo {_companySettings.DianResolutionPrefix} desde {_companySettings.DianResolutionStartNumber} hasta {_companySettings.DianResolutionEndNumber}";

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

            // Obtener tasa de IVA de la configuración
            decimal taxRate = _configuration.GetValue<decimal>("TaxSettings:IVARate");
            if (taxRate > 1) taxRate /= 100;

            // Mapeo de Items
            var items = sale.Details.Select(d =>
            {
                // UnitPrice incluye IVA. Calculamos base.
                // IMPORTANTE: Redondear a 2 decimales en cada paso para evitar rechazos por diferencias de centavos en los totales.
                decimal taxRateFactor = 1 + taxRate;
                decimal baseUnitPrice = Math.Round(d.UnitPrice / taxRateFactor, 2);
                decimal lineExtensionAmount = Math.Round(baseUnitPrice * d.Quantity, 2);
                decimal taxAmount = Math.Round(lineExtensionAmount * taxRate, 2);

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
                            percent = Math.Round(taxRate * 100, 2),
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
                    tax_amount = Math.Round(g.Sum(t => t.tax_amount), 2),
                    percent = g.First().percent,
                    taxable_amount = Math.Round(g.Sum(t => t.taxable_amount), 2)
                }).ToList();

            return new
            {
                number = invoice.InvoiceID,
                orderReference = new { id_order = sale.SaleID.ToString() }, // Referencia interna de la venta
                send_email = true, // Indicar a Plemsi que envíe el correo al cliente
                date = DateTime.Now.ToString("yyyy-MM-dd"), // Fecha de emisión (Hoy) para cumplir FAD09e
                time = DateTime.Now.ToString("HH:mm:ss"),   // Hora de emisión (Ahora)
                prefix = _companySettings.DianResolutionPrefix,
                resolution = _companySettings.DianResolutionNumber, // Corregido: 'resolution' en lugar de 'resolution_number'
                resolutionText = resolutionText, // Texto legal de la resolución
                head_note = $"Venta POS #{sale.SaleID}", // Encabezado opcional
                foot_note = "Gracias por su compra en PhonePalace", // Pie de página opcional
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

        private object BuildCreditNotePayload(Sale sale, string reason, string originalCufe)
        {
            // Reutilizamos la lógica base de construcción de items y cliente
            // Nota: Para una implementación robusta, se debería refactorizar BuildPayload para compartir lógica común.
            // Aquí duplicamos la parte esencial para asegurar que la Nota Crédito sea idéntica a la Factura original.
            
            // 1. Obtener el payload base como si fuera una factura para reutilizar la estructura de 'customer', 'items', etc.
            // Esto es un truco para no duplicar todo el código de mapeo de items e impuestos.
            var basePayload = BuildPayload(sale);
            
            // Usamos reflexión o serialización/deserialización para acceder a las propiedades del objeto anónimo basePayload
            // Para simplicidad y rendimiento en este contexto, asumimos que podemos acceder a las propiedades si usamos dynamic
            // o reconstruimos lo necesario. Dado que BuildPayload retorna object (anónimo), lo mejor es castear a dynamic.
            dynamic dynamicBase = basePayload;

            string invoicePrefix = _companySettings.DianResolutionPrefix;
            string invoiceNumber = sale.Invoice.InvoiceID.ToString();
            string fullInvoiceNumber = $"{invoicePrefix}{invoiceNumber}";

            // Usar la resolución de NC si está definida, si no, usar la de facturación como fallback.
            string ncPrefix = !string.IsNullOrEmpty(_companySettings.CreditNotePrefix) 
                ? _companySettings.CreditNotePrefix 
                : _companySettings.DianResolutionPrefix;
            string ncResolution = !string.IsNullOrEmpty(_companySettings.CreditNoteResolutionNumber) 
                ? _companySettings.CreditNoteResolutionNumber : _companySettings.DianResolutionNumber;

            // Construcción específica para Nota Crédito
            return new
            {
                prefix = ncPrefix,
                resolution = ncResolution,
                send_email = true,
                
                // Referencia a la factura original (Obligatorio para NC)
                invoiceReference = new
                {
                    number = fullInvoiceNumber,
                    uuid = originalCufe,
                    issue_date = sale.Invoice.SaleDate.ToString("yyyy-MM-dd")
                },

                // Razón de la anulación
                discrepancy = new
                {
                    code = 2, // 2: Anulación de factura electrónica
                    description = reason ?? "Anulación de venta"
                },

                // Datos reutilizados de la factura original
                customer = dynamicBase.customer,
                items = dynamicBase.items,
                
                payment = dynamicBase.payment, // Restaurado según prototipo
                
                notes = $"Nota Crédito por anulación de venta #{sale.SaleID}",
                
                // Totales
                invoiceBaseTotal = dynamicBase.invoiceBaseTotal,
                invoiceTaxInclusiveTotal = dynamicBase.invoiceTaxInclusiveTotal,
                invoiceTaxExclusiveTotal = dynamicBase.invoiceTaxExclusiveTotal,
                totalToPay = dynamicBase.totalToPay,
                allTaxTotals = dynamicBase.allTaxTotals
            };
        }

        private object BuildPartialCreditNotePayload(Return returnEntity, Sale sale, string reason, string originalCufe)
        {
            // 1. Reutilizamos la estructura base de la venta para cliente y pagos
            var basePayload = BuildPayload(sale);
            dynamic dynamicBase = basePayload;

            string invoicePrefix = _companySettings.DianResolutionPrefix;
            string invoiceNumber = sale.Invoice.InvoiceID.ToString();
            string fullInvoiceNumber = $"{invoicePrefix}{invoiceNumber}";

            // Usar la resolución de NC si está definida, si no, usar la de facturación como fallback.
            string ncPrefix = !string.IsNullOrEmpty(_companySettings.CreditNotePrefix) 
                ? _companySettings.CreditNotePrefix 
                : _companySettings.DianResolutionPrefix;
            string ncResolution = !string.IsNullOrEmpty(_companySettings.CreditNoteResolutionNumber) 
                ? _companySettings.CreditNoteResolutionNumber : _companySettings.DianResolutionNumber;

            // Obtener tasa de IVA
            decimal taxRate = _configuration.GetValue<decimal>("TaxSettings:IVARate");
            if (taxRate > 1) taxRate /= 100;

            // 2. Construir items SOLO con lo devuelto
            var items = returnEntity.Details.Select(d =>
            {
                decimal taxRateFactor = 1 + taxRate;
                decimal baseUnitPrice = Math.Round(d.UnitPrice / taxRateFactor, 2);
                decimal lineExtensionAmount = Math.Round(baseUnitPrice * d.Quantity, 2);
                decimal taxAmount = Math.Round(lineExtensionAmount * taxRate, 2);

                // Buscar nombre del producto en la venta original (ya que ReturnDetail puede no tener la navegación cargada)
                var originalDetail = sale.Details.FirstOrDefault(sd => sd.ProductID == d.ProductID);
                string productName = originalDetail?.Product?.Name ?? "Producto devuelto";
                string productCode = originalDetail?.Product?.Code ?? originalDetail?.Product?.SKU ?? d.ProductID.ToString();

                return new
                {
                    unit_measure_id = 70, 
                    invoiced_quantity = d.Quantity,
                    line_extension_amount = lineExtensionAmount,
                    free_of_charge_indicator = false,
                    description = productName,
                    code = productCode,
                    type_item_identification_id = 4,
                    price_amount = baseUnitPrice,
                    base_quantity = d.Quantity,
                    tax_totals = new[]
                    {
                        new
                        {
                            tax_id = 1,
                            percent = Math.Round(taxRate * 100, 2),
                            tax_amount = taxAmount, 
                            taxable_amount = lineExtensionAmount
                        }
                    }
                };
            }).ToList();

            // 3. Recalcular totales para la nota parcial
            decimal invoiceBaseTotal = items.Sum(i => (decimal)i.line_extension_amount);
            // Nota: Acceso dinámico a propiedades anónimas
            decimal totalTax = items.Sum(i => (decimal)((dynamic)i).tax_totals[0].tax_amount);
            decimal invoiceTaxInclusiveTotal = invoiceBaseTotal + totalTax;

            var allTaxTotals = items
                .SelectMany(i => (IEnumerable<dynamic>)((dynamic)i).tax_totals)
                .GroupBy(t => (int)t.tax_id)
                .Select(g => new
                {
                    tax_id = g.Key,
                    tax_amount = Math.Round(g.Sum(t => (decimal)t.tax_amount), 2),
                    percent = g.First().percent,
                    taxable_amount = Math.Round(g.Sum(t => (decimal)t.taxable_amount), 2)
                }).ToList();

            return new
            {
                type_document_id = 91, // Nota Crédito
                prefix = ncPrefix,
                resolution = ncResolution,
                send_email = true,

                invoiceReference = new
                {
                    number = fullInvoiceNumber,
                    uuid = originalCufe,
                    issue_date = sale.Invoice.SaleDate.ToString("yyyy-MM-dd")
                },
                discrepancy = new
                {
                    code = 1, // 1: Devolución parcial de bienes
                    description = reason ?? "Devolución parcial"
                },
                customer = dynamicBase.customer,
                items = items,
                payment = dynamicBase.payment, // Restaurado
                notes = $"Nota Crédito Parcial por devolución venta #{sale.SaleID}",
                invoiceBaseTotal,
                invoiceTaxInclusiveTotal,
                invoiceTaxExclusiveTotal = invoiceBaseTotal, // En parciales suele ser igual si no hay descuentos globales
                totalToPay = invoiceTaxInclusiveTotal,
                allTaxTotals
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