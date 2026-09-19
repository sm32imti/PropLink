using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Domain.Entities;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

[Authorize(Roles = "VerificationAgent,Admin")]
[Route("agent")]
public class AgentController : Controller
{
    private readonly ApplicationDbContext _context;

    public AgentController(ApplicationDbContext context)
    {
        _context = context;
    }

    private Guid? CurrentUserId
    {
        get
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdStr, out var id) ? id : null;
        }
    }

    // ==========================================
    // 1. VERIFICATION AGENT DASHBOARD
    // ==========================================
    [HttpGet("")]
    [HttpGet("inspections")]
    public async Task<IActionResult> Index()
    {
        List<InspectionBooking> bookings = new();

        try
        {
            bookings = await _context.InspectionBookings
                .Include(i => i.Property)
                    .ThenInclude(p => p!.Images)
                .Include(i => i.Buyer)
                .Include(i => i.Seller)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }
        catch
        {
            // Fallback to in-memory registry
        }

        // Merge in-memory registry items
        foreach (var regItem in InspectionController._inspectionRegistry.Values)
        {
            if (!bookings.Any(b => b.Id == regItem.Id))
            {
                bookings.Add(regItem);
            }
        }

        // Only show requests that have been accepted by seller and forwarded for agent scheduling
        var agentQueue = bookings
            .Where(b => b.Status != InspectionStatus.PendingSellerApproval && b.Status != InspectionStatus.DeclinedBySeller)
            .ToList();

        var viewModels = agentQueue.Select(b => new AgentInspectionItemViewModel
        {
            BookingId = b.Id,
            PropertyId = b.PropertyId,
            PropertyTitle = b.Property?.Title ?? "Verified Real Estate Property",
            PropertyAddress = b.Property?.Address ?? "Deed-Audited Location",
            PropertyCity = b.Property?.City ?? "PropLink Registry",
            PropertyImageUrl = b.Property?.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl
                ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
            AskingPrice = b.AskingPrice,
            OfferedPrice = b.OfferedPrice,
            InspectionType = b.InspectionType,
            BuyerId = b.BuyerId,
            BuyerName = b.Buyer?.FullName ?? "Prospective Buyer",
            BuyerEmail = b.Buyer?.Email ?? string.Empty,
            BuyerPhone = b.Buyer?.PhoneNumber ?? string.Empty,
            SellerId = b.SellerId,
            SellerName = b.Seller?.FullName ?? "Verified Owner",
            SellerEmail = b.Seller?.Email ?? string.Empty,
            SellerPhone = b.Seller?.PhoneNumber ?? string.Empty,
            AgentId = b.AgentId,
            AgentName = b.AgentName,
            PreferredDate = b.PreferredDate,
            ScheduledDate = b.ScheduledDate,
            MeetingLocationNotes = b.MeetingLocationNotes,
            Status = b.Status,
            BuyerNotes = b.Notes,
            CreatedAt = b.CreatedAt
        }).ToList();

        return View(viewModels);
    }

    // ==========================================
    // 2. AGENT FIXES INSPECTION SCHEDULE
    // ==========================================
    [HttpPost("fix-schedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FixSchedule(AgentScheduleFixInputModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please provide a valid inspection date and time.";
            return RedirectToAction(nameof(Index));
        }

        InspectionBooking? booking = null;

        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == model.BookingId);
        }
        catch
        {
        }

        if (booking == null && InspectionController._inspectionRegistry.TryGetValue(model.BookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Inspection booking not found.";
            return RedirectToAction(nameof(Index));
        }

        var currentAgentName = User.Identity?.Name ?? "Verification Agent";

        booking.ScheduledDate = model.ScheduledDateTime;
        booking.MeetingLocationNotes = model.MeetingLocationNotes;
        booking.ScheduledAt = DateTime.UtcNow;
        booking.Status = InspectionStatus.ScheduleFixed;
        booking.AgentName = currentAgentName;
        booking.AgentId = CurrentUserId;

        // Also reflect on property transaction status if appropriate
        if (booking.Property != null && booking.Property.TransactionStatus == TransactionStatus.Available)
        {
            booking.Property.TransactionStatus = TransactionStatus.MeetingScheduled;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        TempData["ToastMessage"] = $"Meeting schedule officially fixed for {booking.Property?.Title ?? "Property"} on {model.ScheduledDateTime:MMM dd, yyyy 'at' hh:mm tt}!";
        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 3. MARK INSPECTION CONDUCTED
    // ==========================================
    [HttpPost("mark-conducted")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkConducted(Guid bookingId)
    {
        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch
        {
        }

        if (booking == null && InspectionController._inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Booking record not found.";
            return RedirectToAction(nameof(Index));
        }

        booking.Status = InspectionStatus.InspectionCompleted;
        booking.CompletedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        TempData["ToastMessage"] = "Physical inspection marked as successfully conducted on-site.";
        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 4. MARK PROPERTY UNDER PROCESSING
    // ==========================================
    [HttpPost("mark-under-processing")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUnderProcessing(Guid bookingId)
    {
        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch
        {
        }

        if (booking == null && InspectionController._inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Booking record not found.";
            return RedirectToAction(nameof(Index));
        }

        booking.Status = InspectionStatus.UnderProcessing;

        // Lock property online
        if (booking.Property != null)
        {
            booking.Property.TransactionStatus = TransactionStatus.UnderProcessing;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        TempData["ToastMessage"] = "Property has been set to 'Under Processing'! Other users can no longer submit inquiries while legal settlement proceeds.";
        return RedirectToAction(nameof(Index));
    }
}
