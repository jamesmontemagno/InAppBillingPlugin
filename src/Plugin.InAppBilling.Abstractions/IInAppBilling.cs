
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Plugin.InAppBilling.Abstractions
{
    /// <summary>
    /// Interface for InAppBilling
    /// </summary>
    public interface IInAppBilling
    {
        Task<bool> ConnectAsync();
        Task DisconnectAsync();

        Task<InAppBillingProduct> GetProductInfoAsync(string productId);

        Task<List<InAppBillingPurchase>> GetPurchasesAsync();

        Task<InAppBillingPurchase> SubscribeAsync(string productId);

        string ValidationPublicKey { get; set; }
    }
}
