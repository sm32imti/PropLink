using Microsoft.EntityFrameworkCore;
using PropLink.Infrastructure.Data;

// Load environment variables from .env file if present (useful for local development)
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// Database Configuration (Remote Supabase PostgreSQL)
// ==============================================================================
// 1. Check for full connection string in Configuration (appsettings or ConnectionStrings__DefaultConnection env var)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. Check for individual DB environment variables (DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD, DB_SSLMODE)
var dbHost = builder.Configuration["DB_HOST"] ?? Environment.GetEnvironmentVariable("DB_HOST");
if (!string.IsNullOrWhiteSpace(dbHost))
{
    var dbPort = builder.Configuration["DB_PORT"] ?? Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
    var dbName = builder.Configuration["DB_NAME"] ?? Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
    var dbUser = builder.Configuration["DB_USER"] ?? Environment.GetEnvironmentVariable("DB_USER") ?? "";
    var dbPass = builder.Configuration["DB_PASSWORD"] ?? Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
    var dbSsl = builder.Configuration["DB_SSLMODE"] ?? Environment.GetEnvironmentVariable("DB_SSLMODE") ?? "Require";

    // Supabase requires SSL Mode=Require and Trust Server Certificate=true.
    // Pooling=true and Keepalive=30 prevent idle connection dropouts when routed through Supabase Pooler.
    connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode={dbSsl};Trust Server Certificate=true;Pooling=true;Keepalive=30;";
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL connection string was not found. Please provide 'ConnectionStrings__DefaultConnection' or set DB_HOST, DB_USER, DB_PASSWORD in environment variables.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ==============================================================================
// Authentication & Storage Services
// ==============================================================================
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddHttpClient();
builder.Services.AddScoped<PropLink.Application.Common.Interfaces.ICloudStorageService, PropLink.Infrastructure.Services.CloudStorageService>();
builder.Services.AddHostedService<PropLink.Infrastructure.Services.AuctionExpiryBackgroundService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ==============================================================================
// Database Migrations & Seeding Strategy
// ------------------------------------------------------------------------------
// TRADE-OFF DISCUSSION:
// - Automatic Startup Initialization (DbInitializer / context.Database.Migrate()):
//   * PROS: Zero-touch deployment. Automatically sets up schemas and seeds initial 
//     admin/user data when the container spins up against a remote Supabase instance.
//   * CONS: If running multiple container replicas behind a load balancer, concurrent 
//     startup migrations may cause database locking or race conditions.
// - Manual Migrations (dotnet ef database update via CI/CD):
//   * PROS: Fully deterministic, isolated from container startup lifecycle.
//   * CONS: Requires EF Migration tooling / separate CI pipeline stage.
//
// For PropLink, we use automatic startup initialization (DbInitializer.Initialize)
// which safely applies idempotent table/column checks and seeds required seed data.
// ==============================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing/seeding the Supabase database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. See https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
