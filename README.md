## In-App Billing Plugin for Xamarin

# This is a work in progress, not ready for production use.

Simple cross platform in app purchase plugin for Xamarin.iOS and Xamarin.Android

### Setup
* Available on NuGet: https://www.nuget.org/packages/Plugin.InAppBilling [![NuGet](https://img.shields.io/nuget/v/Xam.Plugins.InAppBilling.svg?label=NuGet)](https://www.nuget.org/packages/Xam.Plugins.InAppBilling/)
* Install into your PCL project and Client projects.

Build status: [![Build status](https://ci.appveyor.com/api/projects/status/0tfkgrlq8r2u7wb9?svg=true)](https://ci.appveyor.com/project/JamesMontemagno/inappbillingplugin)

**Platform Support**

|Platform|Supported|Version|
| ------------------- | :-----------: | :------------------: |
|Xamarin.iOS|Yes|iOS 8+|
|Xamarin.Android|Yes|API 14+|
|Windows Phone Silverlight|No||
|Windows Phone RT|No||
|Windows Store RT|No||
|Windows 10 UWP|No||
|Xamarin.Mac|No||

## Make a purchase
You must have your IAP setup before testing the code:

```csharp
try
{
    var productId = "mysku";

    CrossInAppBilling.Current.ValidationPublicKey = "GOOGLE_PLAY_API_KEY";
    var connected = await CrossInAppBilling.Current.ConnectAsync();

    if (!connected)
    {
        //Couldn't connect
        return;
    }

    //try to purchase item
    var purchase = await CrossInAppBilling.Current.PurchaseAsync(productId, ItemType.InAppPurchase, "apppayload");
	if(purchase == null)
	{
		//Not purchased
	}
	else
	{
		//Purchased!
	}
}
catch (Exception ex)
{

}
finally
{
    busy = false;
    await CrossInAppBilling.Current.DisconnectAsync();
}
```


## Check purchase status
You can easily check the status of any number of skus by scanning the purchased items for the app/user.
```csharp
try
{ 
	var productId = "mysku";

    CrossInAppBilling.Current.ValidationPublicKey = "KEY_FROM_GOOGLE_PLAY";
    var connected = await CrossInAppBilling.Current.ConnectAsync();

    if (!connected)
    {
        //Couldn't connect
        return;
    }

    //check purchases

    var purchases = await CrossInAppBilling.Current.GetPurchasesAsync(ItemType.InAppPurchase);

    if(purchases?.Any(p => p.ProductId == productId) ?? false)
    {
        //Purchase restored
    }
    else
    {
        //no purchases found
    }
    
    await CrossInAppBilling.Current.DisconnectAsync();
}
catch (Exception ex)
{
    //Something has gone wrong
}
finally
{
    busy = false;
}
```

### Get Product Information
This is helpful to get translated pricing to display to your users.

```csharp
```

## Android Setup
It is important to follow these steps for Android:

Prerequisite

* Read the [Android developer In App Billing API Docs](https://developer.android.com/google/play/billing/api.html)
* Ensure app is on app store and In App Purchase is setup
* You must place this code in your Main/Base Activity where you will be requesting purchases from.
```csharp
protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
{
    base.OnActivityResult(requestCode, resultCode, data);
    InAppBillingImplementation.HandleActivityResult(requestCode, resultCode, data);
}
```

#### Android Testing Help
* Ensure you have app in Alpha/Beta with the NuGet installed. This will add "com.android.vending.BILLING" permission for you
* Create an IAB product, make sure it is **published** and **active**
* Add a test account to the app, ensure it is the main account on device, and that account is opted-in as tester
* Validated your version code and number in your development environment match what is in the Play store.
* You MUST sign the APK even in debug mode. In XS this is in the properties. In VS you must manually add this to your project:

```
<AndroidKeyStore>True</AndroidKeyStore>
<AndroidSigningKeyStore>KeystoreLocation</AndroidSigningKeyStore>
<AndroidSigningStorePass>PASS</AndroidSigningStorePass>
<AndroidSigningKeyAlias>ALIAS</AndroidSigningKeyAlias>
<AndroidSigningKeyPass>PASS</AndroidSigningKeyPass>
```

#### Android Troubleshooing
* If you see "You need to sign into your google account". This most likely means that you don't have an items published and active for IAB
* If you see "This version of the application is not configured for billing through Google Play": This means the versions number don't match or you don't have the app configured to sign correctly with your keystore.
* If you see "The publisher cannot purchase this item": This means you are trying to buy it on your developer account, and that isn't allowed, you need a different account.


#### License
Under MIT, see LICENSE file.
