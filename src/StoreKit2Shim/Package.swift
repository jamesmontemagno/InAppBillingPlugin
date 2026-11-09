// swift-tools-version: 5.9
import PackageDescription

// Used for tests and editing in Xcode. The shipped framework is produced by build-xcframework.sh.
let package = Package(
    name: "InAppBillingStoreKit",
    platforms: [.iOS(.v15)],
    products: [
        .library(name: "InAppBillingStoreKit", type: .dynamic, targets: ["InAppBillingStoreKit"]),
    ],
    targets: [
        .target(name: "InAppBillingStoreKit"),
        .testTarget(
            name: "InAppBillingStoreKitTests",
            dependencies: ["InAppBillingStoreKit"],
            resources: [.copy("Products.storekit")]
        ),
    ]
)
