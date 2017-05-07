using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Newtonsoft.Json;
using Com.Android.Vending.Billing;
using Java.Security;
using Java.Security.Spec;
using Java.Lang;
using System.Text;

using Plugin.InAppBilling.Abstractions;
using Plugin.CurrentActivity;
using Android.Runtime;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    [Preserve(AllMembers = true)]
    public class InAppBillingImplementation : BaseInAppBilling
    {
        const string SKU_DETAILS_LIST = "DETAILS_LIST";
        const string SKU_ITEM_ID_LIST = "ITEM_ID_LIST";

        const string ITEM_TYPE_INAPP = "inapp";
        const string ITEM_TYPE_SUBSCRIPTION = "subs";

        const string RESPONSE_CODE = "RESPONSE_CODE";
        const string RESPONSE_BUY_INTENT = "BUY_INTENT";
        const string RESPONSE_IAP_DATA = "INAPP_PURCHASE_DATA";
        const string RESPONSE_IAP_DATA_SIGNATURE = "INAPP_DATA_SIGNATURE";
        const string RESPONSE_IAP_DATA_SIGNATURE_LIST = "INAPP_DATA_SIGNATURE_LIST";
        const string RESPONSE_IAP_PURCHASE_ITEM_LIST = "INAPP_PURCHASE_ITEM_LIST";
        const string RESPONSE_IAP_PURCHASE_DATA_LIST = "INAPP_PURCHASE_DATA_LIST";
        const string RESPONSE_IAP_CONTINUATION_TOKEN = "INAPP_CONTINUATION_TOKEN";

        const int PURCHASE_REQUEST_CODE = 1001;

        Activity Context => CrossCurrentActivity.Current.Activity;
        
        /// <summary>
        /// Default Constructor for In App Billing Implemenation on Android
        /// </summary>
        public InAppBillingImplementation()
        {
            
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
            if (serviceConnection.Service == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable, "You are not connected to the Google Play App store.");
            }

            IEnumerable <Product> products = null;
            switch (itemType)
            {
                case ItemType.InAppPurchase:
                    products = await GetProductInfoAsync(productIds, ITEM_TYPE_INAPP);
                    break;
                case ItemType.Subscription:
                    products = await GetProductInfoAsync(productIds, ITEM_TYPE_SUBSCRIPTION);
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
                MicrosPrice = product.MicrosPrice
            });
        }

        Task<IEnumerable<Product>> GetProductInfoAsync(string[] productIds, string itemType)
        {
            var getSkuDetailsTask = Task.Factory.StartNew<IEnumerable<Product>>(() =>
            {

                var querySku = new Bundle();
                querySku.PutStringArrayList(SKU_ITEM_ID_LIST, productIds);


                Bundle skuDetails = serviceConnection.Service.GetSkuDetails(3, Context.PackageName, itemType, querySku);

                if (!skuDetails.ContainsKey(SKU_DETAILS_LIST))
                {
                    return null;
                }

                var products = skuDetails.GetStringArrayList(SKU_DETAILS_LIST);

                if (products == null || !products.Any())
                    return null;

                var items = new List<Product>(products.Count);
                foreach (var item in products)
                {
                    items.Add(JsonConvert.DeserializeObject<Product>(item));
                }
                return items;
            });

            return getSkuDetailsTask;
        }

        /// <summary>
        /// Get all current purhcase for a specifiy product type.
        /// </summary>
        /// <param name="itemType">Type of product</param>
        /// <param name="verifyPurchase">Interface to verify purchase</param>
        /// <returns>The current purchases</returns>
        public async override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            if (serviceConnection.Service == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable, "You are not connected to the Google Play App store.");
            }

            List<Purchase> purchases = null;
            switch (itemType)
            {
                case ItemType.InAppPurchase:
                    purchases = await GetPurchasesAsync(ITEM_TYPE_INAPP, verifyPurchase);
                    break;
                case ItemType.Subscription:
                    purchases = await GetPurchasesAsync(ITEM_TYPE_SUBSCRIPTION, verifyPurchase);
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
                State = p.State,
                Payload = p.DeveloperPayload
            });

            return results;

        }

        Task<List<Purchase>> GetPurchasesAsync(string itemType, IInAppBillingVerifyPurchase verifyPurchase)
        {
            var getPurchasesTask = Task.Run(async () =>
            {
                string continuationToken = string.Empty;
                var purchases = new List<Purchase>();

                do
                {

                    Bundle ownedItems = serviceConnection.Service.GetPurchases(3, Context.PackageName, itemType, null);
                    var response = GetResponseCodeFromBundle(ownedItems);

                    if (response != 0)
                    {
                        break;
                    }

                    if (!ValidOwnedItems(ownedItems))
                    {
                        Console.WriteLine("Invalid purchases");
                        return purchases;
                    }

                    var items = ownedItems.GetStringArrayList(RESPONSE_IAP_PURCHASE_ITEM_LIST);
                    var dataList = ownedItems.GetStringArrayList(RESPONSE_IAP_PURCHASE_DATA_LIST);
                    var signatures = ownedItems.GetStringArrayList(RESPONSE_IAP_DATA_SIGNATURE_LIST);

                    for (int i = 0; i < items.Count; i++)
                    {
                        string data = dataList[i];
                        string sign = signatures[i];

                        if (verifyPurchase == null || await verifyPurchase.VerifyPurchase(data, sign))
                        {
                            var purchase = JsonConvert.DeserializeObject<Purchase>(data);
                            purchases.Add(purchase);
                        }
                    }

                    continuationToken = ownedItems.GetString(RESPONSE_IAP_CONTINUATION_TOKEN);

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
        /// <param name="payload">Developer specific payload</param>
        /// <param name="verifyPurchase">Interface to verify purchase</param>
        /// <returns></returns>
        public async override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
            payload = payload ?? string.Empty;

            if(serviceConnection.Service == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable, "You are not connected to the Google Play App store.");
            }

            Purchase purchase = null;
            switch (itemType)
            {
                case ItemType.InAppPurchase:
                    purchase = await PurchaseAsync(productId, ITEM_TYPE_INAPP, payload, verifyPurchase);
                    break;
                case ItemType.Subscription:
                    purchase = await PurchaseAsync(productId, ITEM_TYPE_SUBSCRIPTION, payload, verifyPurchase);
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
                State = purchase.State,
                ProductId = purchase.ProductId,
                Payload = purchase.DeveloperPayload
            };
        }

        async Task<Purchase> PurchaseAsync(string productSku, string itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase)
        {
            if (tcsPurchase != null && !tcsPurchase.Task.IsCompleted)
                return null;

            tcsPurchase = new TaskCompletionSource<PurchaseResponse>();
           
            Bundle buyIntentBundle = serviceConnection.Service.GetBuyIntent(3, Context.PackageName, productSku, itemType, payload);
            var response = GetResponseCodeFromBundle(buyIntentBundle);
            

            switch(response)
            {
                case 0:
                    //OK to purchase
                    break;
                case 1:
                    //User Cancelled, should try again
                    throw new InAppBillingPurchaseException(PurchaseError.UserCancelled); 
                case 3:
                    //Billing Unavailable
                    throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable);
                case 4:
                    //Item Unavailable
                    throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable);
                case 5:
                    //Developer Error
                    throw new InAppBillingPurchaseException(PurchaseError.DeveloperError);
                case 6:
                    //Generic Error
                    throw new InAppBillingPurchaseException(PurchaseError.GeneralError);
                case 7:
                    var purchases = await GetPurchasesAsync(itemType, verifyPurchase);

                    var purchase = purchases.FirstOrDefault(p => p.ProductId == productSku && p.DeveloperPayload == payload);

                    return purchase;
                    //already purchased
            }

            var pendingIntent = buyIntentBundle.GetParcelable(RESPONSE_BUY_INTENT) as PendingIntent;
            if (pendingIntent != null)
                Context.StartIntentSenderForResult(pendingIntent.IntentSender, PURCHASE_REQUEST_CODE, new Intent(), 0, 0, 0);

            var result = await tcsPurchase.Task;

            if (result == null)
                return null;



            var data = result.PurchaseData;
            var sign = result.DataSignature;

            //for some reason the data didn't come back
            if (string.IsNullOrWhiteSpace(data))
            {
                var purchases = await GetPurchasesAsync(itemType, verifyPurchase);

                var purchase = purchases.FirstOrDefault(p => p.ProductId == productSku && payload.Equals(p.DeveloperPayload ?? string.Empty));

                return purchase;
            }


            if (verifyPurchase == null || await verifyPurchase.VerifyPurchase(data, sign))
            {
                var purchase = JsonConvert.DeserializeObject<Purchase>(data);
                if (purchase.ProductId == productSku && payload.Equals(purchase.DeveloperPayload ?? string.Empty))
                    return purchase;
            }

            return null;

        }

        /// <summary>
        /// Connect to billing service
        /// </summary>
        /// <returns>If Success</returns>
        public override Task<bool> ConnectAsync()
        {
            serviceConnection = new InAppBillingServiceConnection(Context);
            return serviceConnection.ConnectAsync();
        }

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public async override Task DisconnectAsync()
        {
            try
            {
                if (serviceConnection == null)
                    return;

                await serviceConnection.DisconnectAsync();
                serviceConnection.Dispose();
                serviceConnection = null;
            }
            catch(System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unable to disconned: {ex.Message}");
            }
        }

        //inapp:{Context.PackageName}:{productSku}

        /// <summary>
        /// Consume a purchase with a purchase token.
        /// </summary>
        /// <param name="productId">Id or Sku of product</param>
        /// <param name="purchaseToken">Original Purchase Token</param>
        /// <returns>If consumed successful</returns>
        public override Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string purchaseToken)
        {
            if (serviceConnection.Service == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable, "You are not connected to the Google Play App store.");
            }

            var response = serviceConnection.Service.ConsumePurchase(3, Context.PackageName, purchaseToken);
            var result = ParseConsumeResult(response);
            if (!result)
                return null;

            var purchase = new InAppBillingPurchase
            {
                Id = string.Empty,
                PurchaseToken = purchaseToken,
                State = PurchaseState.Purchased,
                AutoRenewing = false,
                Payload = string.Empty,
                ProductId = productId,
                TransactionDateUtc = DateTime.UtcNow
            };

            return Task.FromResult(purchase);
        }

        bool ParseConsumeResult(int response)
        {
            switch (response)
            {
                case 0:
                    return true;
                case 3:
                    //Billing Unavailable
                    throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable);
                case 4:
                    //Item Unavailable
                    throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable);
                case 5:
                    //Developer Error
                    throw new InAppBillingPurchaseException(PurchaseError.DeveloperError);
                case 6:
                    //Generic Error
                    throw new InAppBillingPurchaseException(PurchaseError.GeneralError);
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
            if (serviceConnection.Service == null)
            {
                throw new InAppBillingPurchaseException(PurchaseError.BillingUnavailable, "You are not connected to the Google Play App store.");
            }

            var purchases = await GetPurchasesAsync(itemType, verifyPurchase);

            var purchase = purchases.FirstOrDefault(p => p.ProductId == productId && p.Payload == payload);

            if(purchase == null)
            {
                Console.WriteLine("Unable to find a purchase with matching product id and payload");
                return null;
            }

            var response = serviceConnection.Service.ConsumePurchase(3, Context.PackageName, purchase.PurchaseToken);
            var result = ParseConsumeResult(response);
            if (!result)
                return null;

            return purchase;
        }

        /// <summary>
        /// Must override handle activity and pass back results here.
        /// </summary>
        /// <param name="requestCode"></param>
        /// <param name="resultCode"></param>
        /// <param name="data"></param>
        public static void HandleActivityResult(int requestCode, Result resultCode, Intent data)
        {

            if (PURCHASE_REQUEST_CODE != requestCode || data == null)
            {
                return;
            }

            int responseCode = data.GetIntExtra(RESPONSE_CODE, 0);

            //Reponse returned OK
            if (responseCode == 0)
            {
                var purchaseData = data.GetStringExtra(RESPONSE_IAP_DATA);
                var dataSignature = data.GetStringExtra(RESPONSE_IAP_DATA_SIGNATURE);

                tcsPurchase?.TrySetResult(new PurchaseResponse
                {
                    PurchaseData = purchaseData,
                    DataSignature = dataSignature
                });

            }
            else
            {
                tcsPurchase?.TrySetResult(null);
            }

        }

        [Preserve(AllMembers = true)]
        class PurchaseResponse
        {
            public string PurchaseData { get; set; }
            public string DataSignature { get; set; }
        }


        InAppBillingServiceConnection serviceConnection;
        static TaskCompletionSource<PurchaseResponse> tcsPurchase;
        
        static bool ValidOwnedItems(Bundle purchased)
        {
            return purchased.ContainsKey(RESPONSE_IAP_PURCHASE_ITEM_LIST)
                && purchased.ContainsKey(RESPONSE_IAP_PURCHASE_DATA_LIST)
                && purchased.ContainsKey(RESPONSE_IAP_DATA_SIGNATURE_LIST);
        }

        static int GetResponseCodeFromBundle(Bundle bunble)
        {
            object response = bunble.Get(RESPONSE_CODE);
            if (response == null)
            {
                //Bundle with null response code, assuming OK (known issue)
                return 0;
            }
            if (response is Number)
            {
                return ((Java.Lang.Number)response).IntValue();
            }
            return 6; // Unknown error
        }

        [Preserve(AllMembers = true)]
        class InAppBillingServiceConnection : Java.Lang.Object, IServiceConnection
        {
            public InAppBillingServiceConnection(Context context)
            {
                Context = context;
            }

            TaskCompletionSource<bool> tcsConnect;
           
            public Context Context { get; private set; }
            public IInAppBillingService Service { get; private set; }
            public bool IsConnected { get; private set; }

            public Task<bool> ConnectAsync()
            {
                if (IsConnected)
                    return Task.FromResult(true);

                tcsConnect = new TaskCompletionSource<bool>();
                var serviceIntent = new Intent("com.android.vending.billing.InAppBillingService.BIND");

                serviceIntent.SetPackage("com.android.vending");

                if (Context.PackageManager.QueryIntentServices(serviceIntent, 0).Any())
                {
                    Context.BindService(serviceIntent, this, Bind.AutoCreate);
                    return tcsConnect.Task;
                }

                return Task.FromResult(false);
            }

            /// <summary>
            /// Disconnect from payment service
            /// </summary>
            /// <returns></returns>
            public Task DisconnectAsync()
            {
                if (!IsConnected)
                    return Task.CompletedTask;
                
                Context.UnbindService(this);

                IsConnected = false;
                Service = null;
                return Task.CompletedTask;
            }

            public void OnServiceConnected(ComponentName name, IBinder service)
            {
                Service = IInAppBillingServiceStub.AsInterface(service);

                var pkgName = Context.PackageName;

                if (Service.IsBillingSupported(3, pkgName, ITEM_TYPE_SUBSCRIPTION) == 0)
                {
                    IsConnected = true;
                    tcsConnect.TrySetResult(true);
                    return;
                }

                tcsConnect.TrySetResult(false);
            }

            public void OnServiceDisconnected(ComponentName name)
            {
               
            }
        }
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

            [JsonProperty(PropertyName ="price_currency_code")]
            public string CurrencyCode { get; set; }

            [JsonProperty(PropertyName = "price_amount_micros")]
            public Int64 MicrosPrice { get; set; }

            public override string ToString()
            {
                return string.Format("[Product: Title={0}, Price={1}, Type={2}, Description={3}, ProductId={4}]", Title, Price, Type, Description, ProductId);
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

            [JsonProperty(PropertyName="packageName")]
            public string PackageName { get; set; }


            [JsonProperty(PropertyName = "orderId")]
            public string OrderId { get; set; }

            [JsonProperty(PropertyName ="productId")]
            public string ProductId { get; set; }


            [JsonProperty(PropertyName = "developerPayload")]
            public string DeveloperPayload { get; set; }


            [JsonProperty(PropertyName = "purchaseTime")]
            public Int64 PurchaseTime { get; set; }


            [JsonProperty(PropertyName = "purchaseState")]
            public int PurchaseState { get; set; }


            [JsonProperty(PropertyName = "purchaseToken")]
            public string PurchaseToken { get; set; }


            [JsonIgnore]
            public PurchaseState State
            {
                get
                {
                    if (PurchaseState == 0)
                        return Abstractions.PurchaseState.Purchased;
                    else if (PurchaseState == 1)
                        return Abstractions.PurchaseState.Canceled;
                    else if (PurchaseState == 2)
                        return Abstractions.PurchaseState.Refunded;

                    return Abstractions.PurchaseState.Unknown;
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
                    bool verified = InAppBillingSecurity.Verify(key, signedData, signature);

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
                var chars = key.ToCharArray();;
                for (int j = 0; j < chars.Length; j++)
                    chars[j] = (char)(chars[j] ^ i);
                return new string(chars);
            }

            const string KeyFactoryAlgorithm = "RSA";
            const string SignatureAlgorithm = "SHA1withRSA";

        }
    }
}