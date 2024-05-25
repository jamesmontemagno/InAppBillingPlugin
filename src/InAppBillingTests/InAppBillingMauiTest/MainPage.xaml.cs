using Plugin.InAppBilling;
using System.Collections.ObjectModel;

namespace InAppBillingTests
{
    public partial class MainPage : ContentPage
	{
        public ObservableCollection<InAppBillingProduct> Items { get; } = new();
		public MainPage()
		{
			InitializeComponent();
		}

		private void ButtonConsumable_Clicked(object sender, EventArgs e)
		{

		}

		private async void ButtonNonConsumable_Clicked(object sender, EventArgs e)
		{
			var id = "iaptest";
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
#if ANDROID
                    if (purchase.IsAcknowledged == false)
                        await CrossInAppBilling.Current.FinalizePurchaseAsync([purchase.PurchaseToken]);
#endif
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

		private async void ButtonSub_Clicked(object sender, EventArgs e)
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
#if ANDROID
                    if (purchase.IsAcknowledged  == false)
                        await CrossInAppBilling.Current.FinalizePurchaseAsync([purchase.PurchaseToken]);
#endif
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

		private async void ButtonRenewingSub_Clicked(object sender, EventArgs e)
		{
			
		}

        private async void ButtonProductInfo_Clicked(object sender, EventArgs e)
        {
            try
            {
                await CrossInAppBilling.Current.ConnectAsync();
                var items = await CrossInAppBilling.Current.GetProductInfoAsync(ItemType.InAppPurchase, ["iaptest"]);
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

        private async void ButtonRestore_Clicked(object sender, EventArgs e)
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
	}
}
