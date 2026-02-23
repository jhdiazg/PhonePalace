using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PhonePalace.Domain.Entities;
using System.IO;
using System.Globalization;

namespace PhonePalace.Web.Documents
{
    public class PurchasePdfDocument
    {
        private readonly Purchase _purchase;
        private readonly byte[] _logoBytes;
        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");

        public PurchasePdfDocument(Purchase purchase, byte[] logoBytes)
        {
            _purchase = purchase;
            _logoBytes = logoBytes;
        }

        public byte[] GeneratePdf()
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                    });
                });
            });

            using (var stream = new MemoryStream())
            {
                document.GeneratePdf(stream);
                return stream.ToArray();
            }
        }

        private void ComposeHeader(IContainer container)
        {
            var titleStyle = TextStyle.Default.FontSize(16).SemiBold().FontColor(Colors.Blue.Medium);

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    if (_logoBytes != null && _logoBytes.Length > 0)
                    {
                        column.Item().Width(150).Height(50).Image(_logoBytes).FitArea();
                    }
                    column.Item().Text($"Orden de Compra #{_purchase.Id}").Style(titleStyle);

                    column.Item().Text(text =>
                    {
                        text.Span("Fecha de Compra: ").SemiBold();
                        text.Span($"{_purchase.PurchaseDate:d}");
                    });
                });
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.PaddingVertical(40).Column(column =>
            {
                column.Spacing(20);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text("Proveedor").SemiBold();
                        column.Item().Text(_purchase.Supplier?.DisplayName ?? string.Empty);
                        column.Item().Text(_purchase.Supplier?.StreetAddress ?? string.Empty);
                        column.Item().Text($"{_purchase.Supplier?.Municipality?.Name ?? string.Empty}, {_purchase.Supplier?.Department?.Name ?? string.Empty}");
                        column.Item().Text(_purchase.Supplier?.PhoneNumber ?? string.Empty);
                        column.Item().Text(_purchase.Supplier?.Email ?? string.Empty);
                    });
                });

                column.Item().Element(ComposeTable);

                column.Item().AlignRight().Text($"Total: {_purchase.TotalAmount.ToString("N2", UsCulture)}").Bold();
            });
        }

        private void ComposeTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Text("Producto");
                    header.Cell().AlignRight().Text("Cantidad");
                    header.Cell().AlignRight().Text("Precio Unitario");
                    header.Cell().AlignRight().Text("Subtotal");
                });

                if (_purchase.PurchaseDetails != null)
                {
                    foreach (var item in _purchase.PurchaseDetails)
                    {
                        table.Cell().Text(item.Product?.Name ?? string.Empty);
                        table.Cell().AlignRight().Text(item.Quantity.ToString());
                        table.Cell().AlignRight().Text(item.UnitPrice.ToString("N2", UsCulture));
                        table.Cell().AlignRight().Text(item.TotalPrice.ToString("N2", UsCulture));
                    }
                }
            });
        }
    }
}