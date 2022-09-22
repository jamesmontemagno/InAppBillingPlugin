## In-App Billing Plugin for .NET MAUI, Xamarin, and Windows

A simple In-App Purchase plugin for .NET MAUI, Xamarin, and Windows to query item information, purchase items, restore items, and more.

## Important Version Information
* For .NET 6 & .NET MAUI you must use version 6.x+
* For Xamarin.Forms and pre-.NET 6 it is recommended to use version 5.x
* See migration guides below

## Documentation
Get started by reading through the [In-App Billing Plugin documentation](https://jamesmontemagno.github.io/InAppBillingPlugin/).

There are changes in version 4.0 so read below.

Source code reference of non-conumables -> https://github.com/jamesmontemagno/app-ac-islandtracker/blob/master/TurnipTracker/ViewModel/ProViewModel.cs

Full blog on subscriptions: https://montemagno.com/ios-android-subscription-implemenation-strategies/

## NuGet
* NuGet: [Plugin.InAppBilling](https://www.nuget.org/packages/Plugin.InAppBilling) [![NuGet](https://img.shields.io/nuget/v/Plugin.InAppBilling.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.InAppBilling/)

## Platform Support

|Platform|Version|
| ------------------- | :------------------: |
|Xamarin.iOS & iOS for .NET|10+|
|Xamarin.Mac, macOS for .NET, macCatlyst for .NET |All|
|Xamarin.TVOS, tvOS for .NET|10.13.2|
|Xamarin.Android, Android for .NET|21+|
|Windows 10 UWP|10+|
|Windows App SDK (WinUI 3) |10+|
|Xamarin.Forms|All|
|.NET MAUI|All|

### Created By: [@JamesMontemagno](http://github.com/jamesmontemagno)
* Twitter: [@JamesMontemagno](http://twitter.com/jamesmontemagno)
* Blog: [Montemagno.com](http://montemagno.com)
* Podcast: [Merge Conflict](http://mergeconflict.fm)
* Videos: [James's YouTube Channel](https://www.youtube.com/jamesmontemagno) 

### Checkout my podcast on IAP
I co-host a weekly development podcast, [Merge Conflict](http://mergeconflict.fm), about technology and recently covered IAP and this library: 

* [28: Demystifying In-App Purchases](https://www.mergeconflict.fm/57678-merge-conflict-28-demystifying-in-app-purchases)
* [292: Developer Guide to In-App Subscriptions](https://www.mergeconflict.fm/292)

## Version 5 & 6 Major Update
* This version of the plugins now target .NET 6! (Still including support for Xamarin). Versions 5 & 6 are the same source code, but Version 5 doesn't include 6.0. I would recommend this version for Xamarin apps.
* Android: We now use Google Play Billing Version 4.0!
* iOS: Beta - In version 4 we auto finalized all transactions and after testing I decided to keep this feature on in 5/6... you can no turn that off in your iOS application with `InAppBillingImplementation.FinishAllTransactions = false;`. This would be required if you are using consumables and don't want to auto finish. You will need to finalize manually with `FinalizePurchaseAsync`
* All: There are now "Extras" for all products that give you back tons of info for each platform
* Android: `AcknowledgePurchaseAsync` is now `FinalizePurchaseAsync`

## Version 4 Major Update - Android

We now use Xamarin.Essentials for getting access to the current activity. So ensure you [initialize Xamarin.Essentials](https://docs.microsoft.com/xamarin/essentials/get-started?WT.mc_id=friends-0000-jamont) in your Android app. 

Also if you get a null exception the linker is being aggressive so write the following code in your MainActivity:

```csharp
var context = Xamarin.Essentials.Platform.AppContext;
var activity = Xamarin.Essentials.Platform.CurrentActivity;
```

Version 4.X updates to the new Android billing client. This means there are few important changes:
1. You must acknowledge all purchases within 3 days, by calling `AcknowledgePurchaseAsync` or the Consume API if it a consumable.
2. You must hanle Pending Transactions from outside of you app. See [docs from Google](https://developer.android.com/google/play/billing/integrate#pending)
3. `HandleActivityResult` is removed from the API as it is not needed

### Upgrading from 2/3 to 4
* Remove `Plugin.CurrentActivity.CrossCurrentActivity.Current.Activity = this;`
* Remove `InAppBillingImplementation.HandleActivityResult(requestCode, resultCode, data);`
* Change: `await CrossInAppBilling.Current.ConnectAsync(ItemType.InAppPurchase);` to `await CrossInAppBilling.Current.ConnectAsync();`
* Change: `CrossInAppBilling.Current.PurchaseAsync(productId, ItemType.InAppPurchase, payload);` to `CrossInAppBilling.Current.PurchaseAsync(productId, ItemType.InAppPurchase);`

### Pending Transactions:
* If the result of PurchaseAsync is PurchaseState.PaymentPending, store the order details locally and inform the user that they will have access to the product when the payment completes
* When the user starts the app (and/or visits a particular page), if the stored PurchaseState is PaymentPending, call GetPurchasesAsync and query the result for a purchase that matches the stored purchase.
* If the PurchaseState for this purchase is still PaymentPending, show the same no-access message
* If the PurchaseState is Purchased, call ConsumePurchaseAsync or AcknowledgePurchaseAsync, depending on the product type


To respond to pending transactions you can subscribe to a listener in your Android project startup:

```csharp
// Connect to the service here
await CrossInAppBilling.Current.ConnectAsync();

// Check if there are pending orders, if so then subscribe
var purchases = await CrossInAppBilling.Current.GetPurchasesAsync(ItemType.InAppPurchase);

if (purchases?.Any(p => p.State == PurchaseState.PaymentPending) ?? false)
{
  Plugin.InAppBilling.InAppBillingImplementation.OnAndroidPurchasesUpdated = (billingResult, purchases) =>
  {
       // decide what you are going to do here with purchases
       // probably acknowledge
       // probably disconnect
  };
}
else
{
  await CrossInAppBilling.Current.DisconnectAsync();
}
```

If you do connect the `IsConnected` propety will be `true` and when you make purchases or check purchases again you should check ahead of time and not re-connect or disconnect if there are pending purchases

I highly recommend reading the entire [Google Play Billing System docs](https://developer.android.com/google/play/billing/).

#### Consumable vs Non-consumables on Android

On Android if you purchase anything you must first Acknowledge a purchase else it will be refunded. See the android documentation.

https://developer.android.com/google/play/billing/integrate#process
https://developer.android.com/google/play/billing/integrate#pending

> For consumables, the consumeAsync() method fulfills the acknowledgement requirement and indicates that your app has granted entitlement to the user. This method also enables your app to make the one-time product available for purchase again.

So, if you have a consumable... `ConsumePurchaseAsync` will also acknowledge it, if you have a non-consumable you will need to call `AcknowledgePurchaseAsync`.

## Version 4+ Linker Settings

For linking if you are setting **Link All** you may need to add:

#### Android:
```
Plugin.InAppBilling;Xamarin.Android.Google.BillingClient
```

#### iOS:
```
--linkskip=Plugin.InAppBilling
```

### License
The MIT License (MIT), see [LICENSE](LICENSE) file.

### Want To Support This Project?
All I have ever asked is to be active by submitting bugs, features, and sending those pull requests down! Want to go further? Make sure to subscribe to my weekly development podcast [Merge Conflict](http://mergeconflict.fm), where I talk all about awesome Xamarin goodies and you can optionally support the show by becoming a [supporter on Patreon](https://www.patreon.com/mergeconflictfm).

