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

[assembly: UsesPermission("com.android.vending.BILLING")]
namespace Plugin.InAppBilling
{
	/// <summary>
	/// Implementation for Feature
	/// </summary>
	[Preserve(AllMembers = true)]
    public class InAppBillingImplementation : BaseInAppBilling
    {
		/// <summary>
		/// Gets the context, aka the currently activity.
		/// This is set from the MainApplication.cs file that was laid down by the plugin
		/// </summary>
		/// <value>The context.</value>
		Activity Activity =>
            Xamarin.Essentials.Platform.CurrentActivity ?? throw new NullReferenceException("Current Activity is null, ensure that the MainActivity.cs file is configuring Xamarin.Essentials in your source code so the In App Billing can use it.");

        Context Context => Android.App.Application.Context;

        /// <summary>
        /// Default Constructor for In App Billing Implemenation on Android
        /// </summary>
        public InAppBillingImplementation()
        {

        }


        BillingClient BillingClient { get; set; }
        BillingClient.Builder BillingClientBuilder { get; set; }
        bool IsConnected { get; set; }
        TaskCompletionSource<(BillingResult billingResult, IList<Purchase> purchases)> tcsPurchase;
        TaskCompletionSource<bool> tcsConnect;
        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
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
                System.Diagnostics.Debug.WriteLine($"Unable to disconned: {ex.Message}");
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


