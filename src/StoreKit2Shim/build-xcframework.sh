#!/bin/bash
# Builds build/InAppBillingStoreKit.xcframework (iOS device and simulator). Requires macOS and Xcode.
set -euo pipefail

NAME=InAppBillingStoreKit
MIN_IOS=15.0
ROOT="$(cd "$(dirname "$0")" && pwd)"
OUT="$ROOT/build"
WORK="$OUT/work"
SOURCES=("$ROOT/Sources/$NAME"/*.swift)

rm -rf "$OUT"
mkdir -p "$WORK"

compile() { # <sdk> <target triple> <output binary>
    xcrun --sdk "$1" swiftc \
        -sdk "$(xcrun --sdk "$1" --show-sdk-path)" \
        -target "$2" \
        -module-name "$NAME" \
        -swift-version 5 \
        -O -whole-module-optimization \
        -emit-library \
        -Xlinker -install_name -Xlinker "@rpath/$NAME.framework/$NAME" \
        -module-cache-path "$WORK/module-cache" \
        -o "$3" \
        "${SOURCES[@]}"
}

package_framework() { # <supported platform> <binary> <output directory>
    local framework="$3/$NAME.framework"
    mkdir -p "$framework"
    cp "$2" "$framework/$NAME"
    cat > "$framework/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key><string>en</string>
    <key>CFBundleExecutable</key><string>$NAME</string>
    <key>CFBundleIdentifier</key><string>com.refractored.$NAME</string>
    <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
    <key>CFBundleName</key><string>$NAME</string>
    <key>CFBundlePackageType</key><string>FMWK</string>
    <key>CFBundleShortVersionString</key><string>1.0</string>
    <key>CFBundleSupportedPlatforms</key><array><string>$1</string></array>
    <key>CFBundleVersion</key><string>1</string>
    <key>MinimumOSVersion</key><string>$MIN_IOS</string>
</dict>
</plist>
EOF
    plutil -convert binary1 "$framework/Info.plist"
}

compile iphoneos "arm64-apple-ios$MIN_IOS" "$WORK/device"
compile iphonesimulator "arm64-apple-ios$MIN_IOS-simulator" "$WORK/simulator-arm64"
compile iphonesimulator "x86_64-apple-ios$MIN_IOS-simulator" "$WORK/simulator-x64"
lipo -create "$WORK/simulator-arm64" "$WORK/simulator-x64" -output "$WORK/simulator"

package_framework iPhoneOS "$WORK/device" "$WORK/iphoneos"
package_framework iPhoneSimulator "$WORK/simulator" "$WORK/iphonesimulator"

xcodebuild -create-xcframework \
    -framework "$WORK/iphoneos/$NAME.framework" \
    -framework "$WORK/iphonesimulator/$NAME.framework" \
    -output "$OUT/$NAME.xcframework"

rm -rf "$WORK"
echo "Created $OUT/$NAME.xcframework"
