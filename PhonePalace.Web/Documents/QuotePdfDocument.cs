using PhonePalace.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace PhonePalace.Web.Documents
{
    public class QuotePdfDocument : IDocument
    {
        private readonly Quote _quote;
        private readonly byte[] _logoBytes;

        public QuotePdfDocument(Quote quote, byte[] logoBytes)
        {
            _quote = quote;
            _logoBytes = logoBytes;

            // Asegurarse de que la cultura para el formato de moneda sea la correcta
            // Esto es importante para que los formatos de moneda y fecha se muestren correctamente
            // Por ejemplo, para Colombia, se usa "es-CO" que es el español
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
                    column.Item().Text($"Cotización #{_quote.QuoteID:D4}").Style(titleStyle);

                    column.Item().Text(text =>
                    {
                        text.Span("Fecha de Cotización: ").SemiBold();
                        text.Span($"{_quote.QuoteDate:d}");
                    });

                    column.Item().Text(text =>
                    {
                        text.Span("Válida hasta: ").SemiBold();
                        text.Span($"{_quote.ExpirationDate:d}");
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
                        clientColumn.Item().Text(_quote.Client?.DisplayName ?? "N/A");
                        clientColumn.Item().Text(_quote.Client?.Email ?? "N/A");
                        clientColumn.Item().Text(_quote.Client?.PhoneNumber ?? "N/A");
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

                // Sección de Totales
                column.Item().AlignRight().Column(totalsCol =>
                {
                    totalsCol.Spacing(2);
                    totalsCol.Item().AlignRight().Text($"Subtotal: {_quote.Subtotal:C}").FontSize(12);
                    totalsCol.Item().AlignRight().Text($"IVA ({(_quote.Subtotal > 0 ? Math.Round((_quote.Tax / _quote.Subtotal) * 100, 1) : 0)}%): {_quote.Tax:C}").FontSize(12);
                    totalsCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    totalsCol.Item().Text($"Total: {_quote.Total:C}").FontSize(14).Bold();
                });

                column.Item().PaddingTop(25).Text("Gracias por su preferencia.").FontSize(10).Italic();
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

                foreach (var detail in _quote.Details)
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
