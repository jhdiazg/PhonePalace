using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PhonePalace.Domain.Entities;
using System.Linq;

namespace PhonePalace.Web.Documents
{
    public class QuotePdfDocument
    {
        public Quote Model { get; }
        public byte[] LogoBytes { get; }

        public QuotePdfDocument(Quote model, byte[] logoBytes)
        {
            Model = model;
            LogoBytes = logoBytes;
        }

        public byte[] GeneratePdf()
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf();
        }

        void ComposeHeader(IContainer container)
        {
            var titleStyle = TextStyle.Default.FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Cotización #{Model.QuoteID}").Style(titleStyle);
                    column.Item().Text(text =>
                    {
                        text.Span("Fecha: ").SemiBold();
                        text.Span($"{Model.QuoteDate:dd/MM/yyyy}");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Vence: ").SemiBold();
                        text.Span($"{Model.ExpirationDate:dd/MM/yyyy}");
                    });
                });

                if (LogoBytes != null && LogoBytes.Length > 0)
                {
                    row.ConstantItem(100).Image(LogoBytes);
                }
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(40).Column(column =>
            {
                column.Spacing(5);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Cliente").SemiBold();
                        c.Item().Text(Model.Client.DisplayName);
                        c.Item().Text(Model.Client.Email ?? "");
                        c.Item().Text(Model.Client.PhoneNumber ?? "");
                    });
                });

                column.Item().PaddingTop(20).Element(ComposeTable);

                column.Item().PaddingTop(10).AlignRight().Text(text =>
                {
                    text.Span("Total: ").SemiBold().FontSize(14);
                    text.Span($"{Model.Total:C}").FontSize(14);
                });
            });
        }

        void ComposeTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("#");
                    header.Cell().Element(CellStyle).Text("Producto");
                    header.Cell().Element(CellStyle).AlignRight().Text("Precio Unit.");
                    header.Cell().Element(CellStyle).AlignRight().Text("Cant.");
                    header.Cell().Element(CellStyle).AlignRight().Text("Total");

                    static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                });

                var index = 1;
                foreach (var item in Model.Details)
                {
                    table.Cell().Element(CellStyle).Text(index.ToString());
                    table.Cell().Element(CellStyle).Text(item.Product.Name);
                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice:C}");
                    table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
                    table.Cell().Element(CellStyle).AlignRight().Text($"{(item.UnitPrice * item.Quantity):C}");

                    static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                    index++;
                }
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x => { x.Span("Página "); x.CurrentPageNumber(); });
        }
    }
}