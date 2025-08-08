using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Linq;

namespace PhonePalace.Web.Documents
{
    public class InvoicePdfDocument : IDocument
    {
        private readonly Invoice _invoice;
        private readonly byte[] _logoBytes;

        public InvoicePdfDocument(Invoice invoice, byte[] logoBytes)
        {
            _invoice = invoice;
            _logoBytes = logoBytes;

            // Establecer la cultura a Español (Colombia) para el formato de moneda y fechas.
            CultureInfo.CurrentCulture = new CultureInfo("es-CO");
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(50);
                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
        }

        void ComposeHeader(IContainer container)
        {
            var titleStyle = TextStyle.Default.FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Factura #{_invoice.InvoiceID:D5}").Style(titleStyle);
                    column.Item().Text(text =>
                    {
                        text.Span("Fecha de Venta: ").SemiBold();
                        text.Span($"{_invoice.SaleDate:d}");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Estado: ").SemiBold();
                        text.Span(_invoice.Status == InvoiceStatus.Cancelled ? "Anulada" : "Completada");
                    });
                });
                row.ConstantItem(100).Image(_logoBytes);
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(40).Column(column =>
            {
                column.Spacing(20);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(clientColumn =>
                    {
                        clientColumn.Item().Text("Cliente").SemiBold();
                        clientColumn.Item().Text(_invoice.Client?.DisplayName ?? "N/A");
                        clientColumn.Item().Text(_invoice.Client?.Email ?? "N/A");
                        clientColumn.Item().Text(_invoice.Client?.PhoneNumber ?? "N/A");
                    });
                    row.RelativeItem().Column(companyColumn =>
                    {
                        companyColumn.Item().Text("De").SemiBold().AlignRight();
                        companyColumn.Item().Text("Phone Palace").AlignRight();
                        companyColumn.Item().Text("info@phonepalace.com").AlignRight();
                        companyColumn.Item().Text("+57 300 123 4567").AlignRight();
                    });
                });

                column.Item().Element(ComposeTable);

                column.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Column(paymentsCol =>
                    {
                        paymentsCol.Spacing(2);
                        paymentsCol.Item().Text("Pagos Recibidos").Bold();
                        if (_invoice.Payments.Any())
                        {
                            foreach (var payment in _invoice.Payments)
                            {
                                paymentsCol.Item().Text($"{payment.PaymentMethod}: {payment.Amount:C}");
                            }
                        }
                        else
                        {
                            paymentsCol.Item().Text("No hay pagos registrados para esta venta.").Italic();
                        }
                        paymentsCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        paymentsCol.Item().Text($"Total Pagado: {_invoice.Payments.Sum(p => p.Amount):C}").SemiBold();
                    });

                    row.RelativeItem().AlignRight().Column(totalsCol =>
                    {
                        totalsCol.Spacing(2);
                        totalsCol.Item().AlignRight().Text($"Subtotal: {_invoice.Subtotal:C}").FontSize(12);
                        totalsCol.Item().AlignRight().Text($"IVA (19%): {_invoice.Tax:C}").FontSize(12);
                        totalsCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        totalsCol.Item().AlignRight().Text($"Total: {_invoice.Total:C}").FontSize(14).Bold();
                    });
                });
            });
        }

        void ComposeTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Producto").SemiBold();
                    header.Cell().Element(HeaderCellStyle).AlignCenter().Text("Cant.").SemiBold();
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Precio Unit.").SemiBold();
                    header.Cell().Element(HeaderCellStyle).AlignRight().Text("Subtotal").SemiBold();
                });

                foreach (var detail in _invoice.Details)
                {
                    table.Cell().Element(BodyCellStyle).Text(detail.Product?.Name ?? "Producto no encontrado");
                    table.Cell().Element(BodyCellStyle).AlignCenter().Text(detail.Quantity.ToString());
                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"{detail.UnitPrice:C}");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"{detail.LineTotal:C}");
                }

                static IContainer HeaderCellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                static IContainer BodyCellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5);
            });
        }
    }
}