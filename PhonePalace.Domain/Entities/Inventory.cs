using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhonePalace.Domain.Entities
{
    public class Inventory
    {
        public int InventoryID { get; set; }
        public int ProductID { get; set; }
        public int Stock { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public Product Product { get; set; } = null!;
    }
}