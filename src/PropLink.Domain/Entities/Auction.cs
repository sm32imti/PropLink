using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class Auction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }

    public Guid? BiddingRequestId { get; set; }
    public BiddingRequest? BiddingRequest { get; set; }

    public decimal StartPrice { get; set; }
    public decimal MinIncrement { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime EndTime { get; set; } // Fixed, never extended
    public AuctionStatus Status { get; set; } = AuctionStatus.Active;

    public Guid? WinningBidId { get; set; }
    public Bid? WinningBid { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SellerDecisionAt { get; set; }
    public string? SellerDecisionNotes { get; set; }

    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
}
