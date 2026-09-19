using System.ComponentModel.DataAnnotations;
using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class InspectionBooking
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }

    public Guid BuyerId { get; set; }
    public User? Buyer { get; set; }

    public Guid SellerId { get; set; }
    public User? Seller { get; set; }

    public Guid? AgentId { get; set; }
    public User? Agent { get; set; }
    public string? AgentName { get; set; }

    [MaxLength(50)]
    public string InspectionType { get; set; } = "DirectAskingPrice"; // "DirectAskingPrice" or "PriceOfferWithInspection"

    public decimal AskingPrice { get; set; }
    public decimal? OfferedPrice { get; set; }

    public DateTime PreferredDate { get; set; }
    public DateTime? ScheduledDate { get; set; }

    [MaxLength(500)]
    public string? MeetingLocationNotes { get; set; }

    public InspectionStatus Status { get; set; } = InspectionStatus.PendingSellerApproval;

    public bool AgreedToAntiBypassTerms { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
