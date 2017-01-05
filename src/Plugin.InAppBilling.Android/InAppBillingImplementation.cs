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

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class InAppBillingImplementation : IInAppBilling
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

        public InAppBillingImplementation()
        {
            serviceConnection = new InAppBillingServiceConnection(Context);
        }
        
        public string ValidationPublicKey { get; set; }

        public Activity Context => CrossCurrentActivity.Current.Activity;

        InAppBillingServiceConnection serviceConnection;
        static TaskCompletionSource<object> tcsSubscribe;

        public Task<bool> ConnectAsync()
        {
            return serviceConnection.ConnectAsync();
        }

        public Task DisconnectAsync()
        {
            return serviceConnection.DisconnectAsync();
        }

        public Task<Product> GetProductInfoAsync(string productSku, string itemType)
        {
            var getSkuDetailsTask = Task.Factory.StartNew<Product>(() => {

                var querySku = new Bundle();
                querySku.PutStringArrayList(SKU_ITEM_ID_LIST, new string[] { productSku });


                Bundle skuDetails = serviceConnection.Service.GetSkuDetails(3, Context.PackageName, itemType, querySku);

                if (!skuDetails.ContainsKey(SKU_DETAILS_LIST))
                {
                    return null;
                }

                var products = skuDetails.GetStringArrayList(SKU_DETAILS_LIST);

                if (products == null || !products.Any())
                    return null;

                return JsonConvert.DeserializeObject<Product>(products.FirstOrDefault());
            });

            return getSkuDetailsTask;
        }

        public async Task<Purchase> PurchaseAsync(string sku, string itemType, string payload)
        {
            if (tcsSubscribe != null)
                return null;

            tcsSubscribe = new TaskCompletionSource<object>();

            Bundle buyIntentBundle = serviceConnection.Service.GetBuyIntent(3, Context.PackageName, sku, itemType, payload);
            var response = GetResponseCodeFromBundle(buyIntentBundle);

            // 0=OK, 1=UserCancelled, 3=BillingUnavailable, 4=ItemUnavailable, 5=DeveloperError, 6=Error, 7=AlreadyDownloaded
            if (response != 0)
            {
                return null;
            }

            var pendingIntent = buyIntentBundle.GetParcelable(RESPONSE_BUY_INTENT) as PendingIntent;
            if (pendingIntent != null)
                Context.StartIntentSenderForResult(pendingIntent.IntentSender, PURCHASE_REQUEST_CODE, new Intent(), 0, 0, 0);

            await tcsSubscribe.Task;

            var purchases = await GetPurchasesAsync(itemType);

            return purchases.FirstOrDefault(p => p.ProductId == sku && p.DeveloperPayload == payload);
        }

        public static void HandleActivityResult(int requestCode, Result resultCode, Intent data)
        {
            if (PURCHASE_REQUEST_CODE != requestCode || data == null)
            {
                return;
            }

            tcsSubscribe?.TrySetResult(null);
        }

        public Task<List<Purchase>> GetPurchasesAsync(string itemType)
        {
            var getPurchasesTask = Task.Run(() => {
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

                        if (!string.IsNullOrEmpty(ValidationPublicKey) && Security.VerifyPurchase(ValidationPublicKey, data, sign))
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

        public async Task<InAppBillingProduct> GetProductInfoAsync(string productId)
        {
            var p = await GetProductInfoAsync(productId, ITEM_TYPE_SUBSCRIPTION);

            return new InAppBillingProduct
            {
                Name = p.Description,
                LocalizedPrice = p.Price,
                ProductId = p.ProductId
            };
        }

        public async Task<List<InAppBillingPurchase>> GetPurchasesAsync()
        {
            var purchases = await GetPurchasesAsync(ITEM_TYPE_SUBSCRIPTION);

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return purchases.Select(p => new InAppBillingPurchase
            {
                TransactionDateUtc = epoch + TimeSpan.FromMilliseconds(p.PurchaseTime),
                Id = p.OrderId,
                ProductId = p.ProductId,
            }).ToList();
        }

        public async Task<InAppBillingPurchase> SubscribeAsync(string productId)
        {
            var p = await PurchaseAsync(productId, ITEM_TYPE_SUBSCRIPTION, "tfp");

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return new InAppBillingPurchase
            {
                TransactionDateUtc = epoch + TimeSpan.FromMilliseconds(p.PurchaseTime),
                Id = p.OrderId
            };
        }

        class InAppBillingServiceConnection : Java.Lang.Object, IServiceConnection
        {
            public InAppBillingServiceConnection(Context context)
            {
                Context = context;
            }

            TaskCompletionSource<bool> tcsConnect;
            TaskCompletionSource<object> tcsDisconnect;

            public Context Context { get; private set; }
            public IInAppBillingService Service { get; private set; }
            public bool IsConnected { get; private set; }

            public Task<bool> ConnectAsync()
            {
                if (IsConnected)
                    return Task.FromResult(true);

                tcsConnect = new TaskCompletionSource<bool>();
                var serviceIntent = new Intent("com.android.vending.billing.InAppBillingService.BIND");

                if (Context.PackageManager.QueryIntentServices(serviceIntent, 0).Any())
                {
                    Context.BindService(serviceIntent, this, Bind.AutoCreate);
                    return tcsConnect.Task;
                }

                return Task.FromResult(false);
            }

            public async Task DisconnectAsync()
            {
                if (!IsConnected)
                    return;

                tcsDisconnect = new TaskCompletionSource<object>();
                Context.UnbindService(this);

                await tcsDisconnect.Task;
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
                IsConnected = false;
                Service = null;
                tcsDisconnect.SetResult(null);
            }
        }

        public class Product
        {
            public string Title { get; set; }
            public string Price { get; set; }
            public string Type { get; set; }
            public string Description { get; set; }
            public string ProductId { get; set; }

            public override string ToString()
            {
                return string.Format("[Product: Title={0}, Price={1}, Type={2}, Description={3}, ProductId={4}]", Title, Price, Type, Description, ProductId);
            }
        }

        public class Purchase
        {
            public string PackageName { get; set; }
            public string OrderId { get; set; }
            public string ProductId { get; set; }
            public string DeveloperPayload { get; set; }
            public int PurchaseTime { get; set; }
            public int PurchaseState { get; set; }
            public string PurchaseToken { get; set; }

            public override string ToString()
            {
                return string.Format("[Purchase: PackageName={0}, OrderId={1}, ProductId={2}, DeveloperPayload={3}, PurchaseTime={4}, PurchaseState={5}, PurchaseToken={6}]", PackageName, OrderId, ProductId, DeveloperPayload, PurchaseTime, PurchaseState, PurchaseToken);
            }
        }

        /// <summary>
        /// Utility security class to verify the purchases
        /// </summary>
        public sealed class Security
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
                    var key = Security.GeneratePublicKey(publicKey);
                    bool verified = Security.Verify(key, signedData, signature);

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

            const string KeyFactoryAlgorithm = "RSA";
            const string SignatureAlgorithm = "SHA1withRSA";

        }
    }
}