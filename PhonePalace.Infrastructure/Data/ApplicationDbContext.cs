using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhonePalace.Domain.Entities;
using System.Linq;
using System.Linq.Expressions;

namespace PhonePalace.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Model> Models { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Inventory> Inventories { get; set; }

        // DbSet para los tipos derivados de Product
        public DbSet<CellPhone> CellPhones { get; set; }
        public DbSet<Accessory> Accessories { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<NaturalPerson> NaturalPersons { get; set; }
        public DbSet<LegalEntity> LegalEntities { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Municipality> Municipalities { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<QuoteDetail> QuoteDetails { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceDetail> InvoiceDetails { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<NaturalPersonSupplier> NaturalPersonSuppliers { get; set; }
        public DbSet<LegalEntitySupplier> LegalEntitySuppliers { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseDetail> PurchaseDetails { get; set; }
        public DbSet<AccountPayable> AccountPayables { get; set; }
        public DbSet<Bank> Banks { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleDetail> SaleDetails { get; set; }

        public DbSet<CashRegister> CashRegisters { get; set; }
        public DbSet<CashMovement> CashMovements { get; set; }



        // ... (otros DbSets)
        // ...


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Configuración de Herencia (TPH) ---
            modelBuilder.Entity<Product>()
                .HasDiscriminator<string>("ProductType")
                .HasValue<CellPhone>("CellPhone")
                .HasValue<Accessory>("Accessory");

            modelBuilder.Entity<Client>()
                .HasDiscriminator<string>("ClientType")
                .HasValue<NaturalPerson>("NaturalPerson")
                .HasValue<LegalEntity>("LegalEntity");

            // --- Configuración de Relaciones ---
            modelBuilder.Entity<Department>()
                .HasMany(d => d.Municipalities)
                .WithOne(m => m.Department)
                .HasForeignKey(m => m.DepartmentID);

            modelBuilder.Entity<Client>()
                .HasOne(c => c.Municipality)
                .WithMany()
                .HasForeignKey(c => c.MunicipalityID)
                .OnDelete(DeleteBehavior.Restrict); // Evitar borrado en cascada

            modelBuilder.Entity<Client>()
                .HasOne(c => c.Department)
                .WithMany()
                .HasForeignKey(c => c.DepartmentID)
                .OnDelete(DeleteBehavior.Restrict); // Evitar borrado en cascada

            modelBuilder.Entity<Product>()
                .HasMany(p => p.Images)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductID)
                .OnDelete(DeleteBehavior.Cascade); // Borra las imágenes si se borra el producto

            // --- Filtro de Consulta Global para Borrado Lógico (Soft Delete) ---
            // El filtro se aplica a la entidad raíz 'Client' y se hereda a 'NaturalPerson' y 'LegalEntity'.
            // modelBuilder.Entity<Client>().HasQueryFilter(c => c.IsActive); // Eliminado para mostrar todos los clientes
            modelBuilder.Entity<Purchase>().HasQueryFilter(p => !p.IsDeleted);
            // modelBuilder.Entity<Bank>().HasQueryFilter(b => b.IsActive); // Eliminado para mostrar todos los bancos
            modelBuilder.Entity<Sale>().HasQueryFilter(s => !s.IsDeleted);

            modelBuilder.Entity<Sale>()
                .HasMany(s => s.Details)
                .WithOne(d => d.Sale)
                .HasForeignKey(d => d.SaleID);

            modelBuilder.Entity<Invoice>()
                .HasMany(i => i.Payments)
                .WithOne(p => p.Invoice)
                .HasForeignKey(p => p.InvoiceID);

            // Configuración para CashRegister y CashMovement
            modelBuilder.Entity<CashRegister>()
                .HasMany(cr => cr.CashMovements)
                .WithOne(cm => cm.CashRegister)
                .HasForeignKey(cm => cm.CashRegisterID);

        }
    }
}
