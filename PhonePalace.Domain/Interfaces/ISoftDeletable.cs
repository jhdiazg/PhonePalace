using System;

namespace PhonePalace.Domain.Interfaces
{
    public interface ISoftDeletable
    {
        public bool IsDeleted { get; set; }
        public DateTime? DeletedOn { get; set; }
    }
}
// This interface defines the properties required for soft deletion functionality.
// Classes implementing this interface can be marked as deleted without removing them from the database.
// The IsDeleted property indicates whether the object has been deleted.
// The DeletedOn property stores the timestamp when the object was deleted. 
// This allows for tracking and auditing purposes.
