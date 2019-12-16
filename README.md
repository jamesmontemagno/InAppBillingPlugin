## In-App Billing Plugin for Xamarin and Windows

A simple In-App Purchase plugin for Xamarin and Windows to query item information, purchase items, restore items, and more.

## Documentation
Get started by reading through the [In-App Billing Plugin documentation](https://jamesmontemagno.github.io/InAppBillingPlugin/).

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

## Version 3 Linker Settings

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

