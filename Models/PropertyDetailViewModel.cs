using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class PropertyDetailViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice => Price.ToString("C0");
    public PropertyType PropertyType { get; set; }
    
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double SquareFeet { get; set; }

    public VerificationStatus VerificationStatus { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    public DateTime CreatedAt { get; set; }

    // Public Image Gallery
    public List<string> ImageUrls { get; set; } = new();

    // Public Seller Information (Safe fields only)
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;
    public string SellerPhone { get; set; } = string.Empty;
    public DateTime SellerMemberSince { get; set; }
    public int SellerTotalProperties { get; set; }
    public bool IsOwner { get; set; }

    // Auction & Bidding State
    public AuctionDetailViewModel? ActiveAuction { get; set; }
    public bool HasPendingBiddingRequest { get; set; }
    public bool CanRequestBidding => IsOwner && VerificationStatus == VerificationStatus.Approved && ActiveAuction == null && !HasPendingBiddingRequest;
}
