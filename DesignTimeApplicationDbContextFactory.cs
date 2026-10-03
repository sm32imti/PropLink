using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PropLink.Infrastructure.Data;

namespace PropLink.Web;

public sealed class DesignTimeApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        DotNetEnv.Env.TraversePath().Load();

        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var dbHost = configuration["DB_HOST"] ?? Environment.GetEnvironmentVariable("DB_HOST");

        if (!string.IsNullOrWhiteSpace(dbHost))
        {
            var dbPort = configuration["DB_PORT"] ?? Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
            var dbName = configuration["DB_NAME"] ?? Environment.GetEnvironmentVariable("DB_NAME") ?? "postgres";
            var dbUser = configuration["DB_USER"] ?? Environment.GetEnvironmentVariable("DB_USER") ?? string.Empty;
            var dbPassword = configuration["DB_PASSWORD"] ?? Environment.GetEnvironmentVariable("DB_PASSWORD") ?? string.Empty;
            var dbSslMode = configuration["DB_SSLMODE"] ?? Environment.GetEnvironmentVariable("DB_SSLMODE") ?? "Require";

            connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};SSL Mode={dbSslMode};Trust Server Certificate=true;Pooling=true;Keepalive=30;";
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Provide ConnectionStrings__DefaultConnection or DB_HOST, DB_USER, and DB_PASSWORD for EF tooling.");
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
