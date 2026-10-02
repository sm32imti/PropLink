using System.ComponentModel.DataAnnotations;

namespace PropLink.Domain.Entities;

public class SubscriptionPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(100)]
    public string TransactionId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ValidationId { get; set; }

    [MaxLength(100)]
    public string? BankTranId { get; set; }

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "SSLCommerz"; // "bKash", "Nagad", "Visa", etc.

    [MaxLength(100)]
    public string? CardType { get; set; }

    public decimal Amount { get; set; } = 999.00m;

    [MaxLength(10)]
    public string Currency { get; set; } = "BDT";

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // "Pending", "Success", "Failed", "Cancelled"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime SubscriptionExpiresAt { get; set; }
}
