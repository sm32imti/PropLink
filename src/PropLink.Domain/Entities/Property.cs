using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class Property
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public PropertyType PropertyType { get; set; } = PropertyType.House;
    
    // Address Details
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    // Property Specs
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double SquareFeet { get; set; }

    // Statuses
    public ListingStatus ListingStatus { get; set; } = ListingStatus.Draft;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public TransactionStatus TransactionStatus { get; set; } = TransactionStatus.Available;
    public string? RejectionReason { get; set; }
    public string? AdminReviewNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Ownership & Timestamps
    public Guid SellerId { get; set; }
    public User? Seller { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Relations
    public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
    public ICollection<PropertyDocument> Documents { get; set; } = new List<PropertyDocument>();
    public ICollection<Inquiry> Inquiries { get; set; } = new List<Inquiry>();
    public ICollection<PropertyTransaction> Transactions { get; set; } = new List<PropertyTransaction>();
}
