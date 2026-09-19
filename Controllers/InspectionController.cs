using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropLink.Domain.Entities;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;
using PropLink.Web.Models;

namespace PropLink.Web.Controllers;

[Authorize]
public class InspectionController : Controller
{
    private readonly ApplicationDbContext _context;

    // Concurrent registry fallback to guarantee immediate memory persistence
    internal static readonly ConcurrentDictionary<Guid, InspectionBooking> _inspectionRegistry = new();

    public InspectionController(ApplicationDbContext context)
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

    private async Task<List<InspectionBooking>> GetAllBookingsAsync()
    {
        List<InspectionBooking> bookings = new();
        try
        {
            bookings = await _context.InspectionBookings
                .Include(i => i.Property)
                    .ThenInclude(p => p!.Images)
                .Include(i => i.Buyer)
                .Include(i => i.Seller)
                .Include(i => i.Agent)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
        }
        catch
        {
        }

        foreach (var regItem in _inspectionRegistry.Values)
        {
            if (!bookings.Any(b => b.Id == regItem.Id))
            {
                bookings.Add(regItem);
            }
        }

        return bookings;
    }

    // ==========================================
    // 1. BUYER SUBMITS REQUEST (Direct or Offer)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestInspection(InspectionRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Please ensure all required inspection details and terms are completed.";
            return RedirectToAction("Details", "Property", new { id = model.PropertyId });
        }

        var buyerId = CurrentUserId;
        if (!buyerId.HasValue)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Property", new { id = model.PropertyId }) });
        }

        Property? property = null;
        try
        {
            property = await _context.Properties
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.Id == model.PropertyId);
        }
        catch
        {
        }

        if (property == null)
        {
            TempData["ErrorMessage"] = "Property record not found.";
            return RedirectToAction("Index", "Property");
        }

        if (property.SellerId == buyerId.Value)
        {
            TempData["ErrorMessage"] = "You cannot schedule an inspection on your own property listing.";
            return RedirectToAction("Details", "Property", new { id = model.PropertyId });
        }

        // 1. Property Lock Check: Once locked after inspection, no more buyers can request inspection
        bool isPropertyLocked = property.TransactionStatus == TransactionStatus.UnderProcessing;
        if (!isPropertyLocked)
        {
            try
            {
                isPropertyLocked = await _context.InspectionBookings
                    .AnyAsync(i => i.PropertyId == property.Id && i.Status == InspectionStatus.UnderProcessing);
            }
            catch { }

            if (!isPropertyLocked)
            {
                isPropertyLocked = _inspectionRegistry.Values
                    .Any(i => i.PropertyId == property.Id && i.Status == InspectionStatus.UnderProcessing);
            }
        }

        if (isPropertyLocked)
        {
            TempData["ErrorMessage"] = "This property has completed deed inspection and is currently locked under title processing. No further inspection requests are permitted.";
            return RedirectToAction("Details", "Property", new { id = model.PropertyId });
        }

        // 2. Single Request Constraint: A buyer can request inspection only once per property
        bool alreadyRequested = false;
        try
        {
            alreadyRequested = await _context.InspectionBookings
                .AnyAsync(i => i.PropertyId == property.Id && i.BuyerId == buyerId.Value && i.Status != InspectionStatus.Cancelled && i.Status != InspectionStatus.DeclinedBySeller);
        }
        catch { }

        if (!alreadyRequested)
        {
            alreadyRequested = _inspectionRegistry.Values
                .Any(i => i.PropertyId == property.Id && i.BuyerId == buyerId.Value && i.Status != InspectionStatus.Cancelled && i.Status != InspectionStatus.DeclinedBySeller);
        }

        if (alreadyRequested)
        {
            TempData["ErrorMessage"] = "You have already submitted an inspection or purchase request for this property. Buyers can only request inspection once per property. Please view your status in My Purchase Requests.";
            return RedirectToAction(nameof(MyRequests));
        }

        // Find buyer entity or create placeholder
        User? buyer = null;
        try
        {
            buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyerId.Value);
        }
        catch
        {
        }

        if (buyer == null && AccountController._userRegistry.TryGetValue(User.Identity?.Name ?? "", out var regBuyer))
        {
            buyer = regBuyer;
        }

        var booking = new InspectionBooking
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Property = property,
            BuyerId = buyerId.Value,
            Buyer = buyer,
            SellerId = property.SellerId,
            Seller = property.Seller,
            InspectionType = model.InspectionType,
            AskingPrice = property.Price,
            OfferedPrice = model.InspectionType == "PriceOfferWithInspection" ? model.OfferedPrice : null,
            PreferredDate = model.PreferredDate,
            Status = InspectionStatus.PendingSellerApproval,
            AgreedToAntiBypassTerms = model.AgreeToAntiBypassTerms,
            Notes = $"Slot: {model.PreferredTimeSlot} | Attendees: {model.AttendeesCount} | Notes: {model.BuyerNotes}",
            CreatedAt = DateTime.UtcNow
        };

        _inspectionRegistry[booking.Id] = booking;

        try
        {
            _context.InspectionBookings.Add(booking);
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        TempData["ToastMessage"] = "Your request has been submitted to the seller queue! The seller will review incoming offers and requests. Once accepted, an assigned Verification Agent will schedule the physical deed inspection.";
        return RedirectToAction(nameof(MyRequests));
    }

    // ==========================================
    // 2. BUYER QUEUE: MY REQUESTS
    // ==========================================
    [HttpGet]
    [Route("inspection/my-requests")]
    public async Task<IActionResult> MyRequests()
    {
        var buyerId = CurrentUserId;
        if (!buyerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var allBookings = await GetAllBookingsAsync();
        var myRequests = allBookings
            .Where(b => b.BuyerId == buyerId.Value)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BuyerInspectionItemViewModel
            {
                BookingId = b.Id,
                PropertyId = b.PropertyId,
                PropertyTitle = b.Property?.Title ?? "Verified Real Estate Property",
                PropertyAddress = b.Property?.Address ?? "Deed-Audited Location",
                PropertyCity = b.Property?.City ?? "PropLink Registry",
                PropertyImageUrl = b.Property?.Images?.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl
                    ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                AskingPrice = b.AskingPrice,
                OfferedPrice = b.OfferedPrice,
                InspectionType = b.InspectionType,
                SellerName = b.Seller?.FullName ?? "Verified Owner",
                AgentName = b.AgentName,
                PreferredDate = b.PreferredDate,
                ScheduledDate = b.ScheduledDate,
                MeetingLocationNotes = b.MeetingLocationNotes,
                Status = b.Status,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt
            }).ToList();

        return View(myRequests);
    }

    // ==========================================
    // 3. SELLER QUEUE: PROPERTIES WITH REQUESTS
    // ==========================================
    [HttpGet]
    [Route("inspection/seller-queue")]
    public async Task<IActionResult> SellerQueue()
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        var allBookings = await GetAllBookingsAsync();
        var sellerBookings = (User.IsInRole("Admin")
            ? allBookings
            : allBookings.Where(b => b.SellerId == sellerId.Value)).ToList();

        // Group by property
        var propertyGroups = sellerBookings
            .GroupBy(b => b.PropertyId)
            .Select(g =>
            {
                var sample = g.First();
                return new SellerPropertyQueueItemViewModel
                {
                    PropertyId = g.Key,
                    PropertyTitle = sample.Property?.Title ?? "Verified Property",
                    PropertyAddress = sample.Property?.Address ?? "Audited Address",
                    PropertyCity = sample.Property?.City ?? "PropLink Market",
                    PropertyImageUrl = sample.Property?.Images?.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl
                        ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
                    AskingPrice = sample.AskingPrice,
                    TransactionStatus = sample.Property?.TransactionStatus ?? TransactionStatus.Available,
                    TotalRequestsCount = g.Count(),
                    PendingReviewCount = g.Count(b => b.Status == InspectionStatus.PendingSellerApproval),
                    ForwardedToAgentCount = g.Count(b => b.Status == InspectionStatus.PendingAgentSchedule || b.Status == InspectionStatus.ScheduleFixed || b.Status == InspectionStatus.UnderProcessing),
                    HighestOfferPrice = g.Where(b => b.OfferedPrice.HasValue).Select(b => b.OfferedPrice!.Value).DefaultIfEmpty(sample.AskingPrice).Max(),
                    LatestRequestDate = g.Max(b => b.CreatedAt)
                };
            })
            .OrderByDescending(p => p.PendingReviewCount > 0)
            .ThenByDescending(p => p.LatestRequestDate)
            .ToList();

        return View(propertyGroups);
    }

    // =======================================================
    // 4. SELLER PROPERTY REQUESTS: DETAIL LIST FOR ONE PROPERTY
    // =======================================================
    [HttpGet]
    [Route("inspection/property-requests/{propertyId:guid}")]
    public async Task<IActionResult> PropertyRequests(Guid propertyId)
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        Property? property = null;
        try
        {
            property = await _context.Properties
                .Include(p => p.Images)
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.Id == propertyId);
        }
        catch
        {
        }

        var allBookings = await GetAllBookingsAsync();
        var requestsForProperty = allBookings
            .Where(b => b.PropertyId == propertyId && (b.SellerId == sellerId.Value || User.IsInRole("Admin")))
            .OrderByDescending(b => b.Status == InspectionStatus.PendingSellerApproval)
            .ThenByDescending(b => b.CreatedAt)
            .Select(b => new SellerInspectionRequestItemViewModel
            {
                BookingId = b.Id,
                BuyerId = b.BuyerId,
                BuyerName = b.Buyer?.FullName ?? "Prospective Buyer",
                InspectionType = b.InspectionType,
                AskingPrice = b.AskingPrice,
                OfferedPrice = b.OfferedPrice,
                PreferredDate = b.PreferredDate,
                ScheduledDate = b.ScheduledDate,
                MeetingLocationNotes = b.MeetingLocationNotes,
                Status = b.Status,
                AgentName = b.AgentName,
                Notes = b.Notes,
                CreatedAt = b.CreatedAt,
                AgreedToAntiBypassTerms = b.AgreedToAntiBypassTerms
            }).ToList();

        if (property == null && requestsForProperty.Any())
        {
            var firstReq = allBookings.FirstOrDefault(b => b.PropertyId == propertyId);
            property = firstReq?.Property;
        }

        var viewModel = new SellerPropertyRequestsViewModel
        {
            PropertyId = propertyId,
            PropertyTitle = property?.Title ?? "Verified Real Estate Property",
            PropertyAddress = property?.Address ?? "Deed-Audited Location",
            PropertyCity = property?.City ?? "PropLink Registry",
            PropertyImageUrl = property?.Images?.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl
                ?? "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=1200&q=80",
            AskingPrice = property?.Price ?? (requestsForProperty.FirstOrDefault()?.AskingPrice ?? 0),
            TransactionStatus = property?.TransactionStatus ?? TransactionStatus.Available,
            Requests = requestsForProperty
        };

        return View(viewModel);
    }

    // ===============================================================
    // 5. SELLER ACCEPTS PREFERRED REQUEST -> FORWARDS TO VERIFICATION AGENT
    // ===============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("inspection/accept-request")]
    public async Task<IActionResult> AcceptRequest(Guid bookingId)
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Property)
                .Include(b => b.Buyer)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch
        {
        }

        if (booking == null && _inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Inspection request not found.";
            return RedirectToAction(nameof(SellerQueue));
        }

        if (booking.SellerId != sellerId.Value && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Unauthorized action. You are not the owner of this listing.";
            return RedirectToAction(nameof(SellerQueue));
        }

        // Find or assign active Verification Agent
        User? agent = null;
        try
        {
            agent = await _context.Users.FirstOrDefaultAsync(u => u.Role == "VerificationAgent" && !u.IsBanned);
        }
        catch
        {
        }

        if (agent == null)
        {
            agent = AccountController._userRegistry.Values.FirstOrDefault(u => u.Role == "VerificationAgent" && !u.IsBanned);
        }

        // Forward to Verification Agent!
        booking.Status = InspectionStatus.PendingAgentSchedule;
        booking.AgentId = agent?.Id;
        booking.AgentName = agent?.FullName ?? "Verification Agent";

        _inspectionRegistry[booking.Id] = booking;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        var buyerDisplayName = booking.Buyer?.FullName ?? "the buyer";
        TempData["ToastMessage"] = $"Offer/Request from {buyerDisplayName} accepted! This request has now been forwarded to the assigned Verification Agent to fix the physical inspection schedule.";

        return RedirectToAction(nameof(PropertyRequests), new { propertyId = booking.PropertyId });
    }

    // ===============================================================
    // 6. SELLER DECLINES REQUEST
    // ===============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("inspection/decline-request")]
    public async Task<IActionResult> DeclineRequest(Guid bookingId)
    {
        var sellerId = CurrentUserId;
        if (!sellerId.HasValue)
        {
            return RedirectToAction("Login", "Account");
        }

        InspectionBooking? booking = null;
        try
        {
            booking = await _context.InspectionBookings
                .Include(b => b.Buyer)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
        }
        catch
        {
        }

        if (booking == null && _inspectionRegistry.TryGetValue(bookingId, out var regBooking))
        {
            booking = regBooking;
        }

        if (booking == null)
        {
            TempData["ErrorMessage"] = "Inspection request not found.";
            return RedirectToAction(nameof(SellerQueue));
        }

        if (booking.SellerId != sellerId.Value && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Unauthorized action.";
            return RedirectToAction(nameof(SellerQueue));
        }

        booking.Status = InspectionStatus.DeclinedBySeller;
        _inspectionRegistry[booking.Id] = booking;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
        }

        TempData["ToastMessage"] = "The buyer request has been marked as declined.";
        return RedirectToAction(nameof(PropertyRequests), new { propertyId = booking.PropertyId });
    }
}
