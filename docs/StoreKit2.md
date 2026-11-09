## StoreKit 2 on iOS and Mac Catalyst

On iOS and Mac Catalyst the plugin defaults to Apple's StoreKit 2 and requires iOS/Mac Catalyst 15 or newer (macOS 12+ for Catalyst). Native .NET macOS (`net10.0-macos`) still uses the original StoreKit APIs.

StoreKit 2 is a Swift-only API, so the plugin includes a small Swift framework (`src/StoreKit2Shim`) that is linked into your app automatically. There is nothing extra to install or configure.

The same backend, JWS transaction mapping, and registration API are used on both platforms. The framework includes Mac Catalyst slices for Apple Silicon and Intel.

### Staying on the original StoreKit API

If you aren't ready to migrate receipt validation or restore behavior, register the legacy backend at startup, before anything accesses `CrossInAppBilling.Current` **or** `CrossInAppBilling.IsSupported`:

```csharp
#if IOS || MACCATALYST
CrossInAppBilling.RegisterImplementation(() => new LegacyInAppBillingImplementation());
#endif
```

For MAUI apps, put this in `MauiProgram.CreateMauiApp` before building the app or resolving services. For .NET iOS or Mac Catalyst apps, register it in `AppDelegate.FinishedLaunching` before using billing. If you instantiate billing through your own DI container instead of `CrossInAppBilling`, register `LegacyInAppBillingImplementation` as your `IInAppBilling` singleton there.

The legacy backend retains the original receipt tokens, restore flow, `applicationUsername`, and payment-queue callbacks. Existing configuration on `InAppBillingImplementation.FinishAllTransactions`, `OnPurchaseComplete`, `OnPurchaseFailure`, and `OnShouldAddStorePayment` also applies to the legacy backend. Set callbacks before the first billing access, as the legacy observer captures them when it starts.

The factory runs lazily and `CrossInAppBilling` owns the resulting instance. `CrossInAppBilling.Dispose()` disposes it but retains the registration, so the next access creates another legacy instance. Registration after initialization throws instead of silently switching backends. Don't change backends during a purchase or restore; choose one at startup and keep it for the app session.

To migrate to StoreKit 2 later, remove the legacy registration and replace `OnShouldAddStorePayment` with `OnPurchaseIntent`. No registration is needed for the default backend. There is no automatic fallback: StoreKit 2 failures won't retry a payment using StoreKit 1. Apple has deprecated StoreKit 1; this opt-out preserves its existing behavior and limitations rather than providing new StoreKit 2 features.

### What changed

The following changes apply to the default StoreKit 2 backend, not the legacy opt-out.

| Area | Original StoreKit | StoreKit 2 |
|------|-------------------|------------|
| `InAppBillingPurchase.PurchaseToken` | Base 64 receipt | Signed JWS transaction. Verify it on your server or with the [App Store Server API](https://developer.apple.com/documentation/appstoreserverapi). |
| `InAppBillingPurchase.OriginalJson` | Empty | The JSON representation of the transaction |
| `GetPurchasesAsync` | Restored all history and could prompt for sign-in | Returns current entitlements and unfinished purchases, including only the active transaction of a subscription. Expired subscriptions, revoked purchases, and finished consumables are not returned. It doesn't prompt for sign-in. |
| Restore button | `GetPurchasesAsync` | `GetPurchasesAsync` is normally enough. Call `InAppBillingImplementation.SyncPurchasesAsync` from a restore button if the user reports missing purchases, as it may prompt for sign-in. |
| Ask to Buy | `PurchaseAsync` never completed | `PurchaseAsync` returns a purchase with `PurchaseState.Deferred`. The result arrives later through `OnPurchaseComplete`. |
| `obfuscatedAccountId` | `applicationUsername`, any string | `appAccountToken`, which must be a UUID. See below. |
| Promoted purchases | `OnShouldAddStorePayment` | `OnPurchaseIntent`, on iOS/Mac Catalyst 16.4+. See [Getting Started](GettingStarted.md). The old property remains available for the legacy backend only. |
| `OnPurchaseComplete` | Also raised for restored purchases | Raised for purchases, renewals, and purchases made outside of the app, but not for `GetPurchasesAsync`. |
| `OnPurchaseFailure` | Failed transactions | Failed purchases, transactions that fail verification, and revoked transactions (`PurchaseState.Canceled`). |
| `ReceiptData` | App receipt | Still available, but Apple has deprecated receipts. Prefer `PurchaseToken`. |

`InAppBillingImplementation.FinishAllTransactions` works as before: transactions finish automatically unless you set it to `false`. Finishing a transaction that is already finished now reports success.

### Mac Catalyst limitations

Apple does not support the offer code redemption sheet on Mac Catalyst: its StoreKit 2 API always throws there. The default backend's `PresentCodeRedemption()` reports this as `PlatformNotSupportedException` instead of silently doing nothing. Customers can redeem codes through the App Store; completed transactions still arrive through the transaction listener.

### Account tokens

StoreKit 2 only accepts a UUID as the account token, and `InAppBillingPurchase.ApplicationUsername` returns it in lowercase.

If `obfuscatedAccountId` is a UUID, it is passed as is. Any other value is converted to a [version 5 UUID](https://www.rfc-editor.org/rfc/rfc4122#section-4.3) in the RFC 4122 URL namespace. The same value always creates the same UUID, so your server can compute it to match a transaction to an account. In Python this is `uuid.uuid5(uuid.NAMESPACE_URL, account_id)`.

### Building from source

The Swift framework needs macOS and Xcode. Run this before building or packing the plugin for iOS:

```bash
src/StoreKit2Shim/build-xcframework.sh
```

The published package includes the framework, and CI builds it for each package. The build produces iOS device, universal iOS simulator, and universal Mac Catalyst slices. Both Apple targets are built from the same C# sources.

A project reference to the plugin doesn't link the framework automatically, so add this to your iOS or Mac Catalyst app:

```xml
<NativeReference Include="path/to/src/StoreKit2Shim/build/InAppBillingStoreKit.xcframework">
  <Kind>Framework</Kind>
</NativeReference>
```
