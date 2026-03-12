using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PhonePalace.Domain.Entities;
using PhonePalace.Infrastructure.Configuration;
using System.Linq;

namespace PhonePalace.Web.Documents
{
    public class QuotePdfDocument
    {
        public Quote Model { get; }
        public byte[] LogoBytes { get; }
        private readonly CompanySettings _companySettings;

        public QuotePdfDocument(Quote model, byte[] logoBytes, CompanySettings companySettings)
        {
            Model = model;
            LogoBytes = logoBytes;
            _companySettings = companySettings;
        }

        public byte[] GeneratePdf()
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf();
        }

        void ComposeHeader(IContainer container)
        {
            var titleStyle = TextStyle.Default.FontSize(12).SemiBold().FontColor(Colors.Blue.Medium);

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Cotización # {Model.QuoteID}").Style(titleStyle);
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
                column.Spacing(20);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Cliente").SemiBold();
                        if (Model.Client != null)
                        {
                            c.Item().Text(Model.Client.DisplayName);
                            c.Item().Text(Model.Client.Email ?? "Sin correo");
                            c.Item().Text(Model.Client.PhoneNumber ?? "Sin teléfono");
                        }
                        else
                        {
                            c.Item().Text("Cliente no especificado").Italic();
                        }
                    });
                    row.RelativeItem().Column(companyColumn =>
                    {
                        companyColumn.Item().Text("De").SemiBold().AlignRight();
                        companyColumn.Item().Text(_companySettings.CompanyName).AlignRight();
                        if (!string.IsNullOrEmpty(_companySettings.NIT))
                        {
                            companyColumn.Item().Text($"NIT: {_companySettings.NIT}").AlignRight();
                        }
                        companyColumn.Item().Text(_companySettings.Email).AlignRight();
                        companyColumn.Item().Text(_companySettings.PhoneNumber).AlignRight();
                        if (!string.IsNullOrEmpty(_companySettings.Address))
                        {
                            companyColumn.Item().Text(_companySettings.Address).AlignRight();
                        }
                    });
                });

                column.Item().PaddingTop(20).Element(ComposeTable);

                column.Item().PaddingTop(10).AlignRight().Text(text =>
                {
                    text.Span("Total: ").SemiBold().FontSize(12);
                    text.Span($"{Model.Total:C}").FontSize(12).Bold();
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

                    static IContainer CellStyle(IContainer c) => c.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                });

                // Estilo para las celdas del cuerpo de la tabla, definido una sola vez para claridad y eficiencia.
                static IContainer BodyCellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);

                // Verificación para evitar errores si no hay detalles.
                if (Model.Details == null || !Model.Details.Any())
                {
                    table.Cell().ColumnSpan(5).Padding(10).Text("No hay productos en esta cotización.").Italic();
                    return;
                }

                var index = 1;
                foreach (var item in Model.Details)
                {
                    // Usamos el BodyCellStyle y agregamos un null-check para el nombre del producto.
                    // Esto soluciona el error si un producto no se carga correctamente.
                    table.Cell().Element(BodyCellStyle).Text(index.ToString());
                    table.Cell().Element(BodyCellStyle).Text(item.Product?.Name ?? "Producto no encontrado");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"{item.UnitPrice:C}");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text(item.Quantity.ToString());
                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"{(item.UnitPrice * item.Quantity):C}");

                    index++;
                }
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                column.Item().PaddingTop(5).Text(_companySettings.WarrantyText).FontSize(8).Justify();
                column.Item().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }
    }
}