            return skuDetailsResult.SkuDetails.Select(product => new InAppBillingProduct
            {
                Name = product.Title,
                Description = product.Description,
                CurrencyCode = product.PriceCurrencyCode,
                LocalizedPrice = product.Price,
                ProductId = product.Sku,
                MicrosPrice = product.PriceAmountMicros,
                LocalizedIntroductoryPrice = product.IntroductoryPrice,
                MicrosIntroductoryPrice = product.IntroductoryPriceAmountMicros,
                FreeTrialPeriod = product.FreeTrialPeriod,
                IconUrl = product.IconUrl,
                IntroductoryPriceCycles = product.IntroductoryPriceCycles,
                IntroductoryPricePeriod = product.IntroductoryPricePeriod,
                MicrosOriginalPriceAmount = product.OriginalPriceAmountMicros,
                OriginalPrice = product.OriginalPrice
            });
        }

        
		public override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType)
        {
            if (BillingClient == null)
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");

            var skuType = itemType switch
            {
                ItemType.InAppPurchase => BillingClient.SkuType.Inapp,
                _ => BillingClient.SkuType.Subs
            };

            var purchasesResult = BillingClient.QueryPurchases(skuType);

            ParseBillingResult(purchasesResult.BillingResult);

            return Task.FromResult(purchasesResult.PurchasesList.Select(p => p.ToIABPurchase()));
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload (can not be null)</param>
        /// <param name="verifyPurchase">Interface to verify purchase</param>
        /// <returns></returns>
        public async override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null)
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
                    return await PurchaseAsync(productId, BillingClient.SkuType.Inapp, verifyPurchase);
                case ItemType.Subscription:

                    var result = BillingClient.IsFeatureSupported(BillingClient.FeatureType.Subscriptions);
                    ParseBillingResult(result);
                    return await PurchaseAsync(productId, BillingClient.SkuType.Subs, verifyPurchase);
            }

            return null;
        }

        async Task<InAppBillingPurchase> PurchaseAsync(string productSku, string itemType, IInAppBillingVerifyPurchase verifyPurchase)
        {            

            var skuDetailsParams = SkuDetailsParams.NewBuilder()
                .SetType(itemType)
                .SetSkusList(new List<string> { productSku })
                .Build();

            var skuDetailsResult = await BillingClient.QuerySkuDetailsAsync(skuDetailsParams);
            ParseBillingResult(skuDetailsResult?.Result);

            var skuDetails = skuDetailsResult.SkuDetails.FirstOrDefault();

            if (skuDetails == null)
                throw new ArgumentException($"{productSku} does not exist");

            var flowParams = BillingFlowParams.NewBuilder()
                .SetSkuDetails(skuDetails)
                .Build();

            tcsPurchase = new TaskCompletionSource<(BillingResult billingResult, IList<Android.BillingClient.Api.Purchase> purchases)>();
            var responseCode = BillingClient.LaunchBillingFlow(Activity, flowParams);
            ParseBillingResult(responseCode);        

            var result = await tcsPurchase.Task;
            ParseBillingResult(result.billingResult);

            //we are only buying 1 thing.
            var androidPurchase = result.purchases?.FirstOrDefault(p => p.Sku == productSku);

            //for some reason the data didn't come back
            if (androidPurchase == null)
            {
                var purchases = await GetPurchasesAsync(itemType == BillingClient.SkuType.Inapp ? ItemType.InAppPurchase : ItemType.Subscription);
                return purchases.FirstOrDefault(p => p.ProductId == productSku);
            }

            var data = androidPurchase.OriginalJson;
            var signature = androidPurchase.Signature;

            var purchase = androidPurchase.ToIABPurchase();
            if (verifyPurchase == null || await verifyPurchase.VerifyPurchase(data, signature, productSku, purchase.Id))
                return purchase;

            return null;
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
        public override async Task<bool> ConsumePurchaseAsync(string productId, string purchaseToken)
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

        bool ParseBillingResult(BillingResult result)
        {
            if(result == null)
                throw new InAppBillingPurchaseException(PurchaseError.GeneralError);

            switch (result.ResponseCode)
            {
                case BillingResponseCode.Ok:
                    return true;
                case BillingResponseCode.UserCancelled:
                    //User Cancelled, should try again
                    throw new InAppBillingPurchaseException(PurchaseError.UserCancelled);
                case BillingResponseCode.ServiceUnavailable:
                    //Network connection is down
                    throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable);
                case BillingResponseCode.ServiceDisconnected:
                    //Network connection is down
                    throw new InAppBillingPurchaseException(PurchaseError.ServiceDisconnected);
                case BillingResponseCode.ServiceTimeout:
                    //Network connection is down
                    throw new InAppBillingPurchaseException(PurchaseError.ServiceTimeout);
                case BillingResponseCode.BillingUnavailable:
                    //Billing Unavailable
                    throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable);
                case BillingResponseCode.ItemNotOwned:
                    //Item not owned
                    throw new InAppBillingPurchaseException(PurchaseError.NotOwned);
                case BillingResponseCode.DeveloperError:
                    //Developer Error
                    throw new InAppBillingPurchaseException(PurchaseError.DeveloperError);
                case BillingResponseCode.Error:
                    //Generic Error
                    throw new InAppBillingPurchaseException(PurchaseError.GeneralError);
                case BillingResponseCode.FeatureNotSupported:
                    throw new InAppBillingPurchaseException(PurchaseError.FeatureNotSupported);

                case BillingResponseCode.ItemAlreadyOwned:
                    throw new InAppBillingPurchaseException(PurchaseError.AlreadyOwned);

                case BillingResponseCode.ItemUnavailable:
                    throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Utility security class to verify the purchases
        /// </summary>
        [Preserve(AllMembers = true)]
        public static class InAppBillingSecurity
        {
            /// <summary>
            /// Verifies the purchase.
            /// </summary>
            /// <returns><c>true</c>, if purchase was verified, <c>false</c> otherwise.</returns>
            /// <param name="publicKey">Public key.</param>
            /// <param name="signedData">Signed data.</param>
            /// <param name="signature">Signature.</param>
            public static bool VerifyPurchase(string publicKey, string signedData, string signature)
            {
                if (signedData == null)
                {
                    Console.WriteLine("Security. data is null");
                    return false;
                }

                if (!string.IsNullOrEmpty(signature))
                {
                    var key = InAppBillingSecurity.GeneratePublicKey(publicKey);
                    var verified = InAppBillingSecurity.Verify(key, signedData, signature);

                    if (!verified)
                    {
                        Console.WriteLine("Security. Signature does not match data.");
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Generates the public key.
            /// </summary>
            /// <returns>The public key.</returns>
            /// <param name="encodedPublicKey">Encoded public key.</param>
            public static IPublicKey GeneratePublicKey(string encodedPublicKey)
            {
                try
                {
                    var keyFactory = KeyFactory.GetInstance(KeyFactoryAlgorithm);
                    return keyFactory.GeneratePublic(new X509EncodedKeySpec(Android.Util.Base64.Decode(encodedPublicKey, 0)));
                }
                catch (NoSuchAlgorithmException e)
                {
                    Console.WriteLine(e.Message);
                    throw new RuntimeException(e);
                }
                catch (Java.Lang.Exception e)
                {
                    Console.WriteLine(e.Message);
                    throw new IllegalArgumentException();
                }
            }

            /// <summary>
            /// Verify the specified publicKey, signedData and signature.
            /// </summary>
            /// <param name="publicKey">Public key.</param>
            /// <param name="signedData">Signed data.</param>
            /// <param name="signature">Signature.</param>
            public static bool Verify(IPublicKey publicKey, string signedData, string signature)
            {
                Console.WriteLine("Signature: {0}", signature);
                try
                {
                    var sign = Signature.GetInstance(SignatureAlgorithm);
                    sign.InitVerify(publicKey);
                    sign.Update(Encoding.UTF8.GetBytes(signedData));

                    if (!sign.Verify(Android.Util.Base64.Decode(signature, 0)))
                    {
                        Console.WriteLine("Security. Signature verification failed.");
                        return false;
                    }

                    return true;
                }
                catch (System.Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                return false;
            }

            /// <summary>
            /// Simple string transform via:
            /// http://stackoverflow.com/questions/11671865/how-to-protect-google-play-public-key-when-doing-inapp-billing
            /// </summary>
            /// <param name="key">key to transform</param>
            /// <param name="i">XOR Offset</param>
            /// <returns></returns>
            public static string TransformString(string key, int i)
            {
                var chars = key.ToCharArray(); ;
                for (var j = 0; j < chars.Length; j++)
                    chars[j] = (char)(chars[j] ^ i);
                return new string(chars);
            }

#pragma warning disable IDE1006 // Naming Styles
            const string KeyFactoryAlgorithm = "RSA";
            const string SignatureAlgorithm = "SHA1withRSA";
#pragma warning restore IDE1006 // Naming Styles

        }
    }
}
 