using System;

namespace Plugin.InAppBilling.Abstractions
{
    public class InAppBillingPurchase
    {
        public InAppBillingPurchase()
        {
        }


        public string Id { get; set; }
        public DateTime TransactionDateUtc { get; set; }

        public string ProductId { get; set; }
    }

}
