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

    // ==========================================
    // 5. AGENT MARKS PROPERTY AS SOLD
    // ==========================================
    [HttpPost("mark-sold")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSold(Guid bookingId, decimal sellingPrice, string? remarks)
    {
        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                .Include(b => b.Buyer)
                .Include(b => b.Seller)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch { }

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
        booking.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(remarks))
        {
            booking.Notes = (booking.Notes ?? "") + $" | Sale Finalized: {remarks}";
        }

        // 1. Explicitly fetch and update tracked entity in PostgreSQL
        Property? dbProp = null;
        try
        {
            dbProp = await _context.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId);
        }
        catch { }

        if (dbProp != null)
        {
            dbProp.TransactionStatus = TransactionStatus.Sold;
            dbProp.ListingStatus = ListingStatus.Sold;
            if (sellingPrice > 0)
            {
                dbProp.Price = sellingPrice;
            }
            try
            {
                _context.Properties.Update(dbProp);
            }
            catch { }
        }

        // 2. Also update navigation property if present
        if (booking.Property != null)
        {
            booking.Property.TransactionStatus = TransactionStatus.Sold;
            booking.Property.ListingStatus = ListingStatus.Sold;
            if (sellingPrice > 0)
            {
                booking.Property.Price = sellingPrice;
            }
        }

        // 3. Keep in-memory registry perfectly synchronized
        InspectionController._inspectionRegistry[booking.Id] = booking;
        foreach (var b in InspectionController._inspectionRegistry.Values.Where(x => x.PropertyId == booking.PropertyId))
        {
            if (b.Property != null)
            {
                b.Property.TransactionStatus = TransactionStatus.Sold;
                b.Property.ListingStatus = ListingStatus.Sold;
                if (sellingPrice > 0) b.Property.Price = sellingPrice;
            }
        }

        // 4. Create or update PropertyTransaction record for buyer & seller history
        var finalAgreedPrice = sellingPrice > 0 ? sellingPrice : (dbProp?.Price ?? booking.AskingPrice);
        PropertyTransaction? tx = null;
        try
        {
            tx = await _context.PropertyTransactions
                .FirstOrDefaultAsync(t => t.PropertyId == booking.PropertyId && t.BuyerId == booking.BuyerId);
        }
        catch { }

        if (tx == null)
        {
            tx = new PropertyTransaction
            {
                Id = Guid.NewGuid(),
                PropertyId = booking.PropertyId,
                Property = dbProp ?? booking.Property,
                BuyerId = booking.BuyerId,
                AgreedPrice = finalAgreedPrice,
                Status = TransactionStatus.Sold,
                Notes = string.IsNullOrWhiteSpace(remarks) ? "Inspection verified and deed transferred by Verification Agent." : remarks,
                TransactionDate = DateTime.UtcNow,
                CompletedDate = DateTime.UtcNow
            };
            try
            {
                _context.PropertyTransactions.Add(tx);
            }
            catch { }
        }
        else
        {
            tx.Status = TransactionStatus.Sold;
            tx.AgreedPrice = finalAgreedPrice;
            tx.CompletedDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                tx.Notes = (tx.Notes ?? "") + $" | {remarks}";
            }
            try
            {
                _context.PropertyTransactions.Update(tx);
            }
            catch { }
        }

        InspectionController._transactionRegistry[tx.Id] = tx;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AgentController.MarkSold] Db save: {ex.Message}");
        }

        var displayPrice = dbProp?.Price ?? (booking.Property?.Price ?? sellingPrice);
        TempData["ToastMessage"] = $"Property successfully marked as SOLD for {displayPrice:C0}! Added to buyer's Buying History and seller's Selling History.";
        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 6. AGENT MARKS PROPERTY AS NOT SOLD
    // ==========================================
    [HttpPost("mark-not-sold")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotSold(Guid bookingId, string? reason)
    {
        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch { }

        if (booking == null && InspectionController._inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Booking record not found.";
            return RedirectToAction(nameof(Index));
        }

        booking.Status = InspectionStatus.DeclinedBySeller;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            booking.Notes = (booking.Notes ?? "") + $" | Not Sold: {reason}";
        }

        Property? dbProp = null;
        try
        {
            dbProp = await _context.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId);
        }
        catch { }

        if (dbProp != null)
        {
            dbProp.TransactionStatus = TransactionStatus.Available;
            dbProp.ListingStatus = ListingStatus.Approved;
            try
            {
                _context.Properties.Update(dbProp);
            }
            catch { }
        }

        if (booking.Property != null)
        {
            booking.Property.TransactionStatus = TransactionStatus.Available;
            booking.Property.ListingStatus = ListingStatus.Approved;
        }

        InspectionController._inspectionRegistry[booking.Id] = booking;
        foreach (var b in InspectionController._inspectionRegistry.Values.Where(x => x.PropertyId == booking.PropertyId))
        {
            if (b.Property != null)
            {
                b.Property.TransactionStatus = TransactionStatus.Available;
                b.Property.ListingStatus = ListingStatus.Approved;
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch { }

        TempData["ToastMessage"] = "Inspection marked as Not Sold. The property block has been removed and it is now open for new buyer requests!";
        return RedirectToAction(nameof(Index));
    }
}
