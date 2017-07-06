# Getting Started

## Setup
* NuGet: [Plugin.InAppBilling](http://www.nuget.org/packages/Plugin.InAppBilling) [![NuGet](https://img.shields.io/nuget/v/Plugin.InAppBilling.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.InAppBilling/)
* `PM> Install-Package Plugin.InAppBilling`
* Install into ALL of your projects, include client projects.


## Using Connectivity APIs
It is drop dead simple to gain access to the In-App Billing APIs in any project. All you need to do is get a reference to the current instance of IInAppBilling via `CrossInAppBilling.Current`. Before making any calls to InAppBilling you must use `ConnectAsync` to ensure a valid connection to the app store of the device and always ensure that you call `DisconnectAsync` when you are finished. It is recommended to call `DisconnectAsync` inside of a finally block.

```csharp
public async Task<bool> MakePurchase()
{
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

## Disposing of In-App Billing Plugin
This plugin also implements IDisposable on all implementations. This ensure that all events are unregistered from the platform. This include unregistering from the SKPaymentQueue on iOS. Only dispose when you need to and are no longer listening to events. The next time you gain access to the `CrossInAppBilling.Current` a new instance will be created.

```csharp
public async Task<bool> MakePurchase()
{
    if(!CrossInAppBilling.IsSupported)
        return false;

    using(var billing = CrossInAppBilling.Current)
    {
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


## Permissions & Additional Setup Considerations

### Android:

The `com.android.vending.BILLING` permission is required to use In-App Billing on Android and this library ill automatically added it your Android Manifest when you compile. No need to add them manually!

You must place this code in your Main/Base Activity where you will be requesting purchases from.

```csharp
protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
{
    base.OnActivityResult(requestCode, resultCode, data);
    InAppBillingImplementation.HandleActivityResult(requestCode, resultCode, data);
}
```

## Architecture

### What's with this .Current Global Variable? Why can't I use $FAVORITE_IOC_LIBARY
You totally can! Every plugin I create is based on an interface. The static singleton just gives you a super simple way of gaining access to the platform implementation. If you are looking to use Depenency Injector or Inversion of Control (IoC) you will need to gain access to a reference of `IInAppBilling`. 

This is what your code may look like when using this approach:

```csharp
public MyViewModel()
{
    readonly IInAppBilling billing;
    public MyViewModel(IInAppBilling billing)
    {
        this.billing = billing;
    }

    public async Task<bool> MakePurchase()
    {
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
}
```

Remember that the implementation of the plugin lives in the platform specific applications, which means you will need to register .Current (or instantiate your own CrossInAppBillingImplementation) in your IoC container as the implementation of `IInAppBilling` on each platform. This registration must happen from your application binary, not from your portable/netstandard class library.

### What About Unit Testing?
To learn about unit testing strategies be sure to read my blog: [Unit Testing Plugins for Xamarin](http://motzcod.es/post/159267241302/unit-testing-plugins-for-xamarin)


<= Back to [Table of Contents](README.md)