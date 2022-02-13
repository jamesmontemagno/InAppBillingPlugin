using System;
using System.Linq;
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
                OriginalJson = purchase.OriginalJson,
                Signature = purchase.Signature,
                IsAcknowledged = purchase.IsAcknowledged,
                Payload = purchase.DeveloperPayload,
                ProductId = purchase.Skus.FirstOrDefault(),
                Quantity = purchase.Quantity,
                ProductIds = purchase.Skus,
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

        public static InAppBillingPurchase ToIABPurchase(this PurchaseHistoryRecord purchase)
        {
            return new InAppBillingPurchase
            {
                ConsumptionState = ConsumptionState.NoYetConsumed,
                OriginalJson = purchase.OriginalJson,
                Signature = purchase.Signature,
                Payload = purchase.DeveloperPayload,
                ProductId = purchase.Skus.FirstOrDefault(),
                Quantity = purchase.Quantity,
                ProductIds = purchase.Skus,
                PurchaseToken = purchase.PurchaseToken,
                TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(purchase.PurchaseTime).DateTime,
                State = PurchaseState.Unknown
            };
        }
    }
}
