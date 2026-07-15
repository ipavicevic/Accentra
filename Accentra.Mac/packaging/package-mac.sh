#!/bin/bash
# Builds, bundles, signs, and (optionally) notarizes Accentra.app for macOS.
#
# Usage:
#   ./package-mac.sh <rid> [sign-identity]
#
#   rid            osx-arm64 or osx-x64
#   sign-identity  codesign identity ("Developer ID Application: ..."); ad-hoc if omitted
#
# Notarization runs when NOTARY_PROFILE is set to a notarytool keychain profile
# (create one with: xcrun notarytool store-credentials).
#
# Output: dist/Accentra-<version>-<arch>.zip
set -euo pipefail

RID="${1:?usage: package-mac.sh <osx-arm64|osx-x64> [sign-identity]}"
IDENTITY="${2:--}"   # "-" = ad-hoc signature
ARCH="${RID#osx-}"

cd "$(dirname "$0")/.."   # Accentra.Mac/
PKG_DIR="packaging"

VERSION=$(sed -n 's/.*<FileVersion>\(.*\)<\/FileVersion>.*/\1/p' Accentra.Mac.csproj)
SHORT_VERSION=$(echo "$VERSION" | cut -d. -f1-3)
echo "==> Packaging Accentra $VERSION ($ARCH), identity: $IDENTITY"

echo "==> dotnet publish"
dotnet publish Accentra.Mac.csproj -c Release -r "$RID" --self-contained \
    -p:PublishSingleFile=true -p:DebugType=none -o "bin/publish-$RID"

echo "==> Building Accentra.app"
APP="dist/$ARCH/Accentra.app"
rm -rf "dist/$ARCH" && mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

cp "bin/publish-$RID/Accentra" "$APP/Contents/MacOS/Accentra"
cp "$PKG_DIR/Accentra.icns" "$APP/Contents/Resources/Accentra.icns"
sed -e "s/__VERSION__/$VERSION/" -e "s/__SHORT_VERSION__/$SHORT_VERSION/" \
    "$PKG_DIR/Info.plist" > "$APP/Contents/Info.plist"

echo "==> codesign"
codesign --force --options runtime --timestamp \
    --entitlements "$PKG_DIR/entitlements.plist" \
    --sign "$IDENTITY" "$APP"
codesign --verify --deep --strict "$APP"

ZIP="dist/Accentra-$VERSION-$ARCH.zip"
rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"

if [[ -n "${NOTARY_PROFILE:-}" ]]; then
    echo "==> Notarizing (profile: $NOTARY_PROFILE)"
    # Submit without --wait (it crashes with a bus error on some notarytool
    # versions) and poll for the verdict instead. First submissions of a new
    # hash have been observed to hang indefinitely while a resubmission of the
    # same bytes clears in minutes — resubmit after a stall.
    STALL_SECONDS="${NOTARY_STALL_SECONDS:-600}"
    submit() {
        xcrun notarytool submit "$ZIP" --keychain-profile "$NOTARY_PROFILE" \
            | sed -n 's/^ *id: //p' | head -1
    }
    SUBMISSION=$(submit)
    echo "    submission: $SUBMISSION"
    ATTEMPT=1
    WAITED=0
    while :; do
        STATUS=$(xcrun notarytool info "$SUBMISSION" --keychain-profile "$NOTARY_PROFILE" \
            | sed -n 's/^ *status: //p')
        [[ "$STATUS" != "In Progress" ]] && break
        sleep 30; WAITED=$((WAITED + 30))
        if (( WAITED >= STALL_SECONDS && ATTEMPT < 3 )); then
            ATTEMPT=$((ATTEMPT + 1)); WAITED=0
            SUBMISSION=$(submit)
            echo "    stalled — resubmitted (attempt $ATTEMPT): $SUBMISSION"
        fi
    done
    echo "    status: $STATUS"
    if [[ "$STATUS" != "Accepted" ]]; then
        xcrun notarytool log "$SUBMISSION" --keychain-profile "$NOTARY_PROFILE" || true
        exit 1
    fi
    echo "==> Stapling"
    xcrun stapler staple "$APP"
    rm -f "$ZIP"
    ditto -c -k --keepParent "$APP" "$ZIP"
else
    echo "==> Skipping notarization (NOTARY_PROFILE not set)"
fi

echo "==> Done: Accentra.Mac/$ZIP"
