using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PhonePalace.Web.ViewModels;
using System.Collections.Generic;
using System;

namespace PhonePalace.Web.Documents
{
    public class InventoryPdfDocument : IDocument
    {
        private readonly List<InventoryReportItemViewModel> _items;
        private readonly byte[] _logoData;

        public InventoryPdfDocument(List<InventoryReportItemViewModel> items, byte[] logoData)
        {
            _items = items;
            _logoData = logoData;
        }

        public byte[] GeneratePdf()
        {
            return ((IDocument)this).GeneratePdf();
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(6).FontFamily("Helvetica"));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }

        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                if (_logoData != null && _logoData.Length > 0)
                {
                    row.ConstantItem(60).Image(_logoData);
                }
                
                row.RelativeItem().PaddingLeft(10).Column(column =>
                {
                    column.Item().Text("PLANILLA DE TOMA FÍSICA DE INVENTARIO").FontSize(10).SemiBold().FontColor(Colors.Blue.Medium);
                    column.Item().Text($"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8);
                });
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30); // #
                    columns.RelativeColumn();   // Producto
                    columns.ConstantColumn(80); // SKU
                    columns.ConstantColumn(60); // Stock Sistema
                    columns.ConstantColumn(80); // Conteo Físico
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("#");
                    header.Cell().Element(CellStyle).Text("Producto");
                    header.Cell().Element(CellStyle).Text("SKU");
                    header.Cell().Element(CellStyle).Text("Sistema");
                    header.Cell().Element(CellStyle).Text("Físico");

                    static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                });

                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    table.Cell().Element(CellStyle).Text((i + 1).ToString());
                    table.Cell().Element(CellStyle).Text(item.ProductName);
                    table.Cell().Element(CellStyle).Text(item.ProductSKU);
                    table.Cell().Element(CellStyle).Text(item.CurrentStock.ToString());
                    table.Cell().Element(CellStyle).Text("_______"); // Espacio para escribir

                    static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                }
            });
        }
    }
}