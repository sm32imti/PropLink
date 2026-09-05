namespace PropLink.Domain.Entities;

public class Bid
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AuctionId { get; set; }
    public Auction? Auction { get; set; }

    public Guid BuyerId { get; set; }
    public User? Buyer { get; set; }

    public decimal Amount { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public bool IsFromDirectOffer { get; set; } = false;
}
