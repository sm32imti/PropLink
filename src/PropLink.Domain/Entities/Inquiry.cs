namespace PropLink.Domain.Entities;

public class Inquiry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Property? Property { get; set; }

    public Guid BuyerId { get; set; }
    public User? Buyer { get; set; }

    public string Message { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public bool IsReadBySeller { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
