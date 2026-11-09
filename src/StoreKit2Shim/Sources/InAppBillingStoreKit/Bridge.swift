import Foundation
import StoreKit
import UIKit

/// Holds transactions StoreKit handed to us so they can be finished later by id.
actor TransactionStore {
    private var unfinished: [UInt64: Transaction] = [:]
    private var finishedIds: Set<UInt64> = []
    private var finishedProductIds: Set<String> = []

    func remember(_ transaction: Transaction) {
        if !finishedIds.contains(transaction.id) {
            unfinished[transaction.id] = transaction
        }
    }

    func isFinished(_ transactionId: UInt64) -> Bool {
        finishedIds.contains(transactionId)
    }

    func finish(transactionId: UInt64) async -> Bool {
        if finishedIds.contains(transactionId) {
            return true
        }
        if let transaction = unfinished[transactionId] {
            await complete(transaction)
            return true
        }

        for await result in Transaction.unfinished {
            if case .verified(let transaction) = result, transaction.id == transactionId {
                await complete(transaction)
                return true
            }
        }

        // Nothing is left to finish if the transaction is in the history but no longer unfinished.
        for await result in Transaction.all {
            if case .verified(let transaction) = result, transaction.id == transactionId {
                return true
            }
        }
        return false
    }

    func finish(productId: String) async -> Bool {
        var known = finishedProductIds.contains(productId)
        for (_, transaction) in unfinished where transaction.productID == productId {
            await complete(transaction)
            known = true
        }
        for await result in Transaction.unfinished {
            if case .verified(let transaction) = result, transaction.productID == productId {
                await complete(transaction)
                known = true
            }
        }
        if known {
            return true
        }
        for await result in Transaction.all {
            if case .verified(let transaction) = result, transaction.productID == productId {
                return true
            }
        }
        return false
    }

    private func complete(_ transaction: Transaction) async {
        await transaction.finish()
        unfinished[transaction.id] = nil
        finishedIds.insert(transaction.id)
        finishedProductIds.insert(transaction.productID)
    }
}

enum Bridge {
    static let store = TransactionStore()

    static func products(_ ids: [String]) async -> Response {
        do {
            let products = try await Product.products(for: Set(ids))
            let found = Set(products.map(\.id))
            return Response(products: products.map(ProductDTO.init), missing: ids.filter { !found.contains($0) })
        } catch {
            return Response(failure: error)
        }
    }

    static func purchase(productId: String, appAccountToken: String?) async -> Response {
        do {
            guard let product = try await Product.products(for: [productId]).first else {
                return Response(error: ResponseError(kind: "invalidProduct", message: "Product \(productId) was not found."))
            }

            var options: Set<Product.PurchaseOption> = []
            if let appAccountToken, let token = UUID(uuidString: appAccountToken) {
                options.insert(.appAccountToken(token))
            }

            let result: Product.PurchaseResult
            if #available(iOS 18.2, *), let scene = await activeScene() {
                result = try await product.purchase(confirmIn: scene, options: options)
            } else {
                result = try await product.purchase(options: options)
            }

            switch result {
            case .success(let verification):
                guard case .verified(let transaction) = verification else {
                    return Response(error: unverifiedError(verification))
                }
                await store.remember(transaction)
                return Response(status: "success", transaction: TransactionDTO(verification))
            case .userCancelled:
                return Response(status: "userCancelled")
            case .pending:
                return Response(status: "pending")
            @unknown default:
                return Response(error: ResponseError(kind: "general", message: "Unknown purchase result."))
            }
        } catch {
            return Response(failure: error)
        }
    }

    /// Everything the user currently owns, plus purchases that are still waiting to be finished.
    static func currentPurchases() async -> Response {
        var seen: Set<UInt64> = []
        var transactions: [TransactionDTO] = []

        func add(_ result: VerificationResult<Transaction>) async {
            guard case .verified(let transaction) = result, seen.insert(transaction.id).inserted else {
                return
            }
            // StoreKit can still list a consumable right after it finishes, and returning it would let an app credit it twice.
            if transaction.productType == .consumable, await store.isFinished(transaction.id) {
                return
            }
            await store.remember(transaction)
            transactions.append(TransactionDTO(result))
        }

        for await result in Transaction.currentEntitlements {
            await add(result)
        }
        for await result in Transaction.unfinished {
            await add(result)
        }
        return Response(transactions: transactions)
    }

    static func finish(transactionIds: [String]) async -> Response {
        var results: [FinishResult] = []
        for id in transactionIds {
            var success = false
            if let transactionId = UInt64(id) {
                success = await store.finish(transactionId: transactionId)
            }
            results.append(FinishResult(id: id, success: success))
        }
        return Response(results: results)
    }

    static func finish(productIds: [String]) async -> Response {
        var results: [FinishResult] = []
        for id in productIds {
            results.append(FinishResult(id: id, success: await store.finish(productId: id)))
        }
        return Response(results: results)
    }

    static func sync() async -> Response {
        do {
            try await AppStore.sync()
            return Response(status: "success")
        } catch {
            return Response(failure: error)
        }
    }

    static func storefront() async -> Response {
        guard let storefront = await Storefront.current else {
            return Response()
        }
        return Response(storefront: StorefrontDTO(id: storefront.id, countryCode: storefront.countryCode))
    }

    static func unverifiedError(_ result: VerificationResult<Transaction>) -> ResponseError {
        if case .unverified(_, let error) = result {
            return ResponseError(kind: "general", message: "StoreKit could not verify the transaction: \(error.localizedDescription)")
        }
        return ResponseError(kind: "general", message: "StoreKit could not verify the transaction.")
    }

    @MainActor
    static func activeScene() -> UIWindowScene? {
        let scenes = UIApplication.shared.connectedScenes.compactMap { $0 as? UIWindowScene }
        return scenes.first { $0.activationState == .foregroundActive } ?? scenes.first
    }

    @MainActor
    static func presentOfferCodeSheet() async {
        if #available(iOS 16.0, *), let scene = activeScene() {
            try? await AppStore.presentOfferCodeRedeemSheet(in: scene)
        } else {
            SKPaymentQueue.default().presentCodeRedemptionSheet()
        }
    }
}
