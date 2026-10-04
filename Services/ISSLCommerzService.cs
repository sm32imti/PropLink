using PropLink.Web.Models;

namespace PropLink.Web.Services;

public interface ISSLCommerzService
{
    Task<SSLCommerzInitResponse> InitiateSubscriptionPaymentAsync(
        string tranId, 
        decimal amount, 
        string customerName, 
        string customerEmail, 
        string customerPhone, 
        string siteBaseUrl);

    Task<SSLCommerzValidationResponse?> ValidatePaymentAsync(string valId);
}
