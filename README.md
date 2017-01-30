## In-App Billing Plugin for Xamarin and Windows

Simple cross platform in app purchase plugin for Xamarin.iOS and Xamarin.Android

### Setup
* Please use test NuGet feed: https://ci.appveyor.com/nuget/inappbillingplugin

* Available on NuGet in Future: https://www.nuget.org/packages/Plugin.InAppBilling [![NuGet](https://img.shields.io/nuget/v/Plugin.InAppBilling.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.InAppBilling/)
* Install into your PCL project and Client projects.

Build status: [![Build status](https://ci.appveyor.com/api/projects/status/0tfkgrlq8r2u7wb9?svg=true)](https://ci.appveyor.com/project/JamesMontemagno/inappbillingplugin)

**Platform Support**

|Platform|Version|
| ------------------- | :------------------: |
|Xamarin.iOS|iOS 8+|
|Xamarin.Android|API 14+|
|Windows 10 UWP|10+ (beta)|

## Setup
Android only: You must place this code in your Main/Base Activity where you will be requesting purchases from.
```csharp
protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
{
    base.OnActivityResult(requestCode, resultCode, data);
    InAppBillingImplementation.HandleActivityResult(requestCode, resultCode, data);
}
```

Before making a purchase you must call ```ConnectAsync()``` and then call ```DisconnectAsync()``` before attempting to make another connection. This is used specifically for Android to connect to the billing APIs. It will return a boolean that you can check to see if it was successful. I recommend putting all In App Billing calls in a try/catch/finally as it is highly likely that exceptions could occure during testing and production. Best practice is to call  ```DisconnectAsync()``` in the ```finally```.


## Understanding IInAppBillingVerifyPurchase
There are several calls that take a ```IInAppBillingVerifyPurchase``` as a parameter. This is currently only used in Android projects to do validation with Google Play Services. See the Android Security section below for more inforamtion.

## Make a purchase
You must have your IAP setup before testing the code:

```csharp
try
{
    var productId = "mysku";

    var connected = await CrossInAppBilling.Current.ConnectAsync();

    if (!connected)
    {
        //Couldn't connect to billing
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
    //Disconnect, it is okay if we never connected, this will never throw an exception
    await CrossInAppBilling.Current.DisconnectAsync();
}
```




## Check purchase status
You can easily check the status of any number of skus by scanning the purchased items for the app/user.
```csharp
try
{ 
	var productId = "mysku";
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
    await CrossInAppBilling.Current.DisconnectAsync();
}
```

### Get Product Information
This is helpful to get translated pricing to display to your users.

```csharp
try
{ 
    var productIds = new string []{"mysku","mysku2"};
    var connected = await CrossInAppBilling.Current.ConnectAsync();

    if (!connected)
    {
        //Couldn't connect
        return;
    }

    //check purchases

    var items = await CrossInAppBilling.Current.GetProductInfoAsync(ItemType.InAppPurchase, productIds);

    foreach(var item in items)
    {
        //item info here.
    }
}
catch (Exception ex)
{
    //Something has gone wrong
}
finally
{    
    await CrossInAppBilling.Current.DisconnectAsync();
}
```

## Consumables
Consumable purchases have a separate API and work a bit different on each platform:

* Android: Must be purchased first and then consumed, recommended to pass in a purchaseToken from the purchase.
* iOS: Works the same as a purchase, purchased and you track consumption in code
* UWP: Must be purchased first and then consumed.


```csharp
try
{ 
    var productId = "consumable_coins_500";
    var connected = await CrossInAppBilling.Current.ConnectAsync();

    if (!connected)
    {
        //Couldn't connect
        return;
    }

    //check purchases
    //You can use device infor plugin or Xamarin.Forms to check for device

    var purchase = await CrossInAppBilling.Current.PurchaseAsync(productId, ItemType.InAppPurchase, "apppayload");
    if(purchase == null)
    {
        //Not purchased
        return;
    }
    else
    {
        //Purchased now we can consume
    }

    //If iOS we are done, else try to consume
    if(Device.OS == TargetPlatform.iOS)
        return;
        
    var consumedItem = await CrossInAppBilling.Current.ConsumePurchaseAsync(purchase.ProductId, purchase.PurchaseToken);

    if(consumedItem != null)
    {
        //Consumed!!
    }

}
catch (Exception ex)
{
    //Something has gone wrong
}
finally
{    
    await CrossInAppBilling.Current.DisconnectAsync();
}
```

There is another method that you can call that doesn't take in the token. This will find the first purchased item by that Id and attempt to consume it.

## iOS Setup
* Read the iOS developer [In App Purchases API Docs](https://developer.apple.com/in-app-purchase/)
* Read all parts of the [setup from Xamarin documentation](https://developer.xamarin.com/guides/ios/application_fundamentals/in-app_purchasing/part_1_-_in-app_purchase_basics_and_configuration/), which are great.
* You must setup an in app purchase and understand what each of them are.
* Read through the [testing documentation](https://developer.apple.com/library/content/documentation/LanguagesUtilities/Conceptual/iTunesConnectInAppPurchase_Guide/Chapters/TestingInAppPurchases.html#//apple_ref/doc/uid/TP40013727-CH4-SW1)

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

### Android Security
I recommend reading the [Google Play services Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html) that will walk you through your options on storing your public key. InAppBilling Pluging offers Android developers an additional interface, **IInAppBillingVerifyPurchase** to implement to verify the purchase with their public key and helper methods to encrypt and decrypt. It is recommended to atleast follow the XOR guidance if you do not want to setup a verification server.

IInAppBillingVerifyPurchase has 1 Method: **Task<bool> VerifyPurchase(string signedData, string signature)**. It returns a boolean that validates that the signed data and signature match based on the public key.  If you pass in null to the purchase or get purchases methods no verification will be done.

The simplest and easiest (not necessarily the most secure) way is to do the following:

* Take your public key and break into 3 parts
* Run each through the helper XOR method: Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString
* Save each value out and put them in your app
* Implement the interface with this funcationality:

```
    public class Verify : IInAppBillingVerifyPurchase
    {
        const string key1 = @"XOR_key1";
        const string key2 = @"XOR_key2";
        const string key3 = @"XOR_key3";

        public Task<bool> VerifyPurchase(string signedData, string signature)
        {

#if __ANDROID__
            var key1Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key1, 1);
            var key2Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key2, 2);
            var key3Transform = Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.TransformString(key3, 3);
            
            return Task.FromResult(Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.VerifyPurchase(key1Transform + key2Transform + key3Transform, signedData, signature));
#else
            return Task.FromResult(true);
#endif
        }
    }
```
Plugin.InAppBilling.InAppBillingImplementation.InAppBillingSecurity.VerifyPurchase takes in your public key which you now have reversed back to standard and will do proper RSA validation on the signed data.

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


## UWP Setup
* Read the UWP developer [In App Purchases API Docs](https://msdn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials)
* You must setup an in app purchase
* Read through the [testing documentation](https://msdn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials#testing)

#### UWP Testing
In UWP, in-app purchases get can be tested by using the `CurrentAppSimulator` class instead of `CurrentApp`. 

To switch the UWP's `InAppBillingImplementation` to testing mode, set the `InTestingMode`  boolean property.

```csharp
CrossInAppBilling.Current.InTestingMode = true;
```

#### License
Under MIT, see LICENSE file.
