using System;
using System.Collections.Generic;

namespace PhonePalace.Domain.Entities
{
    public class Bank
    {
        public int BankID { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string AccountNumber { get; set; } = string.Empty;
    }
}
