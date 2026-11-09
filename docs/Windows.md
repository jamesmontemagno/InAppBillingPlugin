## Windows and the Microsoft Store

On Windows the plugin uses [`Windows.Services.Store`](https://learn.microsoft.com/uwp/api/windows.services.store), the current Microsoft Store API. It works from WinUI 3, Windows App SDK, .NET MAUI, WPF, and WinForms apps. The Windows App SDK has no Store API of its own, so no extra package is required.

### Requirements
* Your app must be packaged with MSIX and must not run elevated. Otherwise `ConnectAsync` returns `false`, `CanMakePayments` is `false`, and other calls throw `PurchaseError.BillingUnavailable`.
* Create your add-ons in Partner Center. Developer-managed consumables, Store-managed consumables, durables, and subscriptions are all supported.
* Start purchases from the UI thread if you can. The plugin moves a purchase to the UI thread captured when it was created or connected.

### Product IDs
Everywhere a product ID is expected you can pass either the **product ID** you entered in Partner Center or the 12-character **Store ID**. `InAppBillingProduct.ProductId` and the result of `PurchaseAsync` return the ID you passed. `GetPurchasesAsync` returns the Partner Center product ID, and `ProductIds` contains both.

### Owner window
Store dialogs need an owner window in desktop apps. By default the plugin uses the active window of the calling thread and then the main window of the process. For multi-window apps, or if you get `PurchaseError.DeveloperError` about a missing window, set a provider:

```csharp
#if WINDOWS
// WinUI 3
InAppBillingImplementation.WindowHandleProvider = () => WinRT.Interop.WindowNative.GetWindowHandle(myWindow);

// .NET MAUI
InAppBillingImplementation.WindowHandleProvider = () =>
    ((MauiWinUIWindow)Application.Current.Windows[0].Handler.PlatformView).WindowHandle;
#endif
```

### How the API maps to the Store

| API | Windows behavior |
| --- | --- |
| `ConnectAsync` / `DisconnectAsync` / `IsConnected` | Checks the app can use the Store and listens for license changes. Other calls connect automatically. |
| `GetProductInfoAsync` | Supports all item types. Throws `PurchaseError.InvalidProduct` with the `Invalid` IDs unless `IgnoreInvalidProducts` is set. |
| `GetPurchasesAsync` | Returns current entitlements. Expired, revoked, and fully consumed items are left out. |
| `PurchaseAsync` | Throws `InAppBillingPurchaseException` instead of returning `null`. `subOfferToken` can be a SKU Store ID from `WindowsExtras.Skus`. |
| `obfuscatedAccountId` / `obfuscatedProfileId` | Stored in the Store's developer offer ID as `account\|profile`, and returned on purchases. Don't use `\|` in the account ID. |
| `ConsumePurchaseAsync` | Fulfills a developer-managed consumable, or the **whole** balance of a Store-managed consumable. Grant `purchase.Quantity` units when you consume it. |
| `FinalizePurchaseAsync` / `FinalizePurchaseOfProductAsync` | Report success. The Store completes purchases itself, so `IsAcknowledged` is always `true`. |
| `PresentCodeRedemption` | Opens the Microsoft account [redeem page](https://account.microsoft.com/billing/redeem). Promotional codes aren't available for subscription add-ons. |
| `Storefront` | The Windows home region of the user, as two-letter `Id` and three-letter `CountryCode`. |
| `CanMakePayments` | `false` for unpackaged or elevated apps. |
| `ReceiptData` | Always empty. See [Securing Purchases](SecuringPurchases.md). |
| `UpgradePurchasedSubscriptionAsync` | Throws `PurchaseError.FeatureNotSupported`. The Store has no client API for this, and customers manage subscriptions at https://account.microsoft.com/services. |

### Windows-only APIs
Use these from `#if WINDOWS` code by casting `CrossInAppBilling.Current` to `InAppBillingImplementation`.

```csharp
#if WINDOWS
var billing = (InAppBillingImplementation)CrossInAppBilling.Current;

// Store-managed consumables: read the balance and consume part of it
var balance = await billing.GetConsumableBalanceAsync("coins");
await billing.ConsumePurchaseAsync("coins", quantity: 10, trackingId: Guid.NewGuid());

// Be told about license changes that happen outside of the app
InAppBillingImplementation.OnWindowsLicensesChanged = () => { /* call GetPurchasesAsync */ };

// Anything else in the Store API, such as trials of the app itself
var license = await billing.GetStoreContext().GetAppLicenseAsync();
#endif
```

`InAppBillingProduct.WindowsExtras` has subscription periods, trial details, and SKUs. `InAppBillingPurchase.WindowsExtras` has the expiration date, trial state, and Store IDs.

### Migrating from earlier versions
Earlier versions used `Windows.ApplicationModel.Store`, which isn't supported in packaged desktop apps. If you are upgrading:

* **Failures now throw.** `PurchaseAsync` throws for cancelled and failed purchases, including `PurchaseError.UserCancelled` and `PurchaseError.AlreadyOwned`, instead of returning `null` or a purchase with a failed state.
* **Unknown products now throw** from `GetProductInfoAsync`. Set `IgnoreInvalidProducts = true` for the old behavior.
* **`ConsumePurchaseAsync` throws** `PurchaseError.InvalidProduct` for a product that isn't a consumable.
* **Subscriptions are separate.** `GetPurchasesAsync(ItemType.InAppPurchase)` no longer returns subscriptions.
* **`InTestingMode` uses a new simulator.** `WindowsStoreProxy.xml` is no longer read. See [Testing & Troubleshooting](TestingAndTroubleshooting.md).

<= Back to [Table of Contents](README.md)
