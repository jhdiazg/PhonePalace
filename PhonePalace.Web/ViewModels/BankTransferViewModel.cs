using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.ViewModels
{
    public enum BankTransferType
    {
        [Display(Name = "Entre Bancos")]
        BankToBank = 0,
        [Display(Name = "Retiro (Banco a Caja)")]
        BankToCash = 1,
        [Display(Name = "Consignación (Caja a Banco)")]
        CashToBank = 2
    }

    public class BankTransferViewModel
    {
        [Required(ErrorMessage = "Seleccione el tipo de transferencia.")]
        [Display(Name = "Tipo de Operación")]
        public BankTransferType TransferType { get; set; }

        [Display(Name = "Banco Origen")]
        public int? SourceBankId { get; set; }

        [Display(Name = "Banco Destino")]
        public int? TargetBankId { get; set; }

        [Required(ErrorMessage = "El monto es requerido.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        [DataType(DataType.Currency)]
        [Display(Name = "Monto")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "La descripción es requerida.")]
        [Display(Name = "Descripción / Referencia")]
        public string Description { get; set; } = string.Empty;

        public SelectList? Banks { get; set; }
    }
}