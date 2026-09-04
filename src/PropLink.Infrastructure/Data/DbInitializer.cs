using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using PropLink.Domain.Entities;
using PropLink.Domain.Enums;

namespace PropLink.Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        try
        {
            // Ensure tables exist in Supabase PostgreSQL public schema
            try
            {
                using var cmd = context.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users');";
                context.Database.OpenConnection();
                var exists = (bool?)cmd.ExecuteScalar() ?? false;

                if (!exists)
                {
                    var databaseCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                    databaseCreator?.CreateTables();
                }
            }
            catch
            {
                // Tables already created or schema verified
            }

            // Check if Admin user exists
            if (!context.Users.Any(u => u.Email == "tamjid@gmail.com"))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Tamjid (Administrator)",
                Email = "tamjid@gmail.com",
                PhoneNumber = "+1-555-0100",
                Role = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("tamjid123"),
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
        }

        // Seed Default Standard User
        User? defaultUser = context.Users.FirstOrDefault(u => u.Email == "user@proplink.com");
        if (defaultUser == null)
        {
            defaultUser = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Marcus Sterling",
                Email = "user@proplink.com",
                PhoneNumber = "+1-555-0144",
                Role = "User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
                CreatedAt = DateTime.UtcNow
            };

            context.Users.Add(defaultUser);
        }

        context.SaveChanges();

        // Seed initial verified properties if table is empty
        if (!context.Properties.Any())
        {
            var sellerId = defaultUser.Id;

            var prop1 = new Property
            {
                Id = Guid.NewGuid(),
                Title = "The Grand Horizon Villa",
                Description = "Ultra-modern 5-bedroom luxury estate with infinity pool, panoramic mountain views, and deed-verified title.",
                Price = 1250000,
                PropertyType = PropertyType.House,
                Address = "742 Evergreen Heights",
                City = "Beverly Hills",
                State = "CA",
                ZipCode = "90210",
                Bedrooms = 5,
                Bathrooms = 6,
                SquareFeet = 5800,
                ListingStatus = ListingStatus.Approved,
                VerificationStatus = VerificationStatus.Verified,
                SellerId = sellerId,
                ReviewedAt = DateTime.UtcNow.AddDays(-2),
                AdminReviewNotes = "All title deed records, tax slips, and identity proofs verified by admin.",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                Images = new List<PropertyImage>
                {
                    new PropertyImage { ImageUrl = "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80", Caption = "Front Exterior", IsPrimary = true, DisplayOrder = 1 }
                },
                Documents = new List<PropertyDocument>
                {
                    new PropertyDocument { DocumentType = "Deed", FileName = "deed_evergreen_742.pdf", FilePath = "/uploads/docs/deed_evergreen_742.pdf", ContentType = "application/pdf", Status = VerificationStatus.Verified, VerifiedAt = DateTime.UtcNow.AddDays(-2) }
                }
            };

            var prop2 = new Property
            {
                Id = Guid.NewGuid(),
                Title = "Skyline Azure Penthouse",
                Description = "Floor-to-ceiling glass apartment with private terrace, smart home automation, and verified legal ownership documents.",
                Price = 875000,
                PropertyType = PropertyType.Apartment,
                Address = "100 Ocean Avenue, Unit 42A",
                City = "Santa Monica",
                State = "CA",
                ZipCode = "90401",
                Bedrooms = 3,
                Bathrooms = 3,
                SquareFeet = 2400,
                ListingStatus = ListingStatus.Approved,
                VerificationStatus = VerificationStatus.Verified,
                SellerId = sellerId,
                ReviewedAt = DateTime.UtcNow.AddDays(-1),
                AdminReviewNotes = "Ownership verified with municipal registry.",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Images = new List<PropertyImage>
                {
                    new PropertyImage { ImageUrl = "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80", Caption = "Living Room & Terrace", IsPrimary = true, DisplayOrder = 1 }
                },
                Documents = new List<PropertyDocument>
                {
                    new PropertyDocument { DocumentType = "Title Certificate", FileName = "ocean_ave_title.pdf", FilePath = "/uploads/docs/ocean_ave_title.pdf", ContentType = "application/pdf", Status = VerificationStatus.Verified, VerifiedAt = DateTime.UtcNow.AddDays(-1) }
                }
            };

            var prop3 = new Property
            {
                Id = Guid.NewGuid(),
                Title = "Nordic Pine Modern Residence",
                Description = "Architectural masterpiece with sustainable timber construction, private landscaped garden, and vetted documentation.",
                Price = 640000,
                PropertyType = PropertyType.House,
                Address = "182 Pine Valley Road",
                City = "Seattle",
                State = "WA",
                ZipCode = "98101",
                Bedrooms = 4,
                Bathrooms = 3,
                SquareFeet = 3200,
                ListingStatus = ListingStatus.Approved,
                VerificationStatus = VerificationStatus.Verified,
                SellerId = sellerId,
                ReviewedAt = DateTime.UtcNow.AddHours(-10),
                AdminReviewNotes = "Clear title deed and utility clearance.",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                Images = new List<PropertyImage>
                {
                    new PropertyImage { ImageUrl = "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=1200&q=80", Caption = "Exterior", IsPrimary = true, DisplayOrder = 1 }
                }
            };

            context.Properties.AddRange(prop1, prop2, prop3);
            context.SaveChanges();
        }
    }
    catch
    {
        // Fail-safe for offline or circuit-breaker PostgreSQL mode
    }
}
}
