using System.ComponentModel.DataAnnotations;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class ProfileViewModel
{
    // Personal Information
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NidNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTime MemberSince { get; set; }

    // Form Model for Profile Editing
    public EditProfileViewModel EditProfile { get; set; } = new();

    // Statistics
    public int TotalListedProperties { get; set; }
    public int VerifiedListingsCount { get; set; }
    public int PendingListingsCount { get; set; }
    public int RejectedListingsCount { get; set; }
    public int TotalPurchasesCount { get; set; }
    public int ActiveAuctionsCount { get; set; }
    public int MyBidsCount { get; set; }

    // Selling / Listing History (Properties created by authenticated user)
    public List<MyPropertyListingViewModel> SellingHistory { get; set; } = new();

    // Buying History (Transactions / purchases made by authenticated user)
    public List<PropertyTransactionViewModel> BuyingHistory { get; set; } = new();

    // Seller Auctions (Auctions created on properties owned by authenticated user)
    public List<SellerAuctionItemViewModel> SellerAuctions { get; set; } = new();

    // Buyer Bids (Bids placed by authenticated user)
    public List<BuyerBidItemViewModel> BuyerBids { get; set; } = new();
}

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(30, ErrorMessage = "Phone Number cannot exceed 30 characters.")]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "NID Card Number cannot exceed 50 characters.")]
    [Display(Name = "NID Card Number")]
    public string NidNumber { get; set; } = string.Empty;
}

public class MyPropertyListingViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice => Price.ToString("C0");
    public PropertyType PropertyType { get; set; }
    public string Location { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double SquareFeet { get; set; }
    public string MainImageUrl { get; set; } = string.Empty;
    public VerificationStatus VerificationStatus { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FormattedDate => CreatedAt.ToString("MMM dd, yyyy");

    // Auction State
    public bool HasActiveAuction { get; set; }
    public AuctionStatus? AuctionStatus { get; set; }
    public Guid? ActiveAuctionId { get; set; }
    public decimal? CurrentHighestBid { get; set; }
    public string? FormattedHighestBid => CurrentHighestBid?.ToString("C0");
    public bool HasPendingBiddingRequest { get; set; }
    public bool CanRequestBidding => VerificationStatus == VerificationStatus.Approved && !HasActiveAuction && !HasPendingBiddingRequest && TransactionStatus == TransactionStatus.Available;
}

public class PropertyTransactionViewModel
{
    public Guid TransactionId { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public decimal AgreedPrice { get; set; }
    public string FormattedAgreedPrice => AgreedPrice.ToString("C0");
    public string Location { get; set; } = string.Empty;
    public TransactionStatus TransactionStatus { get; set; }
    public string? Notes { get; set; }
    public DateTime TransactionDate { get; set; }
    public string FormattedDate => TransactionDate.ToString("MMM dd, yyyy");
}
