using PropLink.Domain.Enums;

namespace PropLink.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? NidNumber { get; set; }
    public string Role { get; set; } = "User"; // "User" or "Admin"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Property> Properties { get; set; } = new List<Property>();
    public ICollection<PropertyTransaction> Purchases { get; set; } = new List<PropertyTransaction>();
    public ICollection<Inquiry> Inquiries { get; set; } = new List<Inquiry>();
}
