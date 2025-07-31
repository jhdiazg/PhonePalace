using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Infrastructure.Services;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false) // Facilitar login en desarrollo
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// --- INICIO: Configuración de Sesión ---
// Es necesario para que servicios como AuditService puedan acceder a datos de la sesión del usuario.
builder.Services.AddDistributedMemoryCache(); // Requerido para el estado de sesión en memoria.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Tiempo de inactividad antes de que la sesión expire.
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Marcar la cookie de sesión como esencial para el funcionamiento.
});
// --- FIN: Configuración de Sesión ---

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

builder.Services.AddTransient<EmailService>();

var app = builder.Build();

// --- INICIO: Configuración de Cultura para Colombia ---
// Se clona la cultura 'es-CO' para poder modificarla.
var cultureInfo = new CultureInfo("es-CO");
// Se establece el punto '.' como separador decimal para la validación y el model binding.
// Esto es crucial para que los formularios que envían números desde JavaScript (que usan '.') funcionen correctamente,
// sin afectar el formato de moneda (que seguirá usando los símbolos y separadores de miles de 'es-CO').
cultureInfo.NumberFormat.NumberDecimalSeparator = ".";

var supportedCultures = new[] { cultureInfo };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultureInfo),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});
// --- FIN: Configuración de Cultura ---

// --- Ejecutar Seeders ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        // 1. Poblar roles y usuario administrador
        await PhonePalace.Infrastructure.Data.DbSeeder.SeedRolesAndAdminAsync(services);
        logger.LogInformation("Roles and admin user seeded successfully.");

        // 2. Poblar datos de Departamentos y Municipios
        var context = services.GetRequiredService<ApplicationDbContext>();
        await PhonePalace.Infrastructure.Data.DataSeeder.SeedDaneDataAsync(context);
        logger.LogInformation("DANE data seeded successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the DB.");
    }
}
// --- Fin de los Seeders ---

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Correcto para servir archivos estáticos

app.UseRouting();

// --- INICIO: Habilitar Middleware de Sesión ---
// Debe ir después de UseRouting y antes de UseAuthorization.
app.UseSession();
// --- FIN: Habilitar Middleware de Sesión ---

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
