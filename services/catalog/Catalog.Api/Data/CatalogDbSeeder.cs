using Catalog.Api.Models;

namespace Catalog.Api.Data;

public static class CatalogDbSeeder
{
    public static void Seed(CatalogDbContext db)
    {
        db.Database.EnsureCreated();

        if (db.Products.Any())
        {
            return;
        }

        db.Products.AddRange(
            new Product(Guid.NewGuid(), "Teclado mecánico", "Switches rojos, retroiluminado", 79.99m, 25),
            new Product(Guid.NewGuid(), "Monitor 27\" 4K", "Panel IPS, 144Hz", 349.00m, 12),
            new Product(Guid.NewGuid(), "Mouse inalámbrico", "Sensor óptico 16000 DPI", 29.90m, 50)
        );

        db.SaveChanges();
    }
}
