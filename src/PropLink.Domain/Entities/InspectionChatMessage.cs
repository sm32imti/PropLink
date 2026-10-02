using System.ComponentModel.DataAnnotations;

namespace PropLink.Domain.Entities;

public class InspectionChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }
    public InspectionBooking? Booking { get; set; }

    public Guid SenderId { get; set; }
    public User? Sender { get; set; }

    [MaxLength(150)]
    public string SenderName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SenderRole { get; set; } = string.Empty; // "Buyer" or "Seller"

    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}
