using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhonePalace.Domain.Entities
{
    public class Bank
    {
        public int BankID { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string AccountNumber { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal Balance { get; set; }

        public virtual ICollection<BankTransaction> Transactions { get; set; } = new List<BankTransaction>();
    }
}
