using Plugin.InAppBilling;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace InAppBillingTests
{

    public partial class MainPage : ContentPage
	{
        public ObservableCollection<InAppBillingProduct> Items { get; set; } = new ObservableCollection<InAppBillingProduct>();
		public MainPage()
		{
			InitializeComponent();
		}

        PurchaseStorage purchaseStorage = new PurchaseStorage();

        const string consumableProductId = "consumabletest";
        const string nonConsumableProductId = "iaptest";

        async void ButtonConsumable_Clicked(object sender, EventArgs e)
		{
            var shouldSucceed = sender != ButtonConsumableBroken;
            try
            {
                var purchase = await PurchaseProduct(consumableProductId, ItemType.InAppPurchaseConsumable, shouldSucceed);

                if (purchase == null)
                {
                    await DisplayAlert(string.Empty, "Did not purchase", "OK");
                }
                else
                {
                    await DisplayAlert(string.Empty, "We did it!", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(string.Empty, "Did not purchase: " + ex.Message, "OK");
                Console.WriteLine(ex);
            }
        }

        async void FinaliseConsumable_Clicked(System.Object sender, System.EventArgs e)
        {
            var purchase = purchaseStorage.GetFailedPurchase();
            if (purchase == null)
            {
                await DisplayAlert(string.Empty, "No failed purchase to finalise", "OK");
                return;
            }

            try
            {
                await CrossInAppBilling.Current.ConnectAsync();
                await CrossInAppBilling.Current.ConsumePurchaseAsync(purchase.ProductId, purchase.PurchaseToken, purchase.Id);
                await DisplayAlert(string.Empty, "We did it!", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert(string.Empty, "Did not finalise purchase: " + ex.Message, "OK");
                Console.WriteLine(ex);
            }
            finally
            {
                await CrossInAppBilling.Current.DisconnectAsync();
            }
        }

        async void ButtonNonConsumable_Clicked(object sender, EventArgs e)
		{
			var id = nonConsumableProductId;
			try
			{
                await CrossInAppBilling.Current.ConnectAsync();
				var purchase = await CrossInAppBilling.Current.PurchaseAsync(id, ItemType.InAppPurchase);


				if (purchase == null)
				{
					await DisplayAlert(string.Empty, "Did not purchase", "OK");
				}
				else
				{
                    if (!purchase.IsAcknowledged && Device.RuntimePlatform == Device.Android)
                        await CrossInAppBilling.Current.AcknowledgePurchaseAsync(purchase.PurchaseToken);
					await DisplayAlert(string.Empty, "We did it!", "OK");
				}
			}
			catch (Exception ex)
			{
                await DisplayAlert(string.Empty, "Did not purchase: " + ex.Message, "OK");
                Console.WriteLine(ex);
			}
            finally
            {

                await CrossInAppBilling.Current.DisconnectAsync();
            }
		}

		async void ButtonSub_Clicked(object sender, EventArgs e)
		{
			var id = "renewsub";
			try
            {
                await CrossInAppBilling.Current.ConnectAsync();
                var purchase = await CrossInAppBilling.Current.PurchaseAsync(id, ItemType.Subscription);

				if(purchase == null)
				{
					await DisplayAlert(string.Empty, "Did not purchase", "OK");
				}
				else
                {
                    if (!purchase.IsAcknowledged && Device.RuntimePlatform == Device.Android)
                        await CrossInAppBilling.Current.AcknowledgePurchaseAsync(purchase.PurchaseToken);
                    await DisplayAlert(string.Empty, "We did it!", "OK");
				}
			}
			catch (Exception ex)
			{
                await DisplayAlert(string.Empty, "Did not purchase: " + ex.Message, "OK");
                Console.WriteLine(ex);
            }
            finally
            {

                await CrossInAppBilling.Current.DisconnectAsync();
            }
        }

		async void ButtonRenewingSub_Clicked(object sender, EventArgs e)
		{
			
		}

        async void ButtonProductInfo_Clicked(object sender, EventArgs e)
        {
            try
            {
                await CrossInAppBilling.Current.ConnectAsync();
                var items = await CrossInAppBilling.Current.GetProductInfoAsync(ItemType.InAppPurchase, nonConsumableProductId);
                Items.Clear();
                foreach (var item in items)
                    Items.Add(item);
            }
            catch (Exception ex)
            {
                await DisplayAlert(string.Empty, "Did not purchase: " + ex.Message, "OK");
                Console.WriteLine(ex);
            }
            finally
            {

                await CrossInAppBilling.Current.DisconnectAsync();
            }
        }

        async void ButtonRestore_Clicked(object sender, EventArgs e)
		{
			try
            {
                await CrossInAppBilling.Current.ConnectAsync();
                var purchases = await CrossInAppBilling.Current.GetPurchasesAsync(ItemType.Subscription);

				if (purchases == null)
				{
					await DisplayAlert(string.Empty, "Did not purchase", "OK");
				}
				else
				{
					await DisplayAlert(string.Empty, "We did it!", "OK");
				}
			}
			catch (Exception ex)
			{

				Console.WriteLine(ex);
            }
            finally
            {

                await CrossInAppBilling.Current.DisconnectAsync();
            }
        }

        async Task<bool> ProcessPurchase(string receiptData, string productId, string transactionId, bool shouldSucceed)
        {
            // Real code should do something here
            // It would return true on success
            // But it could throw or return false
            return shouldSucceed;
        }

        async Task<InAppBillingPurchase> PurchaseProduct(string productId, ItemType purchaseType, bool shouldSucceed)
        {

            var billing = CrossInAppBilling.Current;
            try
            {
                var connectionResult = await billing.ConnectAsync();
                if (!connectionResult)
                {
                    throw new Exception("Could not connect to billing service. Please try again later.");
                }

                var purchase = await billing.PurchaseAsync(productId, purchaseType);

                if (purchase == null)
                {
                    throw new Exception("Purchase could not be processed. Please try again later.");
                }

                // Verify the purchase
                var verified = await ProcessPurchase(billing.ReceiptData, productId, purchase.Id, shouldSucceed);
                if (!verified)
                {
                    purchaseStorage.SetFailedPurchase(purchase);
                    throw new Exception("Purchase could not be completed. Please try again later.");
                }

                if (purchaseType == ItemType.InAppPurchaseConsumable)
                {
                    await billing.ConsumePurchaseAsync(productId, purchase.PurchaseToken, purchase.Id);
                }
                else
                {
                    await billing.AcknowledgePurchaseAsync(purchase.PurchaseToken);
                }

                // If we got this far, everything's gravy!
                Debug.WriteLine("Purchase was a success!");

                return purchase;
            }
            catch (InAppBillingPurchaseException purchaseEx)
            {
                if (purchaseEx.PurchaseError == PurchaseError.UserCancelled)
                {
                    Debug.WriteLine("Purchase was cancelled");
                    return null;
                }
                else
                {
                    Debug.WriteLine($"Purchase error: {purchaseEx}");
                }
                throw purchaseEx;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex}");
                throw ex;
            }
            finally
            {
                await billing.DisconnectAsync();
            }
        }

        
    }
}
