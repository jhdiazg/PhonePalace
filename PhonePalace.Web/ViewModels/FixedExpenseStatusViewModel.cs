using PhonePalace.Domain.Entities;

namespace PhonePalace.Web.ViewModels
{
    public class FixedExpenseStatusViewModel
    {
        public FixedExpense? FixedExpense { get; set; }
        public FixedExpensePayment? LastPayment { get; set; }
    }
}