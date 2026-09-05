using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PropLink.Domain.Enums;
using PropLink.Infrastructure.Data;

namespace PropLink.Infrastructure.Services;

public class AuctionExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuctionExpiryBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);

    public AuctionExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AuctionExpiryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auction Expiry Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredAuctionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing expired auctions.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Auction Expiry Background Service stopped.");
    }

    private async Task ProcessExpiredAuctionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        var expiredAuctions = await context.Auctions
            .Include(a => a.Bids)
            .Include(a => a.Property)
            .Where(a => a.Status == AuctionStatus.Active && a.EndTime <= now)
            .ToListAsync(cancellationToken);

        if (!expiredAuctions.Any())
        {
            return;
        }

        _logger.LogInformation("Found {Count} expired active auction(s) to process.", expiredAuctions.Count);

        foreach (var auction in expiredAuctions)
        {
            var highestBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();

            if (highestBid != null)
            {
                auction.Status = AuctionStatus.AwaitingSellerConfirmation;
                auction.WinningBidId = highestBid.Id;
                _logger.LogInformation("Auction {AuctionId} for property {PropertyId} ended with highest bid {Amount} (BidId: {BidId}). Set status to AwaitingSellerConfirmation.",
                    auction.Id, auction.PropertyId, highestBid.Amount, highestBid.Id);
            }
            else
            {
                auction.Status = AuctionStatus.Expired;
                _logger.LogInformation("Auction {AuctionId} for property {PropertyId} ended with NO bids. Set status to Expired.",
                    auction.Id, auction.PropertyId);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
