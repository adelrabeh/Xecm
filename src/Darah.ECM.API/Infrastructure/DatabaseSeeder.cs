using Darah.ECM.Domain.Entities;
using Darah.ECM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Darah.ECM.API.Infrastructure;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcmDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EcmDbContext>>();

        try
        {
            if (db.Database.IsRelational())
                await db.Database.MigrateAsync();

            await SeedAdminAsync(db, logger);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Seeding skipped: {Msg}", ex.Message);
        }
    }

    private static async Task SeedAdminAsync(EcmDbContext db, ILogger logger)
    {
        if (await db.Users.AnyAsync(u => u.Username == "admin"))
            return;

        var hash = HashPassword("Admin@2026");
        var admin = User.Create("admin", "admin@darah.gov.sa", hash,
            "مدير النظام", createdBy: 0, fullNameEn: "System Admin");

        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("✅ Admin user seeded — username: admin, password: Admin@2026");
    }

    public static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }
}
