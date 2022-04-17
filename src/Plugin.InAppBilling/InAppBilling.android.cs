using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Java.Security;
using Java.Security.Spec;
using Java.Lang;
using System.Text;
using Android.BillingClient.Api;
using Android.Content;
using System.Diagnostics.CodeAnalysis;
#if NET
using Microsoft.Maui.ApplicationModel;
#else
using Xamarin.Essentials;
#endif

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingImplementation : BaseInAppBilling
    {
        /// <summary>
        /// Gets or sets a callback for out of band purchases to complete.
        /// </summary>
        public static Action<BillingResult, List<InAppBillingPurchase>?>? OnAndroidPurchasesUpdated { get; set; } = null;

        /// <summary>
        /// Gets the context, aka the currently activity.
        /// This is set from the MainApplication.cs file that was laid down by the plugin
        /// </summary>
        /// <value>The context.</value>
        Activity Activity =>
            Platform.CurrentActivity ?? throw new NullReferenceException("Current Activity is null, ensure that the MainActivity.cs file is configuring Xamarin.Essentials in your source code so the In App Billing can use it.");

        Context Context => Android.App.Application.Context;

        /// <summary>
        /// Default Constructor for In App Billing Implementation on Android
        /// </summary>
        public InAppBillingImplementation()
        {

        }


        BillingClient? BillingClient { get; set; }
        BillingClient.Builder? BillingClientBuilder { get; set; }
        /// <summary>
        /// Determines if it is connected to the backend actively (Android).
        /// </summary>
        public override bool IsConnected { get; set; }
        TaskCompletionSource<(BillingResult billingResult, IList<Purchase> purchases)>? tcsPurchase;
        TaskCompletionSource<bool>? tcsConnect;
        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        [MemberNotNull(nameof(tcsConnect))]
        [MemberNotNull(nameof(BillingClient))]
        [MemberNotNull(nameof(BillingClientBuilder))]
        public override Task<bool> ConnectAsync(bool enablePendingPurchases = true)
        {
            tcsPurchase?.TrySetCanceled();
            tcsPurchase = null;

            tcsConnect?.TrySetCanceled();
            tcsConnect = new TaskCompletionSource<bool>();

            BillingClientBuilder = BillingClient.NewBuilder(Context);
            BillingClientBuilder.SetListener(OnPurchasesUpdated);
            if (enablePendingPurchases)
                BillingClient = BillingClientBuilder.EnablePendingPurchases().Build();
            else
                BillingClient = BillingClientBuilder.Build();

            BillingClient.StartConnection(OnSetupFinished, OnDisconnected);

            return tcsConnect.Task;

            void OnSetupFinished(BillingResult billingResult)
            {
                Console.WriteLine($"Billing Setup Finished : {billingResult.ResponseCode} - {billingResult.DebugMessage}");
                IsConnected = billingResult.ResponseCode == BillingResponseCode.Ok;
                tcsConnect?.TrySetResult(IsConnected);
            }

            void OnDisconnected()
            {
                IsConnected = false;
            }
        }

        public void OnPurchasesUpdated(BillingResult billingResult, IList<Android.BillingClient.Api.Purchase> purchases)
        {
            tcsPurchase?.TrySetResult((billingResult, purchases));

            if (OnAndroidPurchasesUpdated == null)
                return;

            OnAndroidPurchasesUpdated?.Invoke(billingResult, purchases?.Select(p => p.ToIABPurchase())?.ToList());
        }

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public override Task DisconnectAsync()
        {
            try
            {
                BillingClientBuilder?.Dispose();
                BillingClientBuilder = null;
                BillingClient?.EndConnection();
                BillingClient?.Dispose();
                BillingClient = null;
                IsConnected = false;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to disconnect: {ex.Message}");
            }

            return Task.CompletedTask;
        }


        /// <summary>
        /// Gets or sets if in testing mode. Only for UWP
        /// </summary>
        public override bool InTestingMode { get; set; }

        /// <summary>
        /// Get product information of a specific product
        /// </summary>
        /// <param name="productIds">Sku or Id of the product</param>
        /// <param name="itemType">Type of product offering</param>
        /// <returns></returns>
        public async override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
        {
            if (BillingClient == null || !IsConnected)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }


            var skuType = itemType switch
            {
                ItemType.InAppPurchase => BillingClient.SkuType.Inapp,
                ItemType.InAppPurchaseConsumable => BillingClient.SkuType.Inapp,
                _ => BillingClient.SkuType.Subs
            };

            if(skuType == BillingClient.SkuType.Subs)
            {
                var result = BillingClient.IsFeatureSupported(BillingClient.FeatureType.Subscriptions);
                ParseBillingResult(result);
            }

            var skuDetailsParams = SkuDetailsParams.NewBuilder()
                .SetType(skuType)
                .SetSkusList(productIds)
                .Build();

            var skuDetailsResult = await BillingClient.QuerySkuDetailsAsync(skuDetailsParams);
            ParseBillingResult(skuDetailsResult?.Result);


            return skuDetailsResult.SkuDetails.Select(product => product.ToIAPProduct());
        }

        
		public override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, List<string>? doNotFinishTransactionIds = null)
        {
            if (BillingClient == null)
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");

            var skuType = itemType switch
            {
                ItemType.InAppPurchase => BillingClient.SkuType.Inapp,
                ItemType.InAppPurchaseConsumable => BillingClient.SkuType.Inapp,
                _ => BillingClient.SkuType.Subs
            };

            var purchasesResult = BillingClient.QueryPurchases(skuType);

            ParseBillingResult(purchasesResult.BillingResult);

            return Task.FromResult(purchasesResult.PurchasesList.Select(p => p.ToIABPurchase()));
        }

        /// <summary>
        /// Android only: Returns the most recent purchase made by the user for each SKU, even if that purchase is expired, canceled, or consumed.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <returns>The current purchases</returns>
        public override async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesHistoryAsync(ItemType itemType)
        {
            if (BillingClient == null)
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");

            var skuType = itemType switch
            {
                ItemType.InAppPurchase => BillingClient.SkuType.Inapp,
                ItemType.InAppPurchaseConsumable => BillingClient.SkuType.Inapp,
                _ => BillingClient.SkuType.Subs
            };

            var purchasesResult = await BillingClient.QueryPurchaseHistoryAsync(skuType);


            return purchasesResult?.PurchaseHistoryRecords?.Select(p => p.ToIABPurchase()) ?? Enumerable.Empty<InAppBillingPurchase>();
        }

        /// <summary>
        /// (Android specific) Upgrade/Downgrade/Change a previously purchased subscription
        /// </summary>
        /// <param name="newProductId">Sku or ID of product that will replace the old one</param>
        /// <param name="purchaseTokenOfOriginalSubscription">Purchase token of original subscription</param>
        /// <param name="prorationMode">Proration mode (1 - ImmediateWithTimeProration, 2 - ImmediateAndChargeProratedPrice, 3 - ImmediateWithoutProration, 4 - Deferred)</param>
        /// <returns>Purchase details</returns>
        public override async Task<InAppBillingPurchase?> UpgradePurchasedSubscriptionAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode = SubscriptionProrationMode.ImmediateWithTimeProration)
        {
            if (BillingClient == null || !IsConnected)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }

            // If we have a current task and it is not completed then return null.
            // you can't try to purchase twice.
            if (tcsPurchase?.Task != null && !tcsPurchase.Task.IsCompleted)
            {
                return null;
            }

            var purchase = await UpgradePurchasedSubscriptionInternalAsync(newProductId, purchaseTokenOfOriginalSubscription, prorationMode);

            return purchase;
        }

        async Task<InAppBillingPurchase?> UpgradePurchasedSubscriptionInternalAsync(string newProductId, string purchaseTokenOfOriginalSubscription, SubscriptionProrationMode prorationMode)
        {
            var itemType = BillingClient.SkuType.Subs;

            if (tcsPurchase?.Task != null && !tcsPurchase.Task.IsCompleted)
            {
                return null;
            }

            var skuDetailsParams = SkuDetailsParams.NewBuilder()
                .SetType(itemType)
                .SetSkusList(new List<string> { newProductId })
                .Build();

            var skuDetailsResult = await BillingClient!.QuerySkuDetailsAsync(skuDetailsParams);
            ParseBillingResult(skuDetailsResult?.Result);

            var skuDetails = skuDetailsResult?.SkuDetails.FirstOrDefault();

            if (skuDetails == null)
                throw new ArgumentException($"{newProductId} does not exist");

            //1 - BillingFlowParams.ProrationMode.ImmediateWithTimeProration
            //2 - BillingFlowParams.ProrationMode.ImmediateAndChargeProratedPrice
            //3 - BillingFlowParams.ProrationMode.ImmediateWithoutProration
            //4 - BillingFlowParams.ProrationMode.Deferred

            var updateParams = BillingFlowParams.SubscriptionUpdateParams.NewBuilder()
                .SetOldSkuPurchaseToken(purchaseTokenOfOriginalSubscription)
                .SetReplaceSkusProrationMode((int)prorationMode)
                .Build();

            var flowParams = BillingFlowParams.NewBuilder()
                .SetSkuDetails(skuDetails)
                .SetSubscriptionUpdateParams(updateParams)
                .Build();

            tcsPurchase = new TaskCompletionSource<(BillingResult billingResult, IList<Android.BillingClient.Api.Purchase> purchases)>();
            var responseCode = BillingClient.LaunchBillingFlow(Activity, flowParams);

            ParseBillingResult(responseCode);

            var result = await tcsPurchase.Task;
            ParseBillingResult(result.billingResult);

            //we are only buying 1 thing.
            var androidPurchase = result.purchases?.FirstOrDefault(p => p.Skus.Contains(newProductId));

            //for some reason the data didn't come back
            if (androidPurchase == null)
            {
                var purchases = await GetPurchasesAsync(itemType == BillingClient.SkuType.Inapp ? ItemType.InAppPurchase : ItemType.Subscription);
                return purchases.FirstOrDefault(p => p.ProductId == newProductId);
            }

            return androidPurchase.ToIABPurchase();
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="obfuscatedAccountId">Specifies an optional obfuscated string that is uniquely associated with the user's account in your app.</param>
        /// <param name="obfuscatedProfileId">Specifies an optional obfuscated string that is uniquely associated with the user's profile in your app.</param>
        /// <returns></returns>
        public async override Task<InAppBillingPurchase?> PurchaseAsync(string productId, ItemType itemType, string? obfuscatedAccountId = null, string? obfuscatedProfileId = null)
        {
            if (BillingClient == null || !IsConnected)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }

            // If we have a current task and it is not completed then return null.
            // you can't try to purchase twice.
            if (tcsPurchase?.Task != null && !tcsPurchase.Task.IsCompleted)
            {
                return null;
            }

            switch (itemType)
            {
                case ItemType.InAppPurchase:
                case ItemType.InAppPurchaseConsumable:
                    return await PurchaseAsync(productId, BillingClient.SkuType.Inapp, obfuscatedAccountId, obfuscatedProfileId);
                case ItemType.Subscription:

                    var result = BillingClient.IsFeatureSupported(BillingClient.FeatureType.Subscriptions);
                    ParseBillingResult(result);
                    return await PurchaseAsync(productId, BillingClient.SkuType.Subs, obfuscatedAccountId, obfuscatedProfileId);
            }

            return null;
        }

        async Task<InAppBillingPurchase?> PurchaseAsync(string productSku, string itemType, string? obfuscatedAccountId = null, string? obfuscatedProfileId = null)
        {
            var skuDetailsParams = SkuDetailsParams.NewBuilder()
                .SetType(itemType)
                .SetSkusList(new List<string> { productSku })
                .Build();

            var skuDetailsResult = await BillingClient!.QuerySkuDetailsAsync(skuDetailsParams);
            ParseBillingResult(skuDetailsResult?.Result);

            var skuDetails = skuDetailsResult.SkuDetails.FirstOrDefault();

            if (skuDetails == null)
                throw new ArgumentException($"{productSku} does not exist");

            var flowParamsBuilder = BillingFlowParams.NewBuilder()
                .SetSkuDetails(skuDetails);

            if (!string.IsNullOrWhiteSpace(obfuscatedAccountId))
                flowParamsBuilder.SetObfuscatedAccountId(obfuscatedAccountId);

            if (!string.IsNullOrWhiteSpace(obfuscatedProfileId))
                flowParamsBuilder.SetObfuscatedProfileId(obfuscatedProfileId);

            var flowParams = flowParamsBuilder.Build();

            tcsPurchase = new TaskCompletionSource<(BillingResult billingResult, IList<Android.BillingClient.Api.Purchase> purchases)>();
            var responseCode = BillingClient.LaunchBillingFlow(Activity, flowParams);
            ParseBillingResult(responseCode);        

            var result = await tcsPurchase.Task;
            ParseBillingResult(result.billingResult);

            //we are only buying 1 thing.
            var androidPurchase = result.purchases?.FirstOrDefault(p => p.Skus.Contains(productSku));

            //for some reason the data didn't come back
            if (androidPurchase == null)
            {
                var purchases = await GetPurchasesAsync(itemType == BillingClient.SkuType.Inapp ? ItemType.InAppPurchase : ItemType.Subscription);
                return purchases.FirstOrDefault(p => p.ProductId == productSku);
            }

            return androidPurchase.ToIABPurchase();
        }


        public async override Task<bool> AcknowledgePurchaseAsync(string purchaseToken)
        {
            if (BillingClient == null || !IsConnected)
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");

            var acknowledgeParams = AcknowledgePurchaseParams.NewBuilder()
                    .SetPurchaseToken(purchaseToken).Build();

            var result = await BillingClient.AcknowledgePurchaseAsync(acknowledgeParams);

            return ParseBillingResult(result);
        }

        //inapp:{Context.PackageName}:{productSku}

        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="purchaseToken">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        public override async Task<bool> ConsumePurchaseAsync(string? productId, string purchaseToken, string purchaseId, List<string>? doNotFinishProductIds = null)
        {
            if (BillingClient == null || !IsConnected)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }

            
            var consumeParams = ConsumeParams.NewBuilder()
                .SetPurchaseToken(purchaseToken)
                .Build();

            var result = await BillingClient.ConsumeAsync(consumeParams);


            return ParseBillingResult(result.BillingResult);            
        }

        bool ParseBillingResult([NotNull] BillingResult? result)
        {
            if(result == null)
                throw new InAppBillingPurchaseException(PurchaseError.GeneralError);

            return result.ResponseCode switch
            {
                BillingResponseCode.Ok => true,
                BillingResponseCode.UserCancelled => throw new InAppBillingPurchaseException(PurchaseError.UserCancelled),//User Cancelled, should try again
                BillingResponseCode.ServiceUnavailable => throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable),//Network connection is down
                BillingResponseCode.ServiceDisconnected => throw new InAppBillingPurchaseException(PurchaseError.ServiceDisconnected),//Network connection is down
                BillingResponseCode.ServiceTimeout => throw new InAppBillingPurchaseException(PurchaseError.ServiceTimeout),//Network connection is down
                BillingResponseCode.BillingUnavailable => throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable),//Billing Unavailable
                BillingResponseCode.ItemNotOwned => throw new InAppBillingPurchaseException(PurchaseError.NotOwned),//Item not owned
                BillingResponseCode.DeveloperError => throw new InAppBillingPurchaseException(PurchaseError.DeveloperError),//Developer Error
                BillingResponseCode.Error => throw new InAppBillingPurchaseException(PurchaseError.GeneralError),//Generic Error
                BillingResponseCode.FeatureNotSupported => throw new InAppBillingPurchaseException(PurchaseError.FeatureNotSupported),
                BillingResponseCode.ItemAlreadyOwned => throw new InAppBillingPurchaseException(PurchaseError.AlreadyOwned),
                BillingResponseCode.ItemUnavailable => throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable),
                _ => false,
            };
        }       
    }
}
 
