using System.ComponentModel.DataAnnotations;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class RequestBiddingInputModel
{
    [Required]
    public Guid PropertyId { get; set; }

    [Required(ErrorMessage = "Start price is required.")]
    [Range(1, 1000000000, ErrorMessage = "Start price must be greater than $0.")]
    [Display(Name = "Auction Start Price ($)")]
    public decimal StartPrice { get; set; }

    [Required(ErrorMessage = "Minimum bid increment is required.")]
    [Range(1, 1000000, ErrorMessage = "Minimum increment must be at least $1.")]
    [Display(Name = "Minimum Bid Increment ($)")]
    public decimal MinIncrement { get; set; } = 1000;

    [Required(ErrorMessage = "Please choose an auction duration.")]
    [Range(1, 168, ErrorMessage = "Duration must be between 1 and 168 hours.")]
    [Display(Name = "Auction Duration (Hours)")]
    public int DurationHours { get; set; } = 24;
}

public class PlaceBidInputModel
{
    [Required]
    public Guid PropertyId { get; set; }

    [Required]
    public Guid AuctionId { get; set; }

    [Required(ErrorMessage = "Please enter your bid amount.")]
    [Range(1, 1000000000, ErrorMessage = "Bid amount must be valid.")]
    [Display(Name = "Bid Amount ($)")]
    public decimal Amount { get; set; }
}

public class AuctionDetailViewModel
{
    public Guid AuctionId { get; set; }
    public Guid PropertyId { get; set; }
    public decimal StartPrice { get; set; }
    public string FormattedStartPrice => StartPrice.ToString("C0");
    public decimal MinIncrement { get; set; }
    public string FormattedMinIncrement => MinIncrement.ToString("C0");

    public decimal? CurrentHighestBid { get; set; }
    public string FormattedCurrentHighestBid => CurrentHighestBid.HasValue ? CurrentHighestBid.Value.ToString("C0") : FormattedStartPrice;
    public decimal NextMinimumBid => (CurrentHighestBid.HasValue ? CurrentHighestBid.Value + MinIncrement : StartPrice);
    public string FormattedNextMinimumBid => NextMinimumBid.ToString("C0");

    public Guid? HighestBidderId { get; set; }
    public string? HighestBidderName { get; set; }
    public bool IsViewerHighestBidder { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AuctionStatus Status { get; set; }
    public int TotalBidsCount { get; set; }

    public bool IsActive => Status == AuctionStatus.Active && DateTime.UtcNow < EndTime;
    public bool IsAwaitingConfirmation => Status == AuctionStatus.AwaitingSellerConfirmation;
    public bool IsEnded => DateTime.UtcNow >= EndTime || Status != AuctionStatus.Active;

    public List<BidHistoryItemViewModel> BidHistory { get; set; } = new();
}

public class BidHistoryItemViewModel
{
    public Guid BidId { get; set; }
    public string BidderDisplay { get; set; } = "Bidder";
    public decimal Amount { get; set; }
    public string FormattedAmount => Amount.ToString("C0");
    public DateTime PlacedAt { get; set; }
    public string FormattedPlacedAt => PlacedAt.ToString("MMM dd, yyyy HH:mm:ss");
    public bool IsFromDirectOffer { get; set; }
    public bool IsViewer { get; set; }
}

public class AdminBiddingDashboardViewModel
{
    public List<AdminBiddingRequestItemViewModel> PendingRequests { get; set; } = new();
    public int TotalPendingCount { get; set; }
    public int TotalApprovedCount { get; set; }
    public int TotalRejectedCount { get; set; }
}

public class AdminBiddingRequestItemViewModel
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public PropertyType PropertyType { get; set; }
    public string Location { get; set; } = string.Empty;

    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;
    public string SellerPhone { get; set; } = string.Empty;

    public decimal StartPrice { get; set; }
    public string FormattedStartPrice => StartPrice.ToString("C0");
    public decimal MinIncrement { get; set; }
    public string FormattedMinIncrement => MinIncrement.ToString("C0");
    public int DurationHours { get; set; }

    public DateTime RequestedAt { get; set; }
    public BiddingRequestStatus Status { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class RejectBiddingRequestModel
{
    [Required]
    public Guid RequestId { get; set; }

    [Required(ErrorMessage = "Please specify a rejection reason for the seller.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Rejection note must be between 5 and 1000 characters.")]
    public string AdminNote { get; set; } = string.Empty;
}

public class SellerAuctionItemViewModel
{
    public Guid AuctionId { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public PropertyType PropertyType { get; set; }
    public string Location { get; set; } = string.Empty;

    public decimal StartPrice { get; set; }
    public string FormattedStartPrice => StartPrice.ToString("C0");
    public decimal? CurrentHighestBid { get; set; }
    public string FormattedCurrentHighestBid => (CurrentHighestBid ?? StartPrice).ToString("C0");

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AuctionStatus Status { get; set; }
    public int TotalBids { get; set; }

    public Guid? WinningBidId { get; set; }
    public decimal? WinningBidAmount { get; set; }
    public string? FormattedWinningBidAmount => WinningBidAmount?.ToString("C0");
    public string? WinningBuyerName { get; set; }
    public string? WinningBuyerEmail { get; set; }
    public string? WinningBuyerPhone { get; set; }

    public DateTime? SellerDecisionAt { get; set; }
    public string? SellerDecisionNotes { get; set; }
}

public class BuyerBidItemViewModel
{
    public Guid BidId { get; set; }
    public Guid AuctionId { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    public decimal YourLatestBid { get; set; }
    public string FormattedYourLatestBid => YourLatestBid.ToString("C0");

    public decimal CurrentHighestBid { get; set; }
    public string FormattedCurrentHighestBid => CurrentHighestBid.ToString("C0");

    public DateTime PlacedAt { get; set; }
    public string FormattedPlacedAt => PlacedAt.ToString("MMM dd, yyyy");

    public DateTime EndTime { get; set; }
    public AuctionStatus AuctionStatus { get; set; }

    // Position: "Winning", "Outbid", "Won", "Lost"
    public string BidPosition { get; set; } = "Winning";
}
