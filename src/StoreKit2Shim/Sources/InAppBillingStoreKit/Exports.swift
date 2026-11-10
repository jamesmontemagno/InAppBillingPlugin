import Foundation
import StoreKit

// C ABI consumed by src/Plugin.InAppBilling/StoreKit2/StoreKit2Native.apple.cs.
// StoreKit 2 is Swift-only, so everything crosses this boundary as UTF-8 JSON.
// Each async export invokes its callback exactly once.

public typealias IABCallback = @convention(c) (UInt, UnsafePointer<CChar>?) -> Void

private func run(context: UInt, callback: IABCallback, _ operation: @escaping () async -> Response) {
    Task.detached {
        let json = await operation().json
        json.withCString { callback(context, $0) }
    }
}

private func stringArray(_ json: UnsafePointer<CChar>) -> [String] {
    let data = Data(String(cString: json).utf8)
    return (try? JSONDecoder().decode([String].self, from: data)) ?? []
}

@_cdecl("iab_sk2_products")
public func iabProducts(_ idsJson: UnsafePointer<CChar>, _ context: UInt, _ callback: IABCallback) {
    let ids = stringArray(idsJson)
    run(context: context, callback: callback) { await Bridge.products(ids) }
}

@_cdecl("iab_sk2_purchase")
public func iabPurchase(_ productId: UnsafePointer<CChar>, _ appAccountToken: UnsafePointer<CChar>?, _ context: UInt, _ callback: IABCallback) {
    let id = String(cString: productId)
    let token = appAccountToken.map { String(cString: $0) }
    run(context: context, callback: callback) { await Bridge.purchase(productId: id, appAccountToken: token) }
}

@_cdecl("iab_sk2_current_purchases")
public func iabCurrentPurchases(_ context: UInt, _ callback: IABCallback) {
    run(context: context, callback: callback) { await Bridge.currentPurchases() }
}

@_cdecl("iab_sk2_finish_transactions")
public func iabFinishTransactions(_ idsJson: UnsafePointer<CChar>, _ context: UInt, _ callback: IABCallback) {
    let ids = stringArray(idsJson)
    run(context: context, callback: callback) { await Bridge.finish(transactionIds: ids) }
}

@_cdecl("iab_sk2_finish_products")
public func iabFinishProducts(_ idsJson: UnsafePointer<CChar>, _ context: UInt, _ callback: IABCallback) {
    let ids = stringArray(idsJson)
    run(context: context, callback: callback) { await Bridge.finish(productIds: ids) }
}

@_cdecl("iab_sk2_sync")
public func iabSync(_ context: UInt, _ callback: IABCallback) {
    run(context: context, callback: callback) { await Bridge.sync() }
}

@_cdecl("iab_sk2_storefront")
public func iabStorefront(_ context: UInt, _ callback: IABCallback) {
    run(context: context, callback: callback) { await Bridge.storefront() }
}

@_cdecl("iab_sk2_can_make_payments")
public func iabCanMakePayments() -> Int32 {
    AppStore.canMakePayments ? 1 : 0
}

#if !targetEnvironment(macCatalyst)
@_cdecl("iab_sk2_present_offer_code_sheet")
public func iabPresentOfferCodeSheet() {
    Task { await Bridge.presentOfferCodeSheet() }
}
#endif

private let listenerLock = NSLock()
private var listeners: [Task<Void, Never>] = []

@_cdecl("iab_sk2_start_listeners")
public func iabStartListeners(_ context: UInt, _ callback: IABCallback) {
    listenerLock.lock()
    defer { listenerLock.unlock() }

    listeners.forEach { $0.cancel() }
    listeners = [
        Task.detached {
            for await result in Transaction.updates {
                guard !Task.isCancelled else { break }
                if case .verified(let transaction) = result {
                    await Bridge.store.remember(transaction)
                }
                guard !Task.isCancelled else { break }
                let json = Event(kind: "transaction", transaction: TransactionDTO(result)).json
                json.withCString { callback(context, $0) }
            }
        }
    ]

    if #available(iOS 16.4, macCatalyst 16.4, *) {
        listeners.append(Task.detached {
            for await intent in PurchaseIntent.intents {
                guard !Task.isCancelled else { break }
                let json = Event(kind: "purchaseIntent", productId: intent.product.id).json
                json.withCString { callback(context, $0) }
            }
        })
    }
}

@_cdecl("iab_sk2_stop_listeners")
public func iabStopListeners() {
    listenerLock.lock()
    defer { listenerLock.unlock() }

    listeners.forEach { $0.cancel() }
    listeners = []
}
