using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PhonePalace.Web.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System;

namespace PhonePalace.Web.Documents
{
    public class PriceListPdfDocument : IDocument
    {
        private readonly List<ProductIndexViewModel> _products;
        private readonly PriceListType _type;
        private readonly byte[] _logoData;

        public PriceListPdfDocument(List<ProductIndexViewModel> products, PriceListType type, byte[] logoData)
        {
            _products = products;
            _type = type;
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
                
                // Contenido a 3 columnas
                page.Content().PaddingTop(10).MultiColumn(column =>
                {
                    column.Columns(3);
                    column.Spacing(15);
                    column.Content().Element(ComposeContent);
                });

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
                    column.Item().Text($"LISTA DE PRECIOS - {_type}").FontSize(10).SemiBold().FontColor(Colors.Blue.Medium);
                    column.Item().Text($"Fecha de generación: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Darken2);
                });
            });
        }

        void ComposeContent(IContainer container)
        {
            container.Column(column =>
            {
                var categories = _products.GroupBy(p => p.CategoryName).OrderBy(g => g.Key);

                foreach (var category in categories)
                {
                    // Envuelve cada categoría y su tabla en un bloque que no se puede dividir entre columnas
                    column.Item().ShowEntire().Column(categoryColumn =>
                    {
                        // Encabezado de Categoría
                        categoryColumn.Item().PaddingTop(5).PaddingBottom(2).Background(Colors.Grey.Lighten3).Padding(2).Text(category.Key.ToUpper())
                            .FontSize(9).Bold().FontColor(Colors.Black);

                        // Tabla de productos
                        categoryColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(35); // Código
                                columns.RelativeColumn();   // Nombre
                                columns.ConstantColumn(45); // Precio
                            });

                            foreach (var product in category.OrderBy(p => p.Name))
                            {
                                decimal percentage = (int)_type / 100m;
                                decimal basePrice = product.Cost * (1 + percentage);
                                decimal finalPrice = Math.Ceiling(basePrice / 1000m) * 1000m;

                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1).Text(product.Code ?? product.SKU ?? "-").FontColor(Colors.Grey.Darken3);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1).Text(product.Name);
                                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1).AlignRight().Text($"{finalPrice:N0}").SemiBold();
                            }
                        });
                    });
                }
            });
        }
    }
}