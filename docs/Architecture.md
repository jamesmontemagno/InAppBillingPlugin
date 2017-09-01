
## Architecture

I get a lot of questions about architecture and how to unit tests plugins. So here are some things to be aware of for any plugin that I publish.

### What's with this .Current Global Variable? Why can't I use $FAVORITE_IOC_LIBARY
You totally can! Every plugin I create is based on an interface. The static singleton just gives you a super simple way of gaining access to the platform implementation. Realize that the implementation of the plugin lives in your iOS, Android, Windows, etc. Thies means you will need to register it there by instantiating a `Cross___Implementation` from the platform specific projects.

If you are using a ViewModel/IOC approach your code may look like:

```csharp
public MyViewModel()
{
    readonly IPLUGIN plugin;
    public MyViewModel(IPLUGIN plugin)
    {
        this.plugin = plugin;
    }
}
```

### What About Unit Testing?
To learn about unit testing strategies be sure to read my blog: [Unit Testing Plugins for Xamarin](http://motzcod.es/post/159267241302/unit-testing-plugins-for-xamarin)

## Disposing of In-App Billing Plugin
This plugin also implements IDisposable on all implementations. This ensure that all events are unregistered from the platform. This include unregistering from the SKPaymentQueue on iOS. Only dispose when you need to and are no longer listening to events and you must call `Dispose` on the actual static class. The next time you gain access to the `CrossInAppBilling.Current` a new instance will be created.

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
    CrossInAppBilling.Dispose();
}
```

It is recommended to not us a using statement, but instead just call the single `Dispose` on the static class, which will also dispose the `Current`:

```csharp
public async Task<bool> MakePurchase()
{
    if(!CrossInAppBilling.IsSupported)
        return false;

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
    //This does all the disposing you need
    CrossInAppBilling.Dispose();
}
```


<= Back to [Table of Contents](README.md)
