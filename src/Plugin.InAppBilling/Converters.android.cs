using System;
using System.Linq;
using Android.BillingClient.Api;

namespace Plugin.InAppBilling
{
    internal static class Converters
    {
        internal static InAppBillingPurchase ToIABPurchase(this Purchase purchase)
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
                ProductId = purchase.Products?.FirstOrDefault(),
                Quantity = purchase.Quantity,
                ProductIds = purchase.Products,
                PurchaseToken = purchase.PurchaseToken,
                TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(purchase.PurchaseTime).DateTime,
                ObfuscatedAccountId = purchase.AccountIdentifiers?.ObfuscatedAccountId,
                ObfuscatedProfileId = purchase.AccountIdentifiers?.ObfuscatedProfileId,
                TransactionIdentifier = purchase.PurchaseToken
            };

            finalPurchase.State = purchase.PurchaseState switch
            {
                Android.BillingClient.Api.PurchaseState.Pending => PurchaseState.PaymentPending,
                Android.BillingClient.Api.PurchaseState.Purchased => PurchaseState.Purchased,
                _ => PurchaseState.Unknown
            };
            return finalPurchase;
        }

        internal static InAppBillingPurchase ToIABPurchase(this PurchaseHistoryRecord purchase)
        {
            return new InAppBillingPurchase
            {
                ConsumptionState = ConsumptionState.NoYetConsumed,
                OriginalJson = purchase.OriginalJson,
                Signature = purchase.Signature,
                Payload = purchase.DeveloperPayload,
                ProductId = purchase.Products?.FirstOrDefault(),
                Quantity = purchase.Quantity,
                ProductIds = purchase.Products,
                PurchaseToken = purchase.PurchaseToken,
                TransactionDateUtc = DateTimeOffset.FromUnixTimeMilliseconds(purchase.PurchaseTime).DateTime,
                State = PurchaseState.Unknown,
                TransactionIdentifier = purchase.PurchaseToken
            };
        }

        internal static InAppBillingProduct ToIAPProduct(this ProductDetails product)
        {
            var oneTime = product.GetOneTimePurchaseOfferDetails();
            var subs = product.GetSubscriptionOfferDetails()?.Select(s => new SubscriptionOfferDetail
            {
                BasePlanId = s.BasePlanId,
                OfferId = s.OfferId,
                OfferTags = s.OfferTags?.ToList(),
                OfferToken = s.OfferToken,
                PricingPhases = s?.PricingPhases?.PricingPhaseList?.Select(p =>
                new PricingPhase
                {
                    BillingCycleCount = p.,
                    BillingPeriod = p.BillingPeriod,
                    FormattedPrice = p.FormattedPrice,
                    PriceAmountMicros = p.PriceAmountMicros,
                    PriceCurrencyCode = p.PriceCurrencyCode,
                    RecurrenceMode = p.RecurrenceMode
                }).ToList()
            }).ToList(); 

            var firstSub = subs?.FirstOrDefault()?.PricingPhases?.Where(p => p.PriceAmountMicros != 0)?.FirstOrDefault();
             
            return new InAppBillingProduct
            {
                Name = product.Title,
                Description = product.Description,
                CurrencyCode = oneTime?.PriceCurrencyCode ?? firstSub?.PriceCurrencyCode,
                LocalizedPrice = oneTime?.FormattedPrice ?? firstSub?.FormattedPrice,
                ProductId = product.ProductId,
                MicrosPrice = oneTime?.PriceAmountMicros ?? firstSub?.PriceAmountMicros ?? 0,
                AndroidExtras = new InAppBillingProductAndroidExtras
                {
                    SubscriptionOfferDetails = subs
                }
            };
        }
    }
}
