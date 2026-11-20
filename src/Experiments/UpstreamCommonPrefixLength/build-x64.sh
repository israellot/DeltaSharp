#!/usr/bin/env bash
set -euo pipefail
bench="$GITHUB_WORKSPACE/src/Experiments/UpstreamCommonPrefixLength"
runtime="$RUNNER_TEMP/runtime"
reports="$RUNNER_TEMP/reports"

mkdir -p "$runtime"
curl -fsSL --retry 3 "https://codeload.github.com/dotnet/runtime/tar.gz/$BASE_SHA" | tar -xz -C "$runtime" --strip-components=1
cd "$runtime"
git apply --check "$bench/four-byte-loop.patch"
# Both layouts share one checkout, bootstrap framework, desktop JIT and VM.
./build.sh clr.runtime+clr.nativecorelib -c Release /p:_RepoToolManifest="$RUNNER_TEMP/no-optional-tools" > "$reports/stock-build.log" 2>&1 || { tail -100 "$reports/stock-build.log"; exit 1; }
core="$runtime/artifacts/bin/coreclr/linux.x64.Release"
bootstrap=( "$runtime"/.dotnet/shared/Microsoft.NETCore.App/12.0.* )
test "${#bootstrap[@]}" -eq 1
mkdir -p "$RUNNER_TEMP/layout-A" "$RUNNER_TEMP/layout-B"
stock=layout-A
candidate=layout-B
if [[ "$REPLICA" == 2 ]]; then stock=layout-B; candidate=layout-A; fi
printf 'stock=%s\ncandidate=%s\nupstream=%s\n' "$stock" "$candidate" "$BASE_SHA" > "$reports/layouts.txt"
for layout in "$stock" "$candidate"; do
  cp -a "${bootstrap[0]}/." "$RUNNER_TEMP/$layout/"
  cp "$core/corerun" "$core/libcoreclr.so" "$core/libclrjit.so" "$core/libclrgc.so" "$RUNNER_TEMP/$layout/"
done
cp "$core/System.Private.CoreLib.dll" "$RUNNER_TEMP/$stock/"
git apply "$bench/four-byte-loop.patch"
./build.sh clr.nativecorelib -c Release /p:_RepoToolManifest="$RUNNER_TEMP/no-optional-tools" > "$reports/candidate-build.log" 2>&1 || { tail -100 "$reports/candidate-build.log"; exit 1; }
cp "$core/System.Private.CoreLib.dll" "$RUNNER_TEMP/$candidate/"
diff -qr -x System.Private.CoreLib.dll "$RUNNER_TEMP/layout-A" "$RUNNER_TEMP/layout-B"
if cmp -s "$RUNNER_TEMP/layout-A/System.Private.CoreLib.dll" "$RUNNER_TEMP/layout-B/System.Private.CoreLib.dll"; then exit 1; fi
sha256sum "$RUNNER_TEMP"/layout-{A,B}/System.Private.CoreLib.dll | tee "$reports/corelib-hashes.txt"
