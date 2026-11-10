#!/bin/sh
# Copies the .NET Supertext SDK from its GitHub repository into lib/supertext-sdk, with its
# namespace changed from Supertext.Sdk to Supertext.Umbraco.Translation.Sdk. The package
# compiles this copy into its own assembly, so sites install nothing besides the package, and
# another package that references Supertext.Sdk itself can never clash with it.
#
#   scripts/vendor-sdk.sh [commit-or-branch]   (default: main)
#   SDK_DIR=/path/to/clone scripts/vendor-sdk.sh   (use a local clone as it is)
set -eu
REF="${1:-main}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

if [ -n "${SDK_DIR:-}" ]; then
    SRC="$SDK_DIR"
else
    git clone --quiet https://github.com/Supertext/Supertext-API-SDK-Guide "$TMP/sdk"
    git -C "$TMP/sdk" checkout --quiet "$REF"
    SRC="$TMP/sdk"
fi
COMMIT="$(git -C "$SRC" rev-parse HEAD)"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$SRC/sdk/dotnet/src/Supertext.Sdk/Supertext.Sdk.csproj" | head -n 1)"
if [ -z "$VERSION" ]; then
    echo "vendor-sdk: no <Version> in sdk/dotnet/src/Supertext.Sdk/Supertext.Sdk.csproj" >&2
    exit 1
fi

DEST="$ROOT/lib/supertext-sdk"
rm -rf "$DEST"
mkdir -p "$DEST/src"
# The version-neutral files (SdkInfo) and one folder per API version (V1/, namespace Supertext.Sdk.V1).
(cd "$SRC/sdk/dotnet/src/Supertext.Sdk" && find . -name '*.cs' -not -path './bin/*' -not -path './obj/*') | while read -r f; do
    mkdir -p "$DEST/src/$(dirname "$f")"
    cp "$SRC/sdk/dotnet/src/Supertext.Sdk/$f" "$DEST/src/$f"
done
cp "$SRC/LICENSE" "$DEST/LICENSE"
find "$DEST/src" -name '*.cs' -exec sed -i 's/\bSupertext\.Sdk\b/Supertext.Umbraco.Translation.Sdk/g' {} +
printf '%s\n' "$COMMIT" > "$DEST/COMMIT"
printf '%s\n' "$VERSION" > "$DEST/VERSION"
cat > "$DEST/README.md" <<EOT
# Supertext SDK (bundled copy)

.NET part of the [Supertext SDK](https://github.com/Supertext/Supertext-API-SDK-Guide),
version $VERSION, commit \`$COMMIT\`, namespace \`Supertext.Umbraco.Translation.Sdk\` instead of \`Supertext.Sdk\`
(the API v1 classes in \`Supertext.Umbraco.Translation.Sdk.V1\`, folder \`src/V1/\`).
Compiled into Supertext.Umbraco.Translation.dll (see the package's \`.csproj\`).

Don't edit these files: fix the SDK in its own repository, then run \`scripts/vendor-sdk.sh\`.
EOT
if grep -rqE 'Supertext\.Sdk([^.A-Za-z]|$)' "$DEST/src"; then
    echo "vendor-sdk: unprefixed Supertext.Sdk reference left in $DEST/src" >&2
    exit 1
fi
echo "lib/supertext-sdk: Supertext SDK (.NET) $VERSION from commit $COMMIT"
