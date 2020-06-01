using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Newtonsoft.Json;
using Java.Security;
using Java.Security.Spec;
using Java.Lang;
using System.Text;

using Android.Runtime;
using Android.Vending.Billing;
using Android.BillingClient.Api;

[assembly: UsesPermission("com.android.vending.BILLING")]
namespace Plugin.InAppBilling
{
	/// <summary>
	/// Implementation for Feature
	/// </summary>
	[Preserve(AllMembers = true)]
    public class InAppBillingImplementation : BaseInAppBilling, IPurchasesUpdatedListener, IConsumeResponseListener, ISkuDetailsResponseListener, IBillingClientStateListener
    {
		/// <summary>
		/// Gets the context, aka the currently activity.
		/// This is set from the MainApplication.cs file that was laid down by the plugin
		/// </summary>
		/// <value>The context.</value>
		Activity Context =>
            Xamarin.Essentials.Platform.CurrentActivity ?? throw new NullReferenceException("Current Context/Activity is null, ensure that the MainActivity.cs file is configuring Xamarin.Essentials in your source code so the In App Billing can use it.");

        /// <summary>
        /// Default Constructor for In App Billing Implemenation on Android
        /// </summary>
        public InAppBillingImplementation()
        {

        }


        BillingClient BillingClient { get; set; }
        bool IsConnected { get; set; }

        TaskCompletionSource<(BillingResult billingResult, IList<Android.BillingClient.Api.Purchase> purchases)> tcsPurchase;
        TaskCompletionSource<bool> tcsConnect;
        TaskCompletionSource<BillingResult> tcsConsume;
        TaskCompletionSource<(BillingResult billingResult, IList<SkuDetails> skuDetails)> tcsSkuDetailsResponse;

        public void OnPurchasesUpdated(BillingResult billingResult, IList<Android.BillingClient.Api.Purchase> purchases)
        {
            tcsPurchase?.TrySetResult((billingResult, purchases));
        }

        public void OnConsumeResponse(BillingResult billingResult, string p1)
        {
            tcsConsume?.TrySetResult(billingResult);
        }

        public void OnSkuDetailsResponse(BillingResult billingResult, IList<SkuDetails> p1)
        {
            tcsSkuDetailsResponse?.TrySetResult((billingResult, p1));
        }

        public void OnBillingServiceDisconnected()
        {
            IsConnected = false;
        }

        public void OnBillingSetupFinished(BillingResult billingResult)
        {
            Console.WriteLine($"Billing Setup Finished : {billingResult.ResponseCode} - {billingResult.DebugMessage}");
            tcsConnect?.TrySetResult(billingResult.ResponseCode == BillingResponseCode.Ok);
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
            if (BillingClient == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }

            IEnumerable<Product> products = null;
            switch (itemType)
            {
                case ItemType.InAppPurchase:
                    products = await GetProductInfoAsync(productIds, BillingClient.SkuType.Inapp);
                    break;
                case ItemType.Subscription:
                    products = await GetProductInfoAsync(productIds, BillingClient.SkuType.Subs);
                    break;
            }

            if (products == null)
                return null;

            return products.Select(product => new InAppBillingProduct
            {
                Name = product.Title,
                Description = product.Description,
                CurrencyCode = product.CurrencyCode,
                LocalizedPrice = product.Price,
                ProductId = product.ProductId,
                MicrosPrice = product.MicrosPrice,
                LocalizedIntroductoryPrice = product.IntroductoryPrice,
                MicrosIntroductoryPrice = product.IntroductoryPriceAmountMicros
            });
        }

        Task<IEnumerable<Product>> GetProductInfoAsync(string[] productIds, string itemType)
        {
            var getSkuDetailsTask = Task.Factory.StartNew<IEnumerable<Product>>(() =>
            {
                // TODO: Implement
                //var querySku = new Bundle();
                //querySku.PutStringArrayList(SKU_ITEM_ID_LIST, productIds);


                //var skuDetails = serviceConnection.Service.GetSkuDetails(3, Context.PackageName, itemType, querySku);

                //if (!skuDetails.ContainsKey(SKU_DETAILS_LIST))
                //{
                //    return null;
                //}

                //var products = skuDetails.GetStringArrayList(SKU_DETAILS_LIST);

                //if (products == null || !products.Any())
                //    return null;

                //var items = new List<Product>(products.Count);
                //foreach (var item in products)
                //{
                //    items.Add(JsonConvert.DeserializeObject<Product>(item));
                //}
                //return items;
                return null;
            });

            return getSkuDetailsTask;
        }

