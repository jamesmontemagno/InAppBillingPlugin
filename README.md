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
}
catch (Exception ex)
{
    //Something has gone wrong
}
finally
{
    busy = false;
    await CrossInAppBilling.Current.DisconnectAsync();
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




#### License
Under MIT, see LICENSE file.
