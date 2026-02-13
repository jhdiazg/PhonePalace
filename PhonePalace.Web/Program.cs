using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using PhonePalace.Domain.Interfaces;
using PhonePalace.Infrastructure.Data;
using PhonePalace.Infrastructure.Services;
using PhonePalace.Infrastructure.Configuration;
using System.Globalization;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Configuración híbrida: Prioriza variable de entorno (Prod) sobre appsettings (Dev)
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
                       ?? builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();
}

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        // Agregamos el espacio " " al final de la cadena de caracteres permitidos
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ ";
    })
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

// Configuración de Plemsi API
builder.Services.Configure<PlemsiConfig>(builder.Configuration.GetSection("Plemsi"));
builder.Services.Configure<CompanySettings>(builder.Configuration.GetSection("CompanySettings"));

// Registro del Servicio Plemsi con HttpClient tipado
builder.Services.AddHttpClient<IPlemsiService, PlemsiService>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IOptions<PlemsiConfig>>().Value;
    client.BaseAddress = new Uri(config.BaseUrl);
    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.Token);
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    })
    .AddSessionStateTempDataProvider(); // Configura TempData para que use el estado de sesión en lugar de cookies.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();

// Configuración de almacenamiento para VPS
// Se utiliza FileStorageService tanto en desarrollo como en producción para guardar
// las imágenes localmente en el servidor (wwwroot), asegurando que sean visibles en el VPS.
builder.Services.AddScoped<IFileStorageService, FileStorageService>();

builder.Services.AddScoped<ICashService, CashService>();
builder.Services.AddScoped<IBankService, BankService>();

builder.Services.AddTransient<IEmailSender, EmailService>();
builder.Services.AddRazorPages();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";

    // --- INICIO: Configuración de Expiración de Sesión de Autenticación ---
    // Forzar el cierre de sesión después de un tiempo fijo, sin importar la actividad del usuario.
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // La sesión expira después de 60 minutos.
    options.SlidingExpiration = false; // Impide que la cookie se renueve con cada petición.
});

// Plemsi
builder.Services.Configure<PhonePalace.Infrastructure.Configuration.PlemsiConfig>(
    builder.Configuration.GetSection("Plemsi"));


// Configuración de la licencia de QuestPDF
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// --- INICIO: Configuración de Cultura para Colombia ---
var cultureInfo = new CultureInfo("es-CO");

// Clonamos el formato de número para poder modificarlo. La cultura original 'es-CO' usa ',' para decimales.
var numberFormat = (NumberFormatInfo)cultureInfo.NumberFormat.Clone();

// Configuración personalizada de formatos:
// 1. Separador de miles: Coma (",")
numberFormat.NumberGroupSeparator = ",";
numberFormat.CurrencyGroupSeparator = ",";
// 2. Separador decimal: Punto (".") para compatibilidad técnica
numberFormat.NumberDecimalSeparator = ".";
numberFormat.CurrencyDecimalSeparator = ".";
// 3. Moneda sin decimales
numberFormat.CurrencyDecimalDigits = 0;

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
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        // 1. Poblar roles y usuario administrador
        await DbSeeder.SeedRolesAndAdminAsync(services);
        logger.LogInformation("Roles and admin user seeded successfully.");

        // 2. Poblar datos de Departamentos y Municipios
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
