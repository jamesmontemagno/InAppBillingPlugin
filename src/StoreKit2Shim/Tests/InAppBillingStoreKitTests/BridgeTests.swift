import StoreKitTest
import XCTest
@testable import InAppBillingStoreKit

// Runs in a host app because StoreKit test sessions can't make purchases without one.
final class BridgeTests: XCTestCase {
    private var session: SKTestSession!

    override func setUpWithError() throws {
        let url = try XCTUnwrap(Bundle(for: BridgeTests.self).url(forResource: "Products", withExtension: "storekit"))
        session = try SKTestSession(contentsOf: url)
        session.disableDialogs = true
        session.clearTransactions()
    }

    func testProductsReportFoundAndMissingIds() async throws {
        let response = await Bridge.products(["test.nonconsumable", "test.missing"])

        XCTAssertNil(response.error)
        XCTAssertEqual(response.missing, ["test.missing"])

        let product = try XCTUnwrap(response.products?.first)
        XCTAssertEqual(product.id, "test.nonconsumable")
        XCTAssertEqual(product.price, "4.99")
        XCTAssertTrue(product.isFamilyShareable)

        // The keys below are read by StoreKit2Models.ios.cs.
        let json = response.json
        for key in ["products", "missing", "displayName", "displayPrice", "currencyCode", "isFamilyShareable"] {
            XCTAssertTrue(json.contains("\"\(key)\""), "Missing \(key) in \(json)")
        }
    }

    func testPurchaseReturnsSignedTransactionWithAccountToken() async throws {
        let token = "8F14E45F-CEEA-467F-A8C5-1B2F0C9E3D10"
        let response = await Bridge.purchase(productId: "test.nonconsumable", appAccountToken: token)

        XCTAssertNil(response.error)
        XCTAssertEqual(response.status, "success")

        let transaction = try XCTUnwrap(response.transaction)
        XCTAssertEqual(transaction.productId, "test.nonconsumable")
        XCTAssertEqual(transaction.appAccountToken, token.lowercased())
        XCTAssertEqual(transaction.jws.split(separator: ".").count, 3)
        XCTAssertTrue(transaction.verified)
        XCTAssertFalse(transaction.revoked)

        // The keys below are read by StoreKit2Models.ios.cs.
        let json = response.json
        for key in ["originalId", "productId", "purchaseDateMs", "quantity", "appAccountToken", "jws", "revoked", "verified"] {
            XCTAssertTrue(json.contains("\"\(key)\""), "Missing \(key) in \(json)")
        }
    }

    func testPurchaseOfUnknownProductFails() async {
        let response = await Bridge.purchase(productId: "test.missing", appAccountToken: nil)

        XCTAssertEqual(response.error?.kind, "invalidProduct")
    }

    func testUnfinishedConsumableStaysAvailableUntilFinished() async throws {
        let purchase = await Bridge.purchase(productId: "test.consumable", appAccountToken: nil)
        let id = try XCTUnwrap(purchase.transaction?.id)

        let before = await Bridge.currentPurchases()
        XCTAssertTrue(before.transactions?.contains { $0.id == id } == true)

        let finished = await Bridge.finish(transactionIds: [id])
        XCTAssertEqual(finished.results?.first?.success, true)

        let after = await Bridge.currentPurchases()
        XCTAssertFalse(after.transactions?.contains { $0.id == id } == true)

        // Finishing is idempotent so a manual finish after auto finish still reports success.
        let again = await Bridge.finish(transactionIds: [id])
        XCTAssertEqual(again.results?.first?.success, true)
    }

    func testOwnedNonConsumableIsCurrentAfterFinishing() async throws {
        let purchase = await Bridge.purchase(productId: "test.nonconsumable", appAccountToken: nil)
        let id = try XCTUnwrap(purchase.transaction?.id)
        _ = await Bridge.finish(productIds: ["test.nonconsumable"])

        let current = await Bridge.currentPurchases()

        XCTAssertTrue(current.transactions?.contains { $0.id == id } == true)
    }

    func testFinishingUnknownIdsFails() async {
        let response = await Bridge.finish(transactionIds: ["1", "not-a-number"])

        XCTAssertEqual(response.results?.map(\.success), [false, false])
    }
}
