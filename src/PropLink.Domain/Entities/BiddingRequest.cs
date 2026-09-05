using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class BiddingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }

    public Guid SellerId { get; set; }
    public User? Seller { get; set; }

    public decimal StartPrice { get; set; }
    public decimal MinIncrement { get; set; }
    public int DurationHours { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public BiddingRequestStatus Status { get; set; } = BiddingRequestStatus.Pending;

    public string? AdminNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public User? ReviewedByAdmin { get; set; }
}
