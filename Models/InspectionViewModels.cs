using System.ComponentModel.DataAnnotations;
using PropLink.Domain.Enums;

namespace PropLink.Web.Models;

public class InspectionRequestViewModel
{
    [Required]
    public Guid PropertyId { get; set; }

    public string PropertyTitle { get; set; } = string.Empty;
    public decimal AskingPrice { get; set; }

    public string InspectionType { get; set; } = "DirectAskingPrice"; // "DirectAskingPrice" or "PriceOfferWithInspection"

    public decimal? OfferedPrice { get; set; }

    [Required(ErrorMessage = "Please select a preferred inspection date")]
    [DataType(DataType.Date)]
    public DateTime PreferredDate { get; set; } = DateTime.Today.AddDays(1);

    [Required]
    public string PreferredTimeSlot { get; set; } = "Morning (10:00 AM - 1:00 PM)";

    public int AttendeesCount { get; set; } = 1;

    [MaxLength(500)]
    public string? BuyerNotes { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the PropLink In-Person Verification Protocol and Anti-Bypass Agreement.")]
    public bool AgreeToAntiBypassTerms { get; set; } = true;
}

public class AgentInspectionItemViewModel
{
    public Guid BookingId { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string PropertyCity { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public decimal AskingPrice { get; set; }
    public decimal? OfferedPrice { get; set; }
    public string InspectionType { get; set; } = "DirectAskingPrice";

    public Guid BuyerId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string BuyerPhone { get; set; } = string.Empty;

    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;
    public string SellerPhone { get; set; } = string.Empty;

    public Guid? AgentId { get; set; }
    public string? AgentName { get; set; }

    public DateTime PreferredDate { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? MeetingLocationNotes { get; set; }
    public InspectionStatus Status { get; set; }
    public string? BuyerNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AgentScheduleFixInputModel
{
    [Required]
    public Guid BookingId { get; set; }

    [Required(ErrorMessage = "Please select a confirmed inspection date and time")]
    public DateTime ScheduledDateTime { get; set; }

    [MaxLength(500)]
    public string? MeetingLocationNotes { get; set; }
}

public class BuyerInspectionItemViewModel
{
    public Guid BookingId { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string PropertyCity { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public decimal AskingPrice { get; set; }
    public decimal? OfferedPrice { get; set; }
    public string InspectionType { get; set; } = "DirectAskingPrice";
    public string SellerName { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public DateTime PreferredDate { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? MeetingLocationNotes { get; set; }
    public InspectionStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SellerPropertyQueueItemViewModel
{
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string PropertyCity { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public decimal AskingPrice { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    public int TotalRequestsCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int ForwardedToAgentCount { get; set; }
    public decimal? HighestOfferPrice { get; set; }
    public DateTime LatestRequestDate { get; set; }
}

public class SellerPropertyRequestsViewModel
{
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public string PropertyCity { get; set; } = string.Empty;
    public string PropertyImageUrl { get; set; } = string.Empty;
    public decimal AskingPrice { get; set; }
    public TransactionStatus TransactionStatus { get; set; }
    public List<SellerInspectionRequestItemViewModel> Requests { get; set; } = new();
}

public class SellerInspectionRequestItemViewModel
{
    public Guid BookingId { get; set; }
    public Guid BuyerId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string InspectionType { get; set; } = "DirectAskingPrice";
    public decimal AskingPrice { get; set; }
    public decimal? OfferedPrice { get; set; }
    public DateTime PreferredDate { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public string? MeetingLocationNotes { get; set; }
    public InspectionStatus Status { get; set; }
    public string? AgentName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool AgreedToAntiBypassTerms { get; set; }
}

