using Microsoft.EntityFrameworkCore;
using PropLink.Domain.Entities;

namespace PropLink.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<PropertyDocument> PropertyDocuments => Set<PropertyDocument>();
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<PropertyTransaction> PropertyTransactions => Set<PropertyTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.Property(u => u.PhoneNumber).HasMaxLength(30);
            entity.Property(u => u.NidNumber).HasMaxLength(50).IsRequired(false);
        });

        // Configure Property entity
        modelBuilder.Entity<Property>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Title).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.Address).HasMaxLength(250);
            entity.Property(p => p.City).HasMaxLength(100);
            entity.Property(p => p.State).HasMaxLength(100);
            entity.Property(p => p.ZipCode).HasMaxLength(20);
            entity.Property(p => p.RejectionReason).HasMaxLength(2000);
            entity.Property(p => p.AdminReviewNotes).HasMaxLength(2000);

            entity.HasOne(p => p.Seller)
                .WithMany(u => u.Properties)
                .HasForeignKey(p => p.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure PropertyDocument
        modelBuilder.Entity<PropertyDocument>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DocumentType).IsRequired().HasMaxLength(100);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(255);
            entity.Property(d => d.ContentType).HasMaxLength(100);
            entity.Property(d => d.StorageReference);
            entity.Property(d => d.FilePath);
            entity.Property(d => d.ReviewRemarks).HasMaxLength(2000);

            entity.HasOne(d => d.Property)
                .WithMany(p => p.Documents)
                .HasForeignKey(d => d.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure PropertyImage
        modelBuilder.Entity<PropertyImage>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.ImageUrl).IsRequired();
            entity.Property(i => i.ContentType).HasMaxLength(100);
            entity.Property(i => i.Caption).HasMaxLength(255);

            entity.HasOne(i => i.Property)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Inquiry
        modelBuilder.Entity<Inquiry>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Message).IsRequired().HasMaxLength(2000);

            entity.HasOne(i => i.Property)
                .WithMany(p => p.Inquiries)
                .HasForeignKey(i => i.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.Buyer)
                .WithMany(u => u.Inquiries)
                .HasForeignKey(i => i.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure PropertyTransaction (Buying History & Negotiations)
        modelBuilder.Entity<PropertyTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.AgreedPrice).HasPrecision(18, 2);
            entity.Property(t => t.Notes).HasMaxLength(2000);

            entity.HasOne(t => t.Property)
                .WithMany(p => p.Transactions)
                .HasForeignKey(t => t.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Buyer)
                .WithMany(u => u.Purchases)
                .HasForeignKey(t => t.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
