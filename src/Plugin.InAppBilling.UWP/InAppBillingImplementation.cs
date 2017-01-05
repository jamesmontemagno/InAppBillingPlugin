using Plugin.InAppBilling.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class InAppBillingImplementation : IInAppBilling
    {
        public string ValidationPublicKey { get; set; }

        public Task<bool> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task DisconnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task<InAppBillingProduct> GetProductInfoAsync(string productId)
        {
            throw new NotImplementedException();
        }

        public Task<List<InAppBillingPurchase>> GetPurchasesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<InAppBillingPurchase> SubscribeAsync(string productId)
        {
            throw new NotImplementedException();
        }
    }
}