		protected async override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase, string verifyOnlyProductId)
        {
            if (BillingClient == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }

            List<Purchase> purchases = null;
            switch (itemType)
            {
                case ItemType.InAppPurchase:
                    purchases = await GetPurchasesAsync(BillingClient.SkuType.Inapp, verifyPurchase, verifyOnlyProductId);
                    break;
                case ItemType.Subscription:
                    purchases = await GetPurchasesAsync(BillingClient.SkuType.Subs, verifyPurchase, verifyOnlyProductId);
                    break;
            }

            if (purchases == null)
                return null;

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var results = purchases.Select(p => new InAppBillingPurchase
            {
                TransactionDateUtc = epoch + TimeSpan.FromMilliseconds(p.PurchaseTime),
                Id = p.OrderId,
                ProductId = p.ProductId,
                AutoRenewing = p.AutoRenewing,
                PurchaseToken = p.PurchaseToken,
                State = itemType == ItemType.InAppPurchase ? p.State : p.SubscriptionState,
                ConsumptionState = p.ConsumedState,
                Payload = p.DeveloperPayload ?? string.Empty
            });

            return results;

        }

        Task<List<Purchase>> GetPurchasesAsync(string itemType, IInAppBillingVerifyPurchase verifyPurchase, string verifyOnlyProductId = null)
        {
            var getPurchasesTask = Task.Run(async () =>
            {
                var continuationToken = string.Empty;
                var purchases = new List<Purchase>();

                do
                {
                    // TODO: Implement
                    //               var ownedItems = serviceConnection.Service.GetPurchases(3, Context.PackageName, itemType, null);
                    //               var response = GetResponseCodeFromBundle(ownedItems);

                    //               if (response != 0)
                    //               {
                    //                   break;
                    //               }

                    //               if (!ValidOwnedItems(ownedItems))
                    //               {
                    //                   Console.WriteLine("Invalid purchases");
                    //                   return purchases;
                    //               }

                    //               var items = ownedItems.GetStringArrayList(RESPONSE_IAP_PURCHASE_ITEM_LIST);
                    //               var dataList = ownedItems.GetStringArrayList(RESPONSE_IAP_PURCHASE_DATA_LIST);
                    //               var signatures = ownedItems.GetStringArrayList(RESPONSE_IAP_DATA_SIGNATURE_LIST);

                    //               for (var i = 0; i < items.Count; i++)
                    //               {
                    //                   var data = dataList[i];
                    //                   var sign = signatures[i];

                    //                   var purchase = JsonConvert.DeserializeObject<Purchase>(data);

                    //	if (verifyPurchase == null || (verifyOnlyProductId != null && !verifyOnlyProductId.Equals(purchase.ProductId)))
                    //		purchases.Add(purchase);
                    //	else if (await verifyPurchase.VerifyPurchase(data, sign, purchase.ProductId, purchase.OrderId))
                    //		purchases.Add(purchase);
                    //}

                    //               continuationToken = ownedItems.GetString(RESPONSE_IAP_CONTINUATION_TOKEN);

                } while (!string.IsNullOrWhiteSpace(continuationToken));

                return purchases;
            });

            return getPurchasesTask;
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload (can not be null)</param>
        /// <param name="verifyPurchase">Interface to verify purchase</param>
        /// <returns></returns>
        public async override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            if (BillingClient == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }

            Purchase purchase = null;
            switch (itemType)
            {
                case ItemType.InAppPurchase:
                    purchase = await PurchaseAsync(productId, BillingClient.SkuType.Inapp, payload, verifyPurchase);
                    break;
                case ItemType.Subscription:

                    var result = BillingClient.IsFeatureSupported(BillingClient.FeatureType.Subscriptions);
                    ParseBillingResult(result);
                    purchase = await PurchaseAsync(productId, BillingClient.SkuType.Subs, payload, verifyPurchase);
                    break;
            }

            if (purchase == null)
                return null;

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return new InAppBillingPurchase
            {
                TransactionDateUtc = epoch + TimeSpan.FromMilliseconds(purchase.PurchaseTime),
                Id = purchase.OrderId,
                AutoRenewing = purchase.AutoRenewing,
                PurchaseToken = purchase.PurchaseToken,
                State = itemType == ItemType.InAppPurchase ? purchase.State : purchase.SubscriptionState,
                ConsumptionState = purchase.ConsumedState,
                ProductId = purchase.ProductId,
                Payload = purchase.DeveloperPayload ?? string.Empty
            };
        }

