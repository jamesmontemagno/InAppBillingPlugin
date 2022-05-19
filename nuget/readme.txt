In App Billing Plugin for .NET MAUI, Xamarin, & Windows

SUPER IMPORTANT: iOS has changed the way in which in-app purchases are handled. They are no longer automatically finished and you must call `FinalizePurchaseAsync(string transactionIdentifier)` on each transaction! 

Version 5.0+ has more significant updates!
1.) We have removed IInAppBillingVerifyPurchase from all methods. All data required to handle this yourself is returned.
2.) iOS ReceiptURL data is avaialble via ReceiptData
3.) We are now using Android Billing version 4
4.) Major breaking chanages across the API including AcknowledgePurchaseAsync being changed to FinalizePurchaseAsync

Please erad documetnation for all changes

Version 4.0 has significant updates.

1.) You must compile and target against Android 10 or higher
2.) On Android you must handle pending transactions and call `FinalizePurchaseAsync` when done
3.) On Android HandleActivityResult has been removed.
4.) We now use Xamarin.Essentials and setup is required per docs.

Find the latest setup guides, documentation, and testing instructions at: 
https://github.com/jamesmontemagno/InAppBillingPlugin
