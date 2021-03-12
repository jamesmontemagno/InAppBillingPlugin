## In-App Billing Plugin for Xamarin and Windows

A simple In-App Purchase plugin for Xamarin and Windows to query item information, purchase items, restore items, and more.

## Documentation
Get started by reading through the [In-App Billing Plugin documentation](https://jamesmontemagno.github.io/InAppBillingPlugin/).

There are changes in version 4.0 so read below.

Source code reference -> https://github.com/jamesmontemagno/app-ac-islandtracker/blob/freemium/TurnipTracker/ViewModel/ProViewModel.cs

## NuGet
* NuGet: [Plugin.InAppBilling](https://www.nuget.org/packages/Plugin.InAppBilling) [![NuGet](https://img.shields.io/nuget/v/Plugin.InAppBilling.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.InAppBilling/)

Dev Feed: https://ci.appveyor.com/nuget/inappbillingplugin

## Build Status
[![Build status](https://ci.appveyor.com/api/projects/status/0tfkgrlq8r2u7wb9?svg=true)](https://ci.appveyor.com/project/JamesMontemagno/inappbillingplugin)

## Platform Support

|Platform|Version|
| ------------------- | :------------------: |
|Xamarin.iOS|iOS 8+|
|tvOS - Apple TV|All|
|Xamarin.Android|API 14+|
|Windows 10 UWP|10+|

### Created By: [@JamesMontemagno](http://github.com/jamesmontemagno)
* Twitter: [@JamesMontemagno](http://twitter.com/jamesmontemagno)
* Blog: [Montemagno.com](http://montemagno.com)
* Podcasts: [Merge Conflict](http://mergeconflict.fm), [Coffeehouse Blunders](http://blunders.fm), [The Xamarin Podcast](http://xamarinpodcast.com)
* Video: [The Xamarin Show on Channel 9](http://xamarinshow.com), [YouTube Channel](https://www.youtube.com/jamesmontemagno) 

### Checkout my podcast on IAP
I co-host a weekly development podcast, [Merge Conflict](http://mergeconflict.fm), about technology and recently covered IAP and this library: [Merge Conflict 28: Demystifying In-App Purchases](http://www.mergeconflict.fm/57678-merge-conflict-28-demystifying-in-app-purchases)

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

I highly recommend reading the entire [Google Play Billing System docs](https://developer.android.com/google/play/billing/).

#### Consumable vs Non-consumables on Android

On Android if you purchase anything you must first Acknowledge a purchase else it will be refunded. See the android documentation.

https://developer.android.com/google/play/billing/integrate#process
https://developer.android.com/google/play/billing/integrate#pending

> For consumables, the consumeAsync() method fulfills the acknowledgement requirement and indicates that your app has granted entitlement to the user. This method also enables your app to make the one-time product available for purchase again.

So, if you have a consumable... `ConsumePurchaseAsync` will also acknowledge it, if you have a non-consumable you will need to call `AcknowledgePurchaseAsync`.

## Version 3+ Linker Settings

For linking if you are setting **Link All** you may need to add:

#### Android:
```
Plugin.InAppBilling;Xamarin.Android.Google.BillingClient;
```

#### iOS:
```
--linkskip=Plugin.InAppBilling
```

### License
The MIT License (MIT), see [LICENSE](LICENSE) file.

### Want To Support This Project?
All I have ever asked is to be active by submitting bugs, features, and sending those pull requests down! Want to go further? Make sure to subscribe to my weekly development podcast [Merge Conflict](http://mergeconflict.fm), where I talk all about awesome Xamarin goodies and you can optionally support the show by becoming a [supporter on Patreon](https://www.patreon.com/mergeconflictfm).

