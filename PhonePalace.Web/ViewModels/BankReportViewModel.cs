using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Entities;
using System;
using System.Collections.Generic;

namespace PhonePalace.Web.ViewModels
{
    public class BankReportViewModel
    {
        public int? BankId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public SelectList? Banks { get; set; }
        public List<BankTransaction> Transactions { get; set; } = new List<BankTransaction>();
        public decimal PreviousBalance { get; set; }
    }
}