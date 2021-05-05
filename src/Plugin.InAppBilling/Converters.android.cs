using System;
using Android.BillingClient.Api;

namespace Plugin.InAppBilling
{
    public static class Converters
    {
        public static InAppBillingPurchase ToIABPurchase(this Purchase purchase)
        {
            var finalPurchase = new InAppBillingPurchase
            {
                AutoRenewing = purchase.IsAutoRenewing,
                ConsumptionState = ConsumptionState.NoYetConsumed,
                Id = purchase.OrderId,
                IsAcknowledged = purchase.IsAcknowledged,
                Payload = purchase.DeveloperPayload,
                ProductId = purchase.Sku,
                PurchaseToken = purchase.PurchaseToken,
                TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(purchase.PurchaseTime).DateTime,
                ObfuscatedAccountId = purchase.AccountIdentifiers?.ObfuscatedAccountId,
                ObfuscatedProfileId = purchase.AccountIdentifiers?.ObfuscatedProfileId
            };

            finalPurchase.State = purchase.PurchaseState switch
            {
                Android.BillingClient.Api.PurchaseState.Pending => PurchaseState.PaymentPending,
                Android.BillingClient.Api.PurchaseState.Purchased => PurchaseState.Purchased,
                _ => PurchaseState.Unknown
            };
            return finalPurchase;
        }
    }
}
