using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Infrastructure.Data
{
    public static class DataSeeder
    {
        public static async Task SeedDaneDataAsync(ApplicationDbContext context)
        {
            // Seed Departments
            if (!await context.Departments.AnyAsync())
            {
                await context.Departments.AddRangeAsync(DaneData.GetDepartments());
                await context.SaveChangesAsync();
            }

            // Seed Municipalities
            if (!await context.Municipalities.AnyAsync())
            {
                await context.Municipalities.AddRangeAsync(DaneData.GetMunicipalities());
                await context.SaveChangesAsync();
            }
        }
    }
}