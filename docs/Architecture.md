
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


<= Back to [Table of Contents](README.md)