        async Task<Purchase> PurchaseAsync(string productSku, string itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase)
        {
            if (tcsPurchase != null && !tcsPurchase.Task.IsCompleted)
                return null;

            tcsSkuDetailsResponse?.TrySetCanceled();
            tcsSkuDetailsResponse = new TaskCompletionSource<(BillingResult, IList<SkuDetails>)>();

            var skuDetailsParams = SkuDetailsParams.NewBuilder()
                .SetType(itemType)
                .SetSkusList(new List<string> { productSku })
                .Build();
            BillingClient.QuerySkuDetailsAsync(skuDetailsParams, this);

            var skuDetailsResult = await tcsSkuDetailsResponse.Task;
            ParseBillingResult(skuDetailsResult.billingResult);

            var skuDetails = skuDetailsResult.skuDetails.FirstOrDefault();

            var flowParams = BillingFlowParams.NewBuilder()
                .SetSkuDetails(skuDetails)
                .Build();

            tcsPurchase = new TaskCompletionSource<(BillingResult billingResult, IList<Android.BillingClient.Api.Purchase> purchases)>();
            var responseCode = BillingClient.LaunchBillingFlow(Context, flowParams);
            ParseBillingResult(responseCode);        

            var result = await tcsPurchase.Task;
            ParseBillingResult(result.billingResult);

            //we are only buying 1 thing.

            var androidPurchase = result.purchases?.FirstOrDefault(p => p.Sku == productSku);

            // for some reason the data didn't come back
            if (androidPurchase == null)
            {
                var purchases = await GetPurchasesAsync(itemType, verifyPurchase);
                return purchases.FirstOrDefault(p => p.ProductId == productSku);
            }


            var data = androidPurchase.OriginalJson;
            var signature = androidPurchase.Signature;

            var purchase = JsonConvert.DeserializeObject<Purchase>(data);
            if (verifyPurchase == null || await verifyPurchase.VerifyPurchase(data, signature, productSku, purchase.OrderId))
            {
                if (purchase.ProductId == productSku && payload.Equals(purchase.DeveloperPayload ?? string.Empty))
                    return purchase;
            }

            return purchase;

        }

        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        public override Task<bool> ConnectAsync(ItemType itemType = ItemType.InAppPurchase)
        {
            tcsPurchase?.TrySetCanceled();
            tcsPurchase = null;

            tcsConnect?.TrySetCanceled();
            tcsConnect = new TaskCompletionSource<bool>();

            BillingClient = BillingClient.NewBuilder(Context).SetListener(this).Build();
            BillingClient.StartConnection(this);

            return tcsConnect.Task;
        }

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public override Task DisconnectAsync()
        {
            try
            {
                BillingClient?.EndConnection();
                BillingClient?.Dispose();
                BillingClient = null;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to disconned: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        //inapp:{Context.PackageName}:{productSku}

        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="purchaseToken">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        public override async Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string purchaseToken, string payload = null)
        {
            if (BillingClient == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");
            }

            
            var consumeParams = ConsumeParams.NewBuilder()
                .SetPurchaseToken(purchaseToken)
                .SetDeveloperPayload(payload ?? string.Empty)
                .Build();

            tcsConsume?.TrySetCanceled();
            tcsConsume = new TaskCompletionSource<BillingResult>();

            BillingClient.ConsumeAsync(consumeParams, this);

            var result = await tcsConsume.Task;

            ParseBillingResult(result);            

            var purchase = new InAppBillingPurchase
            {
                Id = string.Empty,
                PurchaseToken = purchaseToken,
                State = PurchaseState.Purchased,
                ConsumptionState = ConsumptionState.Consumed,
                AutoRenewing = false,
                Payload = payload,
                ProductId = productId,
                TransactionDateUtc = DateTime.UtcNow
            };

            return purchase;
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
        /// Consume a purchase
        /// </summary>
        /// <param name="productId">Id/Sku of the product</param>
        /// <param name="payload">Developer specific payload of original purchase</param>
        /// <param name="itemType">Type of product being consumed.</param>
        /// <param name="verifyPurchase">Verify Purchase implementation</param>
        /// <returns>If consumed successful</returns>
        public async override Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase)
        {
            if (BillingClient == null)
                throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable, "You are not connected to the Google Play App store.");


            if (payload == null)
                throw new ArgumentNullException(nameof(payload), "Payload can not be null");

            var purchases = await GetPurchasesAsync(itemType, verifyPurchase);

            var purchase = purchases.FirstOrDefault(p => p.ProductId == productId && p.Payload == payload && p.ConsumptionState == ConsumptionState.NoYetConsumed);

			if(purchase == null)
			{
				purchase = purchases.FirstOrDefault(p => p.ProductId == productId && p.Payload == payload);
			}

            if (purchase == null)
            {
                Console.WriteLine("Unable to find a purchase with matching product id and payload");
                return null;
            }

            // TODO: Implement
            //var response = serviceConnection.Service.ConsumePurchase(3, Context.PackageName, purchase.PurchaseToken);
            //var result = ParseConsumeResult(response);
            //if (!result)
                return null;

            //return purchase;
        }


