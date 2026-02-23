﻿using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PhonePalace.Web.Helpers;
using PhonePalace.Infrastructure.Configuration;
using System.Globalization;
using System.Linq;

namespace PhonePalace.Web.Documents
{
    public class InvoicePdfDocument : IDocument
    {
        private readonly Sale _sale;
        private readonly byte[] _logoBytes;
        private readonly CompanySettings _companySettings;
        private readonly string _sellerName;

        public InvoicePdfDocument(Sale sale, byte[] logoBytes, CompanySettings companySettings, string sellerName)
        {
            _sale = sale;
            _logoBytes = logoBytes;
            _companySettings = companySettings;
            _sellerName = sellerName;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            var isHalfLetter = _sale.Details.Count <= 5;

            container
                .Page(page =>
                {
                    page.Margin(20);
                    
                    if (isHalfLetter) 
                    {
                        page.Size(new PageSize(PageSizes.Letter.Width, PageSizes.Letter.Height / 2));
                    } 
                    else 
                    { 
                        page.Size(PageSizes.Letter); 
                    }



                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
        }

        void ComposeHeader(IContainer container)
        {
            var titleStyle = TextStyle.Default.FontSize(14).SemiBold().FontColor(Colors.Blue.Medium);

            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text($"Documento para Garantía # {_sale.Invoice.InvoiceID:D5}").Style(titleStyle);
                    column.Item().Text(text =>
                    {
                        text.Span("Fecha de Venta: ").SemiBold();
                        text.Span($"{_sale.SaleDate:d}");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Estado: ").SemiBold();
                        text.Span(_sale.Invoice.Status == InvoiceStatus.Cancelled ? "Anulada" : "Completada");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Vendedor: ").SemiBold();
                        text.Span(_sellerName);
                    });
                });
                if (_logoBytes != null && _logoBytes.Length > 0)
                {
                    row.ConstantItem(100).Image(_logoBytes);
                }
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(10).Column(column =>
            {
                column.Spacing(5);
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(clientColumn =>
                    {
                        clientColumn.Item().Text("Cliente").SemiBold();
                        clientColumn.Item().Text(_sale.Client?.DisplayName ?? "N/A");
                        if (_sale.Client is NaturalPerson naturalPerson)
                        {
                            clientColumn.Item().Text($"{EnumHelper.GetDisplayName(naturalPerson.DocumentType)}: {naturalPerson.DocumentNumber}");
                        }
                        else if (_sale.Client is LegalEntity legalEntity)
                        {
                            clientColumn.Item().Text($"NIT: {legalEntity.NIT}");
                        }
                        clientColumn.Item().Text(_sale.Client?.Email ?? "N/A");
                        clientColumn.Item().Text(_sale.Client?.PhoneNumber ?? "N/A");
                        if (!string.IsNullOrEmpty(_sale.Client?.MunicipalityID))
                        {
                            clientColumn.Item().Text($"ID Municipio: {_sale.Client.MunicipalityID}");
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

                column.Item().Element(ComposeTable);

                column.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Column(paymentsCol =>
                    {
                        paymentsCol.Spacing(2);
                        paymentsCol.Item().Text("Pagos Recibidos").Bold();
                        if (_sale.Invoice.Payments.Any())
                        {
                            foreach (var payment in _sale.Invoice.Payments)
                            {
                                paymentsCol.Item().Text($"{EnumHelper.GetDisplayName(payment.PaymentMethod)}: {payment.Amount:C}");
                            }
                        }
                        else
                        {
                            paymentsCol.Item().Text("No hay pagos registrados para esta venta.").Italic();
                        }
                        paymentsCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        paymentsCol.Item().Text($"Total Pagado: {_sale.Invoice.Payments.Sum(p => p.Amount):C}").SemiBold();
                    });

                    row.RelativeItem().AlignRight().Column(totalsCol =>
                    {
                        totalsCol.Spacing(2);
                        //totalsCol.Item().AlignRight().Text($"Subtotal: {_sale.Invoice.Subtotal:C}").FontSize(8);
                        //totalsCol.Item().AlignRight().Text($"IVA ({(_sale.Invoice.Subtotal > 0 ? Math.Round((_sale.Invoice.Tax / _sale.Invoice.Subtotal) * 100, 1) : 0)}%): {_sale.Invoice.Tax:C}").FontSize(8);
                        totalsCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        totalsCol.Item().AlignRight().Text($"Total: {_sale.Invoice.Total:C}").FontSize(10).Bold();
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

                foreach (var detail in _sale.Details)
                {
                    table.Cell().Element(BodyCellStyle).Text(detail.Product?.Name ?? "Producto no encontrado");
                    table.Cell().Element(BodyCellStyle).AlignCenter().Text(detail.Quantity.ToString());
                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"{detail.UnitPrice:C}");
                    table.Cell().Element(BodyCellStyle).AlignRight().Text($"{(detail.Quantity * detail.UnitPrice):C}");
                }

                static IContainer HeaderCellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2);
                static IContainer BodyCellStyle(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(2);
            });
        }

        void ComposeFooter(IContainer container)
        {
            container.Column(column =>
            {
                column.Spacing(5);
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                column.Item().PaddingTop(5).Text(_companySettings.WarrantyText).FontSize(6).Justify();
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