using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
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

            // Seed Categories
            if (!await context.Categories.AnyAsync())
            {
                var categories = new[]
                {
                    new Category { Name = "ACCESORIOS PARA CELULAR", IsActive = true },
                    new Category { Name = "CABLES", IsActive = true },
                    new Category { Name = "CABLES DE AUDIO", IsActive = true },
                    new Category { Name = "CABLES DE RED", IsActive = true },
                    new Category { Name = "CABLES HDMI", IsActive = true },
                    new Category { Name = "CAMARAS IMOU", IsActive = true },
                    new Category { Name = "CARGADORES PARA PORTATIL ", IsActive = true },
                    new Category { Name = "CCTV", IsActive = true },
                    new Category { Name = "CELULARES", IsActive = true },
                    new Category { Name = "CELULARES HENRY", IsActive = true },
                    new Category { Name = "CONVERTIDORES", IsActive = true },
                    new Category { Name = "DISCOS ESTADO SOLIDO (SSD)", IsActive = true },
                    new Category { Name = "EXTENDERS", IsActive = true },
                    new Category { Name = "EXTENSION DE CORRIENTE", IsActive = true },
                    new Category { Name = "SERVICIO TECNICO", IsActive = true },
                    new Category { Name = "SPLITTER", IsActive = true },
                    new Category { Name = "TV", IsActive = true },
                    new Category { Name = "VENTILADORES ", IsActive = true }
                };
                await context.Categories.AddRangeAsync(categories);
                await context.SaveChangesAsync();
            }

            // Seed Brands
            if (!await context.Brands.AnyAsync())
            {
                var brands = new[]
                {
                    new Brand { Name = "Samsung", IsActive = true },
                    new Brand { Name = "Apple", IsActive = true },
                    new Brand { Name = "Motorola", IsActive = true },
                    new Brand { Name = "Oppo", IsActive = true }
                };
                await context.Brands.AddRangeAsync(brands);
                await context.SaveChangesAsync();
            }

            // Seed Models
            if (!await context.Models.AnyAsync())
            {
                var samsungBrand = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Samsung");
                var appleBrand = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Apple");
                var motorolaBrand = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Motorola");

                var models = new[]
                {
                    new Model { BrandID = samsungBrand?.BrandID ?? 1, Name = "Galaxy S23", IsActive = true },
                    new Model { BrandID = samsungBrand?.BrandID ?? 1, Name = "Galaxy A54", IsActive = true },
                    new Model { BrandID = appleBrand?.BrandID ?? 2, Name = "iPhone 14", IsActive = true },
                    new Model { BrandID = appleBrand?.BrandID ?? 2, Name = "iPhone 15", IsActive = true },
                    new Model { BrandID = motorolaBrand?.BrandID ?? 3, Name = "Moto G", IsActive = true }
                };
                await context.Models.AddRangeAsync(models);
                await context.SaveChangesAsync();
            }

            // Seed Products
            if (!await context.Products.AnyAsync())
            {
                var categoryCellphones = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Celulares");
                var categoryAccessories = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Accesorios");
                var samsungModel = await context.Models.FirstOrDefaultAsync(m => m.Name == "Galaxy S23");
                var appleModel = await context.Models.FirstOrDefaultAsync(m => m.Name == "iPhone 14");

                var products = new Product[]
                {
                    new CellPhone
                    {
                        Name = "Samsung Galaxy S23 Ultra",
                        SKU = "SS-GS23U-128",
                        Description = "Samsung Galaxy S23 Ultra 128GB",
                        Price = 4500000m,
                        Cost = 3800000m,
                        CategoryID = categoryCellphones?.CategoryID ?? 1,
                        ModelID = samsungModel?.ModelID ?? 1,
                        Color = "Phantom Black",
                        StorageGB = PhonePalace.Domain.Enums.StorageGB._128,
                        RamGB = PhonePalace.Domain.Enums.RamGB._8,
                        IsActive = true
                    },
                    new CellPhone
                    {
                        Name = "iPhone 14 Pro Max",
                        SKU = "AP-IP14PM-256",
                        Description = "Apple iPhone 14 Pro Max 256GB",
                        Price = 5200000m,
                        Cost = 4500000m,
                        CategoryID = categoryCellphones?.CategoryID ?? 1,
                        ModelID = appleModel?.ModelID ?? 3,
                        Color = "Deep Purple",
                        StorageGB = PhonePalace.Domain.Enums.StorageGB._256,
                        RamGB = PhonePalace.Domain.Enums.RamGB._6,
                        IsActive = true
                    },
                    new Accessory
                    {
                        Name = "Funda Samsung Galaxy S23",
                        SKU = "ACC-SS-GS23-CASE",
                        Description = "Funda protectora para Samsung Galaxy S23",
                        Price = 50000m,
                        Cost = 30000m,
                        CategoryID = categoryAccessories?.CategoryID ?? 2,
                        BrandID = 1, // Samsung
                        Material = "Silicone",
                        Color = "Black",
                        Compatibility = "Samsung Galaxy S23",
                        IsActive = true
                    },
                    new Accessory
                    {
                        Name = "Cargador iPhone 20W",
                        SKU = "ACC-AP-CHR-20W",
                        Description = "Cargador rápido Apple 20W USB-C",
                        Price = 150000m,
                        Cost = 120000m,
                        CategoryID = categoryAccessories?.CategoryID ?? 2,
                        BrandID = 2, // Apple
                        Material = "Plastic",
                        Color = "White",
                        Compatibility = "iPhone con USB-C",
                        IsActive = true
                    }
                };

                await context.Products.AddRangeAsync(products);
                await context.SaveChangesAsync();
            }
        }
    }
}