## Securing In-App Purchases (Receipt Validation)

Each platform handles security of In-App Purchases a bit different. To handle this whenever you make a purchase you should use the date from the purchase to validate on your backend.

## Recommended Reading:
* [iOS Securing Purchases Documentation](https://developer.xamarin.com/guides/ios/platform_features/in-app_purchasing/transactions_and_verification/#Securing_Purchases)
* [Google Play service Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html)




Based on the platform this data is a bit different and is returned as part of the InAppBillingPurchase:

### iOS and Mac Catalyst
* `PurchaseToken` is the transaction signed by the App Store as a JWS. StoreKit 2 has already verified it on the device, and transactions that fail are not returned as purchases. Verify it on your server for stronger protection.
* `OriginalJson` is the JSON representation of the transaction.
* `CrossInAppBilling.Current.ReceiptData` still returns the Base 64 app receipt, but Apple has deprecated receipts.
* signature: Always empty string

### Android
* Signature, OriginalJson, DeveloperPayload can all be used.
* See https://developer.android.com/google/play/billing/developer-payload for more information

### Windows
The Microsoft Store has no client-side receipt. `ReceiptData` is empty, and `Signature` is not set. Instead:

* `OriginalJson` contains the Store's collection data for the purchase, including `transactionId` and `orderId`.
* To verify a purchase on your server, use the [Microsoft Store collection API](https://learn.microsoft.com/windows/uwp/monetize/view-and-grant-products-from-a-service). Get the required ID key on the device with `GetStoreContext().GetCustomerCollectionsIdAsync(serviceTicket, publisherUserId)` from `InAppBillingImplementation`.


### Android Security
I recommend reading the [Google Play services Security and Design](https://developer.android.com/google/play/billing/billing_best_practices.html) that will walk you through your options on storing your public key. 

## Server Side Validation
Not only should your receipt be verified in the app, but ideally it should be verified on a server. I leave this in your hands to add the server side validation by reading Apple and Google's documentation. I provide everything you need in the `VerifyPurchase` method to handle verification.

For examples of implementation I would recommend reading a great blog series by Jonathan Peppers:

* [Securing Xamarin.iOS In-App Purchases with Azure Functions](http://jonathanpeppers.com/Blog/securing-in-app-purchases-for-xamarin-with-azure-functions)
* [Securing Xamarin.Android In-App Purchases with Azure Functions](http://jonathanpeppers.com/Blog/securing-google-play-in-app-purchases-for-xamarin-with-azure-functions)


<= Back to [Table of Contents](README.md)
