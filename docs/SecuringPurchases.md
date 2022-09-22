## Securing In-App Purchases (Receipt Validation)

Each platform handles security of In-App Purchases a bit different. To handle this whenever you make a purchase request or a request where validation needs to be done there is an optional parameter that takes in a `IInAppBillingVerifyPurchase`. 

## Recommended Reading:
* [Xamarin.iOS Securing Purchases Documentation](https://developer.xamarin.com/guides/ios/platform_features/in-app_purchasing/transactions_and_verification/#Securing_Purchases)
* [Google Play service Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html)




Based on the platform this data is a bit different and is returned as part of the InAppBillingPurchase:

### iOS
* [Full Receipt as a string in Base 64 Encoding](https://developer.apple.com/library/content/releasenotes/General/ValidateAppStoreReceipt/Introduction.html#//apple_ref/doc/uid/TP40010573) and is available through `CrossInAppBilling.Current.ReceiptData` .
* signature: Always empty string

### Android
* Signature, OriginalJson, DeveloperPayload can all be used.
* See https://developer.android.com/google/play/billing/developer-payload for more information

### UWP
No additional authentication is provided.


### Android Security
I recommend reading the [Google Play services Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html) that will walk you through your options on storing your public key. 

## Server Side Validation
Not only should your receipt be verified in the app, but ideally it should be verified on a server. I leave this in your hands to add the server side validation by reading Apple and Google's documentation. I provide everything you need in the `VerifyPurchase` method to handle verification.

For examples of implementation I would recommend reading a great blog series by Jonathan Peppers:

* [Securing Xamarin.iOS In-App Purchases with Azure Functions](http://jonathanpeppers.com/Blog/securing-in-app-purchases-for-xamarin-with-azure-functions)
* [Securing Xamarin.Android In-App Purchases with Azure Functions](http://jonathanpeppers.com/Blog/securing-google-play-in-app-purchases-for-xamarin-with-azure-functions)


<= Back to [Table of Contents](README.md)
