using Microsoft.EntityFrameworkCore;
using PropLink.Infrastructure.Data;

// Load environment variables from .env file if present
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var dbHost = builder.Configuration["DB_HOST"] ?? Environment.GetEnvironmentVariable("DB_HOST");
if (!string.IsNullOrWhiteSpace(dbHost))
{
    var dbPort = builder.Configuration["DB_PORT"] ?? Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
    var dbName = builder.Configuration["DB_NAME"] ?? Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
    var dbUser = builder.Configuration["DB_USER"] ?? Environment.GetEnvironmentVariable("DB_USER") ?? "";
    var dbPass = builder.Configuration["DB_PASSWORD"] ?? Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
    connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode=Require;Trust Server Certificate=true;";
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("PostgreSQL connection string was not found.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddHttpClient();
builder.Services.AddScoped<PropLink.Application.Common.Interfaces.ICloudStorageService, PropLink.Infrastructure.Services.CloudStorageService>();
builder.Services.AddHostedService<PropLink.Infrastructure.Services.AuctionExpiryBackgroundService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed Database (Admin, Users, Initial Verified Properties)
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
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
