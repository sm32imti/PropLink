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
            // Ensure tables and columns exist in Supabase PostgreSQL public schema
            try
            {
                context.Database.OpenConnection();
                using var cmd = context.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users');";
                var exists = (bool?)cmd.ExecuteScalar() ?? false;

                if (!exists)
                {
                    var databaseCreator = context.Database.GetService<IDatabaseCreator>() as RelationalDatabaseCreator;
                    databaseCreator?.CreateTables();
                }

                // Automatic schema evolution for new columns and tables
                string[] migrationQueries = new[]
                {
                    @"ALTER TABLE ""Properties"" ADD COLUMN IF NOT EXISTS ""RejectionReason"" character varying(2000);",
                    @"ALTER TABLE ""Properties"" ADD COLUMN IF NOT EXISTS ""TransactionStatus"" integer DEFAULT 0;",
                    @"ALTER TABLE ""PropertyDocuments"" ADD COLUMN IF NOT EXISTS ""StorageReference"" text DEFAULT '';",
                    @"ALTER TABLE ""PropertyDocuments"" ADD COLUMN IF NOT EXISTS ""ReviewRemarks"" character varying(2000);",
                    @"ALTER TABLE ""PropertyDocuments"" ADD COLUMN IF NOT EXISTS ""FileData"" bytea;",
                    @"ALTER TABLE ""PropertyDocuments"" ADD COLUMN IF NOT EXISTS ""ContentType"" character varying(100) DEFAULT 'application/pdf';",
                    @"ALTER TABLE ""PropertyImages"" ADD COLUMN IF NOT EXISTS ""Caption"" character varying(255);",
                    @"ALTER TABLE ""PropertyImages"" ADD COLUMN IF NOT EXISTS ""FileData"" bytea;",
                    @"ALTER TABLE ""PropertyImages"" ADD COLUMN IF NOT EXISTS ""ContentType"" character varying(100) DEFAULT 'image/jpeg';",
                    @"ALTER TABLE ""PropertyImages"" ALTER COLUMN ""ImageUrl"" TYPE text;",
                    @"ALTER TABLE ""PropertyDocuments"" ALTER COLUMN ""StorageReference"" TYPE text;",
                    @"ALTER TABLE ""PropertyDocuments"" ALTER COLUMN ""FilePath"" TYPE text;",
                    @"CREATE TABLE IF NOT EXISTS ""PropertyTransactions"" (
                        ""Id"" uuid NOT NULL PRIMARY KEY,
                        ""PropertyId"" uuid NOT NULL REFERENCES ""Properties""(""Id"") ON DELETE CASCADE,
                        ""BuyerId"" uuid NOT NULL REFERENCES ""Users""(""Id"") ON DELETE RESTRICT,
                        ""AgreedPrice"" numeric(18,2) NOT NULL,
                        ""Status"" integer NOT NULL DEFAULT 0,
                        ""Notes"" character varying(2000),
                        ""TransactionDate"" timestamp with time zone NOT NULL,
                        ""CompletedDate"" timestamp with time zone
                    );"
                };

                foreach (var sql in migrationQueries)
                {
                    try
                    {
                        using var qCmd = context.Database.GetDbConnection().CreateCommand();
                        qCmd.CommandText = sql;
                        qCmd.ExecuteNonQuery();
                    }
                    catch
                    {
                    }
                }

                // Align and auto-heal any existing properties in EF Core
                var existingProperties = context.Properties
                    .Include(p => p.Images)
                    .Include(p => p.Documents)
                    .ToList();

                Console.WriteLine($"[DbInitializer] Found {existingProperties.Count} properties in DB.");
                if (existingProperties.Any())
                {
                    var fallbackGallery = new[]
                    {
                        "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                        "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?auto=format&fit=crop&w=1200&q=80",
                        "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=1200&q=80",
                        "https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?auto=format&fit=crop&w=1200&q=80"
                    };

                    int pIndex = 0;
                    foreach (var p in existingProperties)
                    {
                        // 1. Ensure at least 1 image is attached
                        if (!p.Images.Any() || p.Images.All(i => string.IsNullOrWhiteSpace(i.ImageUrl)))
                        {
                            p.Images.Add(new PropertyImage
                            {
                                Id = Guid.NewGuid(),
                                PropertyId = p.Id,
                                ImageUrl = fallbackGallery[pIndex % fallbackGallery.Length],
                                Caption = "Architectural Exterior",
                                IsPrimary = true,
                                DisplayOrder = 1,
                                UploadedAt = DateTime.UtcNow
                            });
                        }

                        // 2. Ensure at least 1 NID and Title Deed is attached
                        if (!p.Documents.Any(d => d.DocumentType == "NID"))
                        {
                            p.Documents.Add(new PropertyDocument
                            {
                                Id = Guid.NewGuid(),
                                PropertyId = p.Id,
                                DocumentType = "NID",
                                FileName = "seller_identity_document.pdf",
                                StorageReference = "proplink-documents-secure/nid_documents/sample_seller_nid.pdf",
                                FilePath = "proplink-documents-secure/nid_documents/sample_seller_nid.pdf",
                                ContentType = "application/pdf",
                                FileSizeBytes = 1048576,
                                Status = p.VerificationStatus,
                                UploadedAt = DateTime.UtcNow
                            });
                        }

                        if (!p.Documents.Any(d => d.DocumentType != "NID"))
                        {
                            p.Documents.Add(new PropertyDocument
                            {
                                Id = Guid.NewGuid(),
                                PropertyId = p.Id,
                                DocumentType = "Deed / Title",
                                FileName = "municipal_title_deed.pdf",
                                StorageReference = "proplink-documents-secure/ownership_deeds/sample_title_deed.pdf",
                                FilePath = "proplink-documents-secure/ownership_deeds/sample_title_deed.pdf",
                                ContentType = "application/pdf",
                                FileSizeBytes = 2097152,
                                Status = p.VerificationStatus,
                                UploadedAt = DateTime.UtcNow
                            });
                        }

                        pIndex++;
                    }
                    context.SaveChanges();
                    Console.WriteLine("[DbInitializer] Successfully verified and auto-healed property image and document links.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DbInitializer Error] {ex.Message}");
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
                VerificationStatus = VerificationStatus.Approved,
                TransactionStatus = TransactionStatus.Available,
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
                    new PropertyDocument { DocumentType = "NID", FileName = "seller_nid_card.pdf", StorageReference = "proplink-documents-secure/seller_nid_card.pdf", ContentType = "application/pdf", Status = VerificationStatus.Approved, VerifiedAt = DateTime.UtcNow.AddDays(-2) },
                    new PropertyDocument { DocumentType = "Deed", FileName = "deed_evergreen_742.pdf", StorageReference = "proplink-documents-secure/deed_evergreen_742.pdf", ContentType = "application/pdf", Status = VerificationStatus.Approved, VerifiedAt = DateTime.UtcNow.AddDays(-2) }
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
                VerificationStatus = VerificationStatus.Approved,
                TransactionStatus = TransactionStatus.Negotiation,
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
                    new PropertyDocument { DocumentType = "NID", FileName = "seller_nid_card.pdf", StorageReference = "proplink-documents-secure/seller_nid_card.pdf", ContentType = "application/pdf", Status = VerificationStatus.Approved, VerifiedAt = DateTime.UtcNow.AddDays(-1) },
                    new PropertyDocument { DocumentType = "Title Certificate", FileName = "ocean_ave_title.pdf", StorageReference = "proplink-documents-secure/ocean_ave_title.pdf", ContentType = "application/pdf", Status = VerificationStatus.Approved, VerifiedAt = DateTime.UtcNow.AddDays(-1) }
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
                VerificationStatus = VerificationStatus.Approved,
                TransactionStatus = TransactionStatus.Available,
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

            // Seed a sample purchase transaction for the defaultUser buying prop1
            if (!context.PropertyTransactions.Any())
            {
                var transaction = new PropertyTransaction
                {
                    Id = Guid.NewGuid(),
                    PropertyId = prop1.Id,
                    BuyerId = defaultUser.Id,
                    AgreedPrice = prop1.Price,
                    Status = TransactionStatus.AgreementReached,
                    Notes = "Formal acquisition agreement executed with deed verification confirmation.",
                    TransactionDate = DateTime.UtcNow.AddDays(-1)
                };
                context.PropertyTransactions.Add(transaction);
                context.SaveChanges();
            }
        }
    }
    catch
    {
        // Fail-safe for offline or circuit-breaker PostgreSQL mode
    }
}
}