        [Preserve(AllMembers = true)]
        class PurchaseResponse
        {
            public string PurchaseData { get; set; }
            public string DataSignature { get; set; }
        }




   //     [Preserve(AllMembers = true)]
   //     class InAppBillingServiceConnection : Java.Lang.Object, IServiceConnection
   //     {
			//ItemType itemType = ItemType.InAppPurchase;

			//public InAppBillingServiceConnection(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
   //         {
   //             Context = Application.Context;
   //         }

   //         public InAppBillingServiceConnection(ItemType itemType = ItemType.InAppPurchase)
   //         {
   //             Context = Application.Context;
			//	this.itemType = itemType;

			//}

   //         public InAppBillingServiceConnection(Context context, ItemType itemType = ItemType.InAppPurchase)
   //         {
   //             Context = context;
			//	this.itemType = itemType;

			//}

   //         TaskCompletionSource<bool> tcsConnect;

   //         public Context Context { get; private set; }
   //         public IInAppBillingService Service { get; private set; }
   //         public bool IsConnected { get; private set; }

   //         public Task<bool> ConnectAsync()
   //         {
   //             if (IsConnected)
   //                 return Task.FromResult(true);

   //             tcsConnect = new TaskCompletionSource<bool>();
   //             var serviceIntent = new Intent("com.android.vending.billing.InAppBillingService.BIND");

			//	if(serviceIntent == null)
			//		return Task.FromResult(false);

			//	serviceIntent.SetPackage("com.android.vending");

   //             if (Context.PackageManager.QueryIntentServices(serviceIntent, 0).Any())
   //             {
   //                 Context.BindService(serviceIntent, this, Bind.AutoCreate);
   //                 return tcsConnect?.Task ?? Task.FromResult(false);
   //             }

   //             return Task.FromResult(false);
   //         }

   //         /// <summary>
   //         /// Disconnect from payment service
   //         /// </summary>
   //         /// <returns></returns>
   //         public Task DisconnectAsync()
   //         {
   //             if (!IsConnected)
   //                 return Task.CompletedTask;

   //             Context?.UnbindService(this);

   //             IsConnected = false;
   //             Service = null;
   //             return Task.CompletedTask;
   //         }

   //         public void OnServiceConnected(ComponentName name, IBinder service)
   //         {
   //             Service = InAppBillingServiceStub.AsInterface(service);

			//	if (Service == null || Context == null)
			//	{
			//		tcsConnect?.TrySetResult(false);
			//		return;
			//	}

			//	var pkgName = Context.PackageName;

			//	var type = itemType == ItemType.Subscription ? ITEM_TYPE_SUBSCRIPTION : ITEM_TYPE_INAPP;

			//	try
			//	{
			//		if (Service.IsBillingSupported(3, pkgName, type) == 0)
			//		{
			//			IsConnected = true;
			//			tcsConnect?.TrySetResult(true);
			//			return;
			//		}
			//	}
			//	catch(System.Exception ex)
			//	{
			//		Console.WriteLine("Unable to check if billing is supported: " + ex.Message);
			//	}

   //             tcsConnect?.TrySetResult(false);
   //         }

   //         public void OnServiceDisconnected(ComponentName name)
   //         {

   //         }
   //     }
        [Preserve(AllMembers = true)]
        class Product
        {
            [JsonConstructor]
            public Product()
            {

            }

            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }


