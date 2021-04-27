# Getting Started

## Setup
* NuGet: [Plugin.InAppBilling](http://www.nuget.org/packages/Plugin.InAppBilling) [![NuGet](https://img.shields.io/nuget/v/Plugin.InAppBilling.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.InAppBilling/)
* `PM> Install-Package Plugin.InAppBilling`
* Install into ALL of your projects, include client projects.


## Using InAppBilling APIs
It is drop dead simple to gain access to the In-App Billing APIs in any project. All you need to do is get a reference to the current instance of IInAppBilling via `CrossInAppBilling.Current`. Before making any calls to InAppBilling you must use `ConnectAsync` to ensure a valid connection to the app store of the device and always ensure that you call `DisconnectAsync` when you are finished. It is recommended to call `DisconnectAsync` inside of a finally block.

```csharp
public async Task<bool> MakePurchase()
{
    var billing = CrossInAppBilling.Current;
    try
    {
        var connected = await billing.ConnectAsync();
        if(!connected)
            return false;
        
        //make additional billing calls
    }
    finally
    {
        await billing.DisconnectAsync();
    }
}
```



There may be instances where you install a plugin into a platform that it isn't supported yet. This means you will have access to the interface, but no implementation exists. You can make a simple check before calling any API to see if it is supported on the platform where the code is running. This if nifty when unit testing:

```csharp
public async Task<bool> MakePurchase()
{
    if(!CrossInAppBilling.IsSupported)
        return false;

    try
    {
        var billing = CrossInAppBilling.Current;
        var connected = await billing.ConnectAsync();
        if(!connected)
            return false;
        
        //make additional billing calls
    
    }
    finally
    {
        await billing.DisconnectAsync();
    }
}
```

## In-App Billing Recommended Reading
Due to the complex nature of In-App Billing I highly recommend reading exactly how they work on each platform before you start using this plugin:

* [Apple's iOS/tvOS Documentation](https://developer.apple.com/in-app-purchase/)
* [Android Documentation](https://developer.android.com/google/play/billing/billing_integrate.html)
* [Microsoft's UWP Documentation](https://docs.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials)

In addition to this core reading I recommend the following:
* [Xamarin.iOS Setup Documentation](https://developer.xamarin.com/guides/ios/application_fundamentals/in-app_purchasing/part_1_-_in-app_purchase_basics_and_configuration/)
* [Google Play service Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html)

## Creating an In-App Purchase
Each app store has you create them in a different area.

* Apple: Go to iTunes Connect -> Select App -> Features -> In-App Purchases
* Android: Go to Google Play Console -> Select App -> Store presence -> In-app products (you can only create on if you have uploaded a version of your app with this plugin or the Vending permission set).
* Microsoft: Go to Dashboard -> Select App -> Add-ons

## Permissions & Additional Setup

## iOS
iOS also has the ability to make In-App Purchases from the app store if you mark them as so. To support this you must open your `AppDelegate` and add the following to your `FinishedLaunching`:

```csharp
Plugin.InAppBilling.InAppBillingImplementation.OnShouldAddStorePayment = OnShouldAddStorePayment;
var current = Plugin.InAppBilling.CrossInAppBilling.Current; //initializes
```

Then add this method in the `AppDelegate`:

```csharp
 bool OnShouldAddStorePayment(SKPaymentQueue queue, SKPayment payment, SKProduct product)
{
    //Process and check purchases
    return true;
}
```

## Android

In version 4 we use Xamarin.Essentials so you must ensure you initialize it in your Android project. It is setup by default in new projects:

```csharp
protected override void OnCreate(Bundle savedInstanceState) {
    //...
    base.OnCreate(savedInstanceState);
    Xamarin.Essentials.Platform.Init(this, savedInstanceState); // add this line to your code, it may also be called: bundle
    //...
```

The `com.android.vending.BILLING` permission is required to use In-App Billing on Android and this library will automatically added it your Android Manifest when you compile. No need to add them manually!


<= Back to [Table of Contents](README.md)
