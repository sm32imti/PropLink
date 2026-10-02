using System.Text.Json.Serialization;

namespace PropLink.Web.Models;

public class SubscriptionPricingViewModel
{
    public bool IsSubscribed { get; set; }
    public string SubscriptionTier { get; set; } = "Free";
    public DateTime? SubscriptionExpiresAt { get; set; }
    public int DaysRemaining { get; set; }

    public int PostsUsedThisMonth { get; set; }
    public int PostLimit { get; set; } = 1; // 1 for Free, 10 for Pro
    public bool CanPostMore => PostsUsedThisMonth < PostLimit;

    public int VisitsUsedThisMonth { get; set; }
    public int VisitLimit { get; set; } = 2; // 2 for Free, 99999 for Pro (Unlimited)
    public bool IsVisitLimitUnlimited => IsSubscribed;
    public bool CanRequestMoreVisits => IsSubscribed || VisitsUsedThisMonth < VisitLimit;

    public decimal MonthlyProPrice { get; set; } = 999.00m;
}

public class SubscriptionSuccessViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public string? BankTranId { get; set; }
    public string? ValidationId { get; set; }
    public decimal Amount { get; set; } = 999.00m;
    public string Currency { get; set; } = "BDT";
    public string PaymentMethod { get; set; } = "SSLCommerz";
    public string? CardType { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public DateTime SubscriptionExpiresAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
}

public class SSLCommerzInitResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("failedreason")]
    public string? FailedReason { get; set; }

    [JsonPropertyName("sessionkey")]
    public string? SessionKey { get; set; }

    [JsonPropertyName("GatewayPageURL")]
    public string? GatewayPageURL { get; set; }
}

public class SSLCommerzValidationResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("tran_date")]
    public string? TranDate { get; set; }

    [JsonPropertyName("tran_id")]
    public string? TranId { get; set; }

    [JsonPropertyName("val_id")]
    public string? ValId { get; set; }

    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }

    [JsonPropertyName("bank_tran_id")]
    public string? BankTranId { get; set; }
}