            [JsonProperty(PropertyName = "price")]
            public string Price { get; set; }


            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; }


            [JsonProperty(PropertyName = "description")]
            public string Description { get; set; }


            [JsonProperty(PropertyName = "productId")]
            public string ProductId { get; set; }

            [JsonProperty(PropertyName = "price_currency_code")]
            public string CurrencyCode { get; set; }

            [JsonProperty(PropertyName = "price_amount_micros")]
            public Int64 MicrosPrice { get; set; }

            [JsonProperty(PropertyName = "introductoryPrice")]
            public string IntroductoryPrice { get; set; }

            // 0 is default if this property is not set
            [JsonProperty(PropertyName = "introductoryPriceAmountMicros")]
            public Int64 IntroductoryPriceAmountMicros { get; set; }

            [JsonProperty(PropertyName = "introductoryPricePeriod")]
            public string IntroductoryPricePeriod { get; set; }

            [JsonProperty(PropertyName = "introductoryPriceCycles")]
            public int IntroductoryPriceCycles { get; set; }

            public override string ToString() {
                return string.Format("[Product: Title={0}, Price={1}, Type={2}, Description={3}, ProductId={4}, CurrencyCode={5}, MicrosPrice={6}, IntroductoryPrice={7}, IntroductoryPriceAmountMicros={8}, IntroductoryPricePeriod={9}, IntroductoryPriceCycles={10}]", Title, Price, Type, Description, ProductId, CurrencyCode, MicrosPrice, IntroductoryPrice, IntroductoryPriceAmountMicros, IntroductoryPricePeriod, IntroductoryPriceCycles);
            }
        }

        [Preserve(AllMembers = true)]
        class Purchase
        {
            [JsonConstructor]
            public Purchase()
            {

            }


            [JsonProperty(PropertyName = "autoRenewing")]
            public bool AutoRenewing { get; set; }

            [JsonProperty(PropertyName = "packageName")]
            public string PackageName { get; set; }


            [JsonProperty(PropertyName = "orderId")]
            public string OrderId { get; set; }

            [JsonProperty(PropertyName = "productId")]
            public string ProductId { get; set; }


            [JsonProperty(PropertyName = "developerPayload")]
            public string DeveloperPayload { get; set; }


            [JsonProperty(PropertyName = "purchaseTime")]
            public Int64 PurchaseTime { get; set; }


            /// <summary>
            /// purchase state
            /// </summary>
            [JsonProperty(PropertyName = "purchaseState")]
            public int PurchaseState { get; set; }


            [JsonProperty(PropertyName = "purchaseToken")]
            public string PurchaseToken { get; set; }

            [JsonProperty(PropertyName = "consumptionState")]
            public int ConsumptionState { get; set; }


            /// <summary>
            /// for subscriptions
            /// </summary>
            [JsonProperty(PropertyName = "paymentState")]
            public int PaymentState { get; set; }


            [JsonIgnore]
            public PurchaseState State
            {
                get
                {
                    if (PurchaseState == 0)
                        return InAppBilling.PurchaseState.Purchased;
                    else if (PurchaseState == 1)
                        return InAppBilling.PurchaseState.Canceled;
                    else if (PurchaseState == 2)
                        return InAppBilling.PurchaseState.Refunded;

                    return InAppBilling.PurchaseState.Unknown;
                }
            }

            [JsonIgnore]
            public ConsumptionState ConsumedState => ConsumptionState == 0 ? InAppBilling.ConsumptionState.NoYetConsumed : InAppBilling.ConsumptionState.Consumed;

            [JsonIgnore]
            public PurchaseState SubscriptionState
            {
                get
                {
                    if (PaymentState == 0)
                        return InAppBilling.PurchaseState.PaymentPending;
                    else if (PaymentState == 1)
                        return InAppBilling.PurchaseState.Purchased;
                    else if (PaymentState == 2)
                        return InAppBilling.PurchaseState.FreeTrial;

                    return InAppBilling.PurchaseState.Unknown;
                }
            }

            public override string ToString()
            {
                return string.Format("[Purchase: PackageName={0}, OrderId={1}, ProductId={2}, DeveloperPayload={3}, PurchaseTime={4}, PurchaseState={5}, PurchaseToken={6}]", PackageName, OrderId, ProductId, DeveloperPayload, PurchaseTime, PurchaseState, PurchaseToken);
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

            const string KeyFactoryAlgorithm = "RSA";
            const string SignatureAlgorithm = "SHA1withRSA";

        }
    }
}