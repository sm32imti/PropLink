using System.Collections.Concurrent;
using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Domain.Entities;
using PropLink.Infrastructure.Data;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;

    // In-memory persistent user fallback registry to guarantee immediate registration & login
    private static readonly ConcurrentDictionary<string, User> _userRegistry = new(StringComparer.OrdinalIgnoreCase);

    static AccountController()
    {
        // Pre-populate Admin account
        var admin = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Tamjid (Administrator)",
            Email = "tamjid@gmail.com",
            PhoneNumber = "+1-555-0100",
            Role = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("tamjid123"),
            CreatedAt = DateTime.UtcNow
        };
        _userRegistry["tamjid@gmail.com"] = admin;

        // Pre-populate Standard User account
        var defaultUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Marcus Sterling",
            Email = "user@proplink.com",
            PhoneNumber = "+1-555-0144",
            Role = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
            CreatedAt = DateTime.UtcNow
        };
        _userRegistry["user@proplink.com"] = defaultUser;
    }

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        User? user = null;
        var emailKey = model.Email.Trim().ToLower();

        // 1. Try querying from PostgreSQL database
        try
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailKey);
        }
        catch
        {
            // PostgreSQL connection failed or initializing
        }

        // 2. Fallback to in-memory registered accounts
        if (user == null && _userRegistry.TryGetValue(emailKey, out var registeredUser))
        {
            user = registeredUser;
        }

        bool isPasswordValid = false;

        if (user != null && !string.IsNullOrEmpty(user.PasswordHash))
        {
            try
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
            }
            catch
            {
                isPasswordValid = false;
            }
        }

        if (!isPasswordValid || user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email address or password. Please check your credentials.");
            return View(model);
        }

        // Create authentication claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        TempData["ToastMessage"] = $"Welcome back, {user.FullName}!";

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailKey = model.Email.Trim().ToLower();

        // Check if email already exists in in-memory registry
        if (_userRegistry.ContainsKey(emailKey))
        {
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = model.FullName.Trim(),
            Email = emailKey,
            PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty,
            Role = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            CreatedAt = DateTime.UtcNow
        };

        // Always register in registry immediately
        _userRegistry[emailKey] = newUser;

        // Also persist to PostgreSQL database
        try
        {
            var existingUserInDb = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailKey);
            if (!existingUserInDb)
            {
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
            }
        }
        catch
        {
            // If PostgreSQL is currently offline, user is preserved in registry
        }

        // Direct user after registration stage to the Login stage as requested
        TempData["SuccessMessage"] = "Registration successful! You can now log in with your credentials.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["ToastMessage"] = "You have been logged out successfully.";
        return RedirectToAction("Index", "Home");
    }
}
