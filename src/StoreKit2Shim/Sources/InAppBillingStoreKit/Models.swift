import Foundation
import StoreKit

// JSON contract with src/Plugin.InAppBilling/StoreKit2/StoreKit2Models.apple.cs.

struct Response: Encodable {
    var error: ResponseError?
    var status: String?
    var products: [ProductDTO]?
    var missing: [String]?
    var transaction: TransactionDTO?
    var transactions: [TransactionDTO]?
    var results: [FinishResult]?
    var storefront: StorefrontDTO?

    init(error: ResponseError? = nil,
         status: String? = nil,
         products: [ProductDTO]? = nil,
         missing: [String]? = nil,
         transaction: TransactionDTO? = nil,
         transactions: [TransactionDTO]? = nil,
         results: [FinishResult]? = nil,
         storefront: StorefrontDTO? = nil) {
        self.error = error
        self.status = status
        self.products = products
        self.missing = missing
        self.transaction = transaction
        self.transactions = transactions
        self.results = results
        self.storefront = storefront
    }

    init(failure: Error) {
        self.init(error: ResponseError(failure))
    }

    var json: String {
        guard let data = try? JSONEncoder().encode(self),
              let text = String(data: data, encoding: .utf8) else {
            return #"{"error":{"kind":"general","message":"Unable to encode the StoreKit response."}}"#
        }
        return text
    }
}

struct Event: Encodable {
    var kind: String
    var transaction: TransactionDTO?
    var productId: String?

    var json: String {
        guard let data = try? JSONEncoder().encode(self), let text = String(data: data, encoding: .utf8) else {
            return "{}"
        }
        return text
    }
}

struct ResponseError: Encodable {
    var kind: String
    var message: String

    init(kind: String, message: String) {
        self.kind = kind
        self.message = message
    }

    init(_ error: Error) {
        kind = ResponseError.kind(for: error)
        message = error.localizedDescription
    }

    private static func kind(for error: Error) -> String {
        if hasUnderlyingCode(error as NSError, code: 3038) {
            return "termsChanged"
        }

        if let purchaseError = error as? Product.PurchaseError {
            switch purchaseError {
            case .productUnavailable: return "itemUnavailable"
            case .purchaseNotAllowed: return "paymentNotAllowed"
            default: return "paymentInvalid"
            }
        }

        if let storeError = error as? StoreKitError {
            switch storeError {
            case .userCancelled: return "userCancelled"
            case .networkError: return "network"
            case .notAvailableInStorefront: return "itemUnavailable"
            case .notEntitled: return "notOwned"
            default: return "general"
            }
        }

        return "general"
    }

    // Apple reports that the user must accept new terms as underlying error 3038.
    private static func hasUnderlyingCode(_ error: NSError, code: Int) -> Bool {
        guard let underlying = error.userInfo[NSUnderlyingErrorKey] as? NSError else {
            return false
        }
        return underlying.code == code || hasUnderlyingCode(underlying, code: code)
    }
}

struct FinishResult: Encodable {
    var id: String
    var success: Bool
}

struct StorefrontDTO: Encodable {
    var id: String
    var countryCode: String
}

struct TransactionDTO: Encodable {
    var id: String
    var originalId: String
    var productId: String
    var purchaseDateMs: Int64
    var quantity: Int
    var appAccountToken: String?
    var jws: String
    var json: String
    var revoked: Bool
    var verified: Bool

    init(_ result: VerificationResult<Transaction>) {
        let transaction = result.unsafePayloadValue
        id = String(transaction.id)
        originalId = String(transaction.originalID)
        productId = transaction.productID
        purchaseDateMs = Int64(transaction.purchaseDate.timeIntervalSince1970 * 1000)
        quantity = transaction.purchasedQuantity
        appAccountToken = transaction.appAccountToken?.uuidString.lowercased()
        jws = result.jwsRepresentation
        json = String(data: transaction.jsonRepresentation, encoding: .utf8) ?? ""
        revoked = transaction.revocationDate != nil
        if case .verified = result {
            verified = true
        } else {
            verified = false
        }
    }
}

struct PeriodDTO: Encodable {
    var value: Int
    var unit: String

    init(_ period: Product.SubscriptionPeriod) {
        value = period.value
        switch period.unit {
        case .day: unit = "day"
        case .week: unit = "week"
        case .month: unit = "month"
        case .year: unit = "year"
        @unknown default: unit = "unknown"
        }
    }
}

struct OfferDTO: Encodable {
    var id: String?
    var type: String
    var price: String
    var displayPrice: String
    var paymentMode: String
    var period: PeriodDTO
    var periodCount: Int

    init(_ offer: Product.SubscriptionOffer) {
        id = offer.id
        type = offer.type == .introductory ? "introductory" : (offer.type == .promotional ? "promotional" : "unknown")
        price = "\(offer.price)"
        displayPrice = offer.displayPrice
        period = PeriodDTO(offer.period)
        periodCount = offer.periodCount
        switch offer.paymentMode {
        case .freeTrial: paymentMode = "freeTrial"
        case .payAsYouGo: paymentMode = "payAsYouGo"
        case .payUpFront: paymentMode = "payUpFront"
        default: paymentMode = "unknown"
        }
    }
}

struct ProductDTO: Encodable {
    var id: String
    var displayName: String
    var description: String
    var displayPrice: String
    var price: String
    var currencyCode: String
    var isFamilyShareable: Bool
    var subscriptionGroupId: String?
    var subscriptionPeriod: PeriodDTO?
    var introductoryOffer: OfferDTO?
    var promotionalOffers: [OfferDTO]?

    init(_ product: Product) {
        id = product.id
        displayName = product.displayName
        description = product.description
        displayPrice = product.displayPrice
        // Decimal's description is invariant, so it survives the trip to C# in every locale.
        price = "\(product.price)"
        currencyCode = product.priceFormatStyle.currencyCode
        isFamilyShareable = product.isFamilyShareable
        if let subscription = product.subscription {
            subscriptionGroupId = subscription.subscriptionGroupID
            subscriptionPeriod = PeriodDTO(subscription.subscriptionPeriod)
            introductoryOffer = subscription.introductoryOffer.map(OfferDTO.init)
            promotionalOffers = subscription.promotionalOffers.map(OfferDTO.init)
        }
    }
}
