## In-App Billing Plugin for .NET MAUI  and Windows

> **Project status:** Android and Windows are actively maintained and track the current Google Play Billing Library and Microsoft Store APIs. iOS and Mac keep working as they are today and are not getting new features; they still use the original StoreKit APIs. Contributions are welcome, and you can always fork the library or pull the source into your project.

A simple In-App Purchase plugin for .NET MAUI and Windows to query item information, purchase items, restore items, and more.

Subscriptions are supported on iOS, Android, Mac, and Windows.

## Important Version Information
* Windows now uses `Windows.Services.Store` and supports WinUI 3 and Windows App SDK apps, subscriptions, and consumables. This changes some Windows behavior, see the [migration notes](docs/Windows.md#migrating-from-earlier-versions)
* v10.1 uses Android Billing v9 and requires Android API 23+ (see [AndroidX versions](#androidx-versions-for-android-billing-v9))
* v10 now supports .NET 10+ and Android Billing v8
* v9 now supports .NET 9+ and Android Billing v7
* v8 now supports .NET 8+ .NET MAUI and Windows Apps.
* v7 now supports .NET 6+, .NET MAUI, UWP, and Xamarin/Xamarin.Forms projects
* v7 is built against Android Billing Library 6.0
* See migration guides below

## Documentation
Get started by reading through the [In-App Billing Plugin documentation](https://jamesmontemagno.github.io/InAppBillingPlugin/).

## NuGet
* NuGet: [Plugin.InAppBilling](https://www.nuget.org/packages/Plugin.InAppBilling) [![NuGet](https://img.shields.io/nuget/v/Plugin.InAppBilling.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.InAppBilling/)

## Platform Support

|Platform|Version|
| ------------------- | :------------------: |
|iOS for .NET|10+|
|macCatlyst for .NET |All|
|Android for .NET|23+|
|Windows App SDK (WinUI 3) |10+|
|.NET MAUI|All|

### AndroidX versions for Android Billing v9
Android Billing v9 needs newer AndroidX libraries than .NET MAUI 10 brings in, so restoring can fail with `NU1107` on `Xamarin.AndroidX.Lifecycle.*`. If it does, reference these from your app:

```xml
<ItemGroup Condition="$(TargetFramework.Contains('-android'))">
  <PackageReference Include="Xamarin.AndroidX.Activity.Ktx" Version="1.13.0.1" />
  <PackageReference Include="Xamarin.AndroidX.Collection.Ktx" Version="1.6.0.1" />
  <PackageReference Include="Xamarin.AndroidX.Fragment.Ktx" Version="1.9.1" />
  <PackageReference Include="Xamarin.AndroidX.Lifecycle.LiveData" Version="2.11.0.1" />
  <PackageReference Include="Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx" Version="2.11.0.1" />
  <PackageReference Include="Xamarin.AndroidX.Lifecycle.Process" Version="2.11.0.1" />
  <PackageReference Include="Xamarin.AndroidX.Lifecycle.Runtime.Ktx" Version="2.11.0.1" />
  <PackageReference Include="Xamarin.AndroidX.Lifecycle.Runtime.Ktx.Android" Version="2.11.0.1" />
  <PackageReference Include="Xamarin.AndroidX.Lifecycle.ViewModel.Ktx" Version="2.11.0.1" />
  <PackageReference Include="Xamarin.AndroidX.SavedState.SavedState.Ktx" Version="1.5.0.1" />
</ItemGroup>
```

### Created By: [@JamesMontemagno](http://github.com/jamesmontemagno)
* Twitter: [@JamesMontemagno](http://twitter.com/jamesmontemagno)
* Blog: [Montemagno.com](http://montemagno.com)
* Podcast: [Merge Conflict](http://mergeconflict.fm)
* Videos: [James's YouTube Channel](https://www.youtube.com/jamesmontemagno) 

## Version 8 Major Updates
* Nice new re-write and re-implementation of APIs!
* Built against .NET 8
* Now using latest Android Billing v6.2.1

## Version 7 Major Updates
* Android: You must compile and target against Android 12 or higher  (or Android 13 if v7.1)
* Android: Now using Android Billing v6
* Android: Major changes to Android product details, subscriptions, and more
* Android: Please read through: https://developer.android.com/google/play/billing/migrate-gpblv6
* Android: `AcknowledgePurchaseAsync` is now `FinalizePurchaseAsync`
* All: There are now "Extras" for all products that give you back tons of info for each platform


If you receive an error in Google Play you may need to add this to your AndroidManifest.xml inside the application node:

```xml
<meta-data
  android:name="com.google.android.play.billingclient.version"
  android:value="X.X.X" />
```

Update X.X.X with the version of the Billing Library that is a dependency on the NuGet package.

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

#### Android Progaurd Rules

```
-keep class com.android.billingclient.api.** { *; }
-keep class com.android.vending.billing.** { *; }
```

#### iOS:
```
--linkskip=Plugin.InAppBilling
```

### License
The MIT License (MIT), see [LICENSE](LICENSE) file.

### Want To Support This Project?
All I have ever asked is to be active by submitting bugs, features, and sending those pull requests down! Want to go further? Make sure to subscribe to my weekly development podcast [Merge Conflict](http://mergeconflict.fm), where I talk all about awesome Xamarin goodies and you can optionally support the show by becoming a [supporter on Patreon](https://www.patreon.com/mergeconflictfm).

