using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PropLink.Web.Models;

public class RegisterViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Full name is required")]
    [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Password))
        {
            yield break;
        }

        if (Password.Length < 8)
        {
            yield return new ValidationResult("Password must be at least 8 characters long.", new[] { nameof(Password) });
        }

        if (!Regex.IsMatch(Password, @"[A-Z]"))
        {
            yield return new ValidationResult("Capital character needed: Password must contain at least one uppercase letter (A-Z).", new[] { nameof(Password) });
        }

        if (!Regex.IsMatch(Password, @"[0-9]"))
        {
            yield return new ValidationResult("Number needed: Password must contain at least one digit (0-9).", new[] { nameof(Password) });
        }

        if (!Regex.IsMatch(Password, @"[^a-zA-Z0-9]"))
        {
            yield return new ValidationResult("Special character needed: Password must contain at least one special character (e.g. !@#$%^&*).", new[] { nameof(Password) });
        }
    }
}
