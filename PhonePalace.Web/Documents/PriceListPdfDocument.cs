using System;
using System.Collections.Generic;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PhonePalace.Web.ViewModels;
using PhonePalace.Domain.Enums;

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

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer container)
        {
            container
                .Page(page =>
                {
                    page.Margin(20);
                    
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
            var titleStyle = TextStyle.Default.FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Lista de Precios - {_type}").Style(titleStyle);
                    column.Item().Text(text =>
                    {
                        text.Span("Fecha de generación: ").SemiBold();
                        text.Span($"{DateTime.Now:g}");
                    });
                });

                if (_logoData != null && _logoData.Length > 0)
                {
                    row.ConstantItem(100).Image(_logoData);
                }
            });
        }

        void ComposeContent(IContainer container)
        {
            var groupedProducts = _products.GroupBy(p => p.CategoryName).OrderBy(g => g.Key);

            container.PaddingVertical(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        columns.RelativeColumn(1.2f); // Código
                        columns.RelativeColumn(3.5f); // Producto
                        columns.RelativeColumn(1.3f); // Precio
                        
                        if (i < 2) columns.ConstantColumn(10); // Spacer
                    }
                });

                table.Header(header =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        header.Cell().Element(HeaderCellStyle).Text("Cód");
                        header.Cell().Element(HeaderCellStyle).Text("Producto");
                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("Precio");
                        
                        if (i < 2) header.Cell();
                    }
                });

                foreach (var group in groupedProducts)
                {
                    // Header de Categoría
                    table.Cell().ColumnSpan(11).Background(Colors.Grey.Lighten3).Padding(2).Text(group.Key).FontSize(7).SemiBold();

                    var products = group.ToList();
                    var rows = (int)Math.Ceiling(products.Count / 3.0);

                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            var index = r * 3 + c;
                            if (index < products.Count)
                            {
                                var product = products[index];
                                decimal percentage = (int)_type;
                                decimal rawPrice = product.Cost * (1 + percentage / 100m);
                                decimal calculatedPrice = Math.Round(rawPrice / 1000m) * 1000;
                                
                                table.Cell().Element(CellStyle).Text(product.Code ?? product.SKU ?? "-");
                                table.Cell().Element(CellStyle).Text(product.Name);
                                table.Cell().Element(CellStyle).AlignRight().Text($"{calculatedPrice:C0}");
                            }
                            else
                            {
                                table.Cell();
                                table.Cell();
                                table.Cell();
                            }

                            if (c < 2) table.Cell();
                        }
                    }
                }
            });
        }

        static IContainer HeaderCellStyle(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).DefaultTextStyle(x => x.FontSize(7).SemiBold());
        }

        static IContainer CellStyle(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).PaddingVertical(2).DefaultTextStyle(x => x.FontSize(6));
        }
    }
}