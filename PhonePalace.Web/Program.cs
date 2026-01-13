using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Infrastructure.Services;
using System.Globalization;
using PhonePalace.Web.Areas.Identity.Data;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = false) // Facilitar login en desarrollo
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

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

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    })
    .AddSessionStateTempDataProvider(); // Configura TempData para que use el estado de sesión en lugar de cookies.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<ICashService, CashService>();

builder.Services.AddTransient<IEmailSender, EmailService>();
builder.Services.AddRazorPages();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Configuración de la licencia de QuestPDF
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// --- INICIO: Configuración de Cultura para Colombia ---
var cultureInfo = new CultureInfo("es-CO");

// Clonamos el formato de número para poder modificarlo. La cultura original 'es-CO' usa ',' para decimales.
var numberFormat = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();

// ESTA ES LA LÍNEA CLAVE: Cambiamos el separador decimal a '.' para el PARSEO de números.
// Esto es fundamental para que el Model Binding de ASP.NET Core pueda entender los valores
// que envían los formularios web y JavaScript, que usan '.' por estándar internacional.
numberFormat.NumberDecimalSeparator = ".";

// Asignamos el formato modificado a nuestra cultura personalizada.
cultureInfo.NumberFormat = numberFormat;

// La UI (fechas, formato de moneda con ToString("C")) seguirá usando las reglas de 'es-CO',
// pero el backend ahora entenderá los números de los formularios.
var supportedCultures = new[] { cultureInfo };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(cultureInfo),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});
// --- FIN: Configuración de Cultura para Colombia ---

// --- Ejecutar Seeders ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        // 1. Poblar roles y usuario administrador
        await DbSeeder.SeedRolesAndAdminAsync(services);
        logger.LogInformation("Roles and admin user seeded successfully.");

        // 2. Poblar datos de Departamentos y Municipios
        var context = services.GetRequiredService<ApplicationDbContext>();
        await DataSeeder.SeedDaneDataAsync(context);
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
