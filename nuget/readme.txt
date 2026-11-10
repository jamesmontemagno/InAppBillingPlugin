In-App Billing Plugin for .NET MAUI

> Project status: Android and Windows are actively maintained and track the current Google Play Billing Library and Microsoft Store APIs. iOS and Mac Catalyst use StoreKit 2, while native macOS still uses the original StoreKit APIs. Contributions are welcome, and you can always fork the library or pull the source into your project.

* iOS and Mac Catalyst now default to StoreKit 2 and require iOS/Mac Catalyst 15+ (macOS 12+ for Catalyst). Purchase tokens and restore behavior have changed. A legacy StoreKit opt-out is available, see docs/StoreKit2.md.

* Windows now uses Windows.Services.Store and supports WinUI 3 and Windows App SDK apps, subscriptions, and consumables. This changes some Windows behavior, see docs/Windows.md in the repository.
* v10.1 uses Android Billing v9 and requires Android API 23+. If restore fails with NU1107 on Xamarin.AndroidX.Lifecycle packages, see the README for the AndroidX versions to reference.
* v10 now supports .NET 10+ and Android Billing v8

Version 9.0+ - .NET 9+
1. Built against Android Billing v7

Version 8.0+ - .NET 8+
1. Updated APIs and you must target .NET 8

Version 7.0+  - Major Android updates
1.) You must compile and target against Android 12 or higher
2.) Android: Now using Android Billing v6
3.) Android: Major changes to Android product details, subscriptions, and more

Please read through: https://developer.android.com/google/play/billing/migrate-gpblv6


Version 5.0+ has significant updates!
1.) We have removed IInAppBillingVerifyPurchase from all methods. All data required to handle this yourself is returned.
2.) iOS ReceiptURL data is avaialble via ReceiptData
3.) We are now using Android Billing version 4
4.) Major breaking chanages across the API including AcknowledgePurchaseAsync being changed to FinalizePurchaseAsync
5.) Tons of new APIs when you get information about products

Please read documentation for all changes at https://github.com/jamesmontemagno/InAppBillingPlugin

Version 4.0 has significant updates.

1.) You must compile and target against Android 10 or higher
2.) On Android you must handle pending transactions and call `FinalizePurchaseAsync` when done
3.) On Android HandleActivityResult has been removed.
4.) We now use Xamarin.Essentials and setup is required per docs.

Find the latest setup guides, documentation, and testing instructions at: 
https://github.com/jamesmontemagno/InAppBillingPlugin
