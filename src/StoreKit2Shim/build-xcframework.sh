#!/bin/bash
# Builds build/InAppBillingStoreKit.xcframework (iOS device, simulator, and universal Mac Catalyst). Requires macOS and Xcode.
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
    local sdk
    sdk="$(xcrun --sdk "$1" --show-sdk-path)"
    local sdk_flags=(-sdk "$sdk")
    if [ "$1" = "macosx" ]; then
        sdk_flags+=(
            -F "$sdk/System/iOSSupport/System/Library/Frameworks"
            -I "$sdk/System/iOSSupport/usr/include"
            -L "$sdk/System/iOSSupport/usr/lib"
        )
    fi
    xcrun --sdk "$1" swiftc \
        -target "$2" \
        -module-name "$NAME" \
        -swift-version 5 \
        -O -whole-module-optimization \
        -emit-library \
        -Xlinker -install_name -Xlinker "@rpath/$NAME.framework/$NAME" \
        -module-cache-path "$WORK/module-cache" \
        -o "$3" \
        "${sdk_flags[@]}" \
        "${SOURCES[@]}"
}

package_framework() { # <supported platform> <binary> <output directory>
    local framework="$3/$NAME.framework"
    local minimum_key=MinimumOSVersion
    local minimum_version="$MIN_IOS"
    if [ "$1" = "MacOSX" ]; then
        # Mac Catalyst 15 maps to macOS 12, not macOS 15.
        minimum_key=LSMinimumSystemVersion
        minimum_version=12.0
    fi
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
    <key>$minimum_key</key><string>$minimum_version</string>
</dict>
</plist>
EOF
    plutil -convert binary1 "$framework/Info.plist"
}

compile iphoneos "arm64-apple-ios$MIN_IOS" "$WORK/device"
compile iphonesimulator "arm64-apple-ios$MIN_IOS-simulator" "$WORK/simulator-arm64"
compile iphonesimulator "x86_64-apple-ios$MIN_IOS-simulator" "$WORK/simulator-x64"
lipo -create "$WORK/simulator-arm64" "$WORK/simulator-x64" -output "$WORK/simulator"
compile macosx "arm64-apple-ios$MIN_IOS-macabi" "$WORK/catalyst-arm64"
compile macosx "x86_64-apple-ios$MIN_IOS-macabi" "$WORK/catalyst-x64"
lipo -create "$WORK/catalyst-arm64" "$WORK/catalyst-x64" -output "$WORK/catalyst"

package_framework iPhoneOS "$WORK/device" "$WORK/iphoneos"
package_framework iPhoneSimulator "$WORK/simulator" "$WORK/iphonesimulator"
package_framework MacOSX "$WORK/catalyst" "$WORK/maccatalyst"

xcodebuild -create-xcframework \
    -framework "$WORK/iphoneos/$NAME.framework" \
    -framework "$WORK/iphonesimulator/$NAME.framework" \
    -framework "$WORK/maccatalyst/$NAME.framework" \
    -output "$OUT/$NAME.xcframework"

rm -rf "$WORK"
echo "Created $OUT/$NAME.xcframework"
