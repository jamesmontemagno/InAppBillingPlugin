## iOS StoreKit 2

On iOS the plugin uses Apple's StoreKit 2 and requires iOS 15 or newer. Mac Catalyst and macOS still use the original StoreKit APIs.

StoreKit 2 is a Swift-only API, so the plugin includes a small Swift framework (`src/StoreKit2Shim`) that is linked into your app automatically. There is nothing extra to install or configure.

### What changed

| Area | Original StoreKit | StoreKit 2 |
|------|-------------------|------------|
| `InAppBillingPurchase.PurchaseToken` | Base 64 receipt | Signed JWS transaction. Verify it on your server or with the [App Store Server API](https://developer.apple.com/documentation/appstoreserverapi). |
| `InAppBillingPurchase.OriginalJson` | Empty | The JSON representation of the transaction |
| `GetPurchasesAsync` | Restored all history and could prompt for sign-in | Returns current entitlements and unfinished purchases, including only the active transaction of a subscription. Expired subscriptions, revoked purchases, and finished consumables are not returned. It doesn't prompt for sign-in. |
| Restore button | `GetPurchasesAsync` | `GetPurchasesAsync` is normally enough. Call `InAppBillingImplementation.SyncPurchasesAsync` from a restore button if the user reports missing purchases, as it may prompt for sign-in. |
| Ask to Buy | `PurchaseAsync` never completed | `PurchaseAsync` returns a purchase with `PurchaseState.Deferred`. The result arrives later through `OnPurchaseComplete`. |
| `obfuscatedAccountId` | `applicationUsername`, any string | `appAccountToken`, which must be a UUID. See below. |
| Promoted purchases | `OnShouldAddStorePayment` | `OnPurchaseIntent`, on iOS 16.4+. See [Getting Started](GettingStarted.md). The old property is now a compile error. |
| `OnPurchaseComplete` | Also raised for restored purchases | Raised for purchases, renewals, and purchases made outside of the app, but not for `GetPurchasesAsync`. |
| `OnPurchaseFailure` | Failed transactions | Failed purchases, transactions that fail verification, and revoked transactions (`PurchaseState.Canceled`). |
| `ReceiptData` | App receipt | Still available, but Apple has deprecated receipts. Prefer `PurchaseToken`. |

`InAppBillingImplementation.FinishAllTransactions` works as before: transactions finish automatically unless you set it to `false`. Finishing a transaction that is already finished now reports success.

### Account tokens

StoreKit 2 only accepts a UUID as the account token, and `InAppBillingPurchase.ApplicationUsername` returns it in lowercase.

If `obfuscatedAccountId` is a UUID, it is passed as is. Any other value is converted to a [version 5 UUID](https://www.rfc-editor.org/rfc/rfc4122#section-4.3) in the RFC 4122 URL namespace. The same value always creates the same UUID, so your server can compute it to match a transaction to an account. In Python this is `uuid.uuid5(uuid.NAMESPACE_URL, account_id)`.

### Building from source

The Swift framework needs macOS and Xcode. Run this before building or packing the plugin for iOS:

```bash
src/StoreKit2Shim/build-xcframework.sh
```

The published package includes the framework, and CI builds it for each package.

A project reference to the plugin doesn't link the framework automatically, so add this to your iOS app:

```xml
<NativeReference Include="path/to/src/StoreKit2Shim/build/InAppBillingStoreKit.xcframework">
  <Kind>Framework</Kind>
</NativeReference>
```
