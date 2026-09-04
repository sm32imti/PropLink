using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class PropertyTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }

    public Guid BuyerId { get; set; }
    public User? Buyer { get; set; }

    public decimal AgreedPrice { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Negotiation;
    public string? Notes { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedDate { get; set; }
}
