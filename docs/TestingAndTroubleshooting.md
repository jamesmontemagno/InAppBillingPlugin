## Testing and Troubleshooting

Integrating and testing In-App Purchases is not an easy task and should go through a lot of testing. I have attempted to simplify the integration part, but the app store side is a bit trickier. Here are my tips and tricks on each platform:


## iOS Testing & Troubleshooting
* Read the iOS developer [In App Purchases API Docs](https://developer.apple.com/in-app-purchase/)
* Read all parts of the [setup from Xamarin documentation](https://developer.xamarin.com/guides/ios/application_fundamentals/in-app_purchasing/part_1_-_in-app_purchase_basics_and_configuration/), which are great.
* You must setup an in app purchase and understand what each of them are.
* Read through the [testing documentation](https://developer.apple.com/library/content/documentation/LanguagesUtilities/Conceptual/iTunesConnectInAppPurchase_Guide/Chapters/TestingInAppPurchases.html#//apple_ref/doc/uid/TP40013727-CH4-SW1)

### Ensure Contracts are Signed
You will not be able to test any StoreKit functionality until you have an iOS Paid Applications contract – StoreKit calls in your code will fail until Apple has processed your Contracts, Tax, and Banking information.

### How to test purchase from TestFlight
* Don't try to sign-in Settings > iTunes & App Stores
* Log out of iTunes & App Stores (make sure you're not logged into any account)
* Just open the app you're trying to test
* Your app will prompt you to sign in
* Enter your credentials for your sandbox test account
* While you could e.g. read your items, you need a real device to test purchasing. NO apis work on Simulators.


## Android Testing 
* You MUST use a physical device. Emulators do not work.
* Ensure you have app in Alpha/Beta with the NuGet installed. This will add "com.android.vending.BILLING" permission for you
* Create an IAB product, make sure it is **published** and **active**
* Add a test account to the app, ensure it is the main account on device, and that account is opted-in as tester
* Validated your version code and number in your development environment match what is in the Play store.
* You MUST sign the APK even in debug mode. In XS this is in the properties. In VS you must manually add this to your project:

```xml
<AndroidKeyStore>True</AndroidKeyStore>
<AndroidSigningKeyStore>KeystoreLocation</AndroidSigningKeyStore>
<AndroidSigningStorePass>PASS</AndroidSigningStorePass>
<AndroidSigningKeyAlias>ALIAS</AndroidSigningKeyAlias>
<AndroidSigningKeyPass>PASS</AndroidSigningKeyPass>
```
* You could use the static product IDs for testing, e.g. android.test.purchased, as described in [Androids Developer Documentation](https://developer.android.com/google/play/billing/billing_testing).

## Android Troubleshooing
* If you see "You need to sign into your google account". This most likely means that you don't have an items published and active for IAB
* If you see "This version of the application is not configured for billing through Google Play": This means the versions number don't match or you don't have the app configured to sign correctly with your keystore.
* If you see "The publisher cannot purchase this item": This means you are trying to buy it on your developer account, and that isn't allowed, you need a different account.


## UWP Testing & Troubleshooting
* Read the UWP developer [In App Purchases API Docs](https://msdn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials)
* You must setup an in app purchase
* Read through the [testing documentation](https://msdn.microsoft.com/en-us/windows/uwp/monetize/in-app-purchases-and-trials#testing)

### Turning on Testing Mode
In UWP, in-app purchases get can be tested by using the `CurrentAppSimulator` class instead of `CurrentApp`. 

To switch the UWP's `InAppBillingImplementation` to testing mode, set the `InTestingMode`  boolean property.

```csharp
CrossInAppBilling.Current.InTestingMode = true;
```


<= Back to [Table of Contents](README.md)
