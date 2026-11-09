import StoreKitTest
import XCTest
@testable import InAppBillingStoreKit

// Purchases need a real app, so they are covered end to end by src/InAppBillingTests/StoreKit2Smoke.
final class BridgeTests: XCTestCase {
    private var session: SKTestSession!

    override func setUpWithError() throws {
        let url = try XCTUnwrap(Bundle.module.url(forResource: "Products", withExtension: "storekit"))
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

    func testPurchaseOfUnknownProductFails() async {
        let response = await Bridge.purchase(productId: "test.missing", appAccountToken: nil)

        XCTAssertEqual(response.error?.kind, "invalidProduct")
    }

    func testFinishingUnknownIdsFails() async {
        let response = await Bridge.finish(transactionIds: ["1", "not-a-number"])

        XCTAssertEqual(response.results?.map(\.success), [false, false])
    }
}
