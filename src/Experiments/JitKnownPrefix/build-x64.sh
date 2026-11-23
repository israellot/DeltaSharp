#!/usr/bin/env bash
set -euo pipefail
bench="$GITHUB_WORKSPACE/src/Experiments/JitKnownPrefix"
runtime="$RUNNER_TEMP/runtime"
reports="$RUNNER_TEMP/reports"

mkdir -p "$runtime"
curl -fsSL --retry 3 "https://codeload.github.com/dotnet/runtime/tar.gz/$BASE_SHA" | tar -xz -C "$runtime" --strip-components=1
cd "$runtime"
git apply --check "$bench/known48.patch"
git apply --check "$bench/known4816.patch"
# Both layouts share one checkout, bootstrap framework, desktop JIT and VM.
./build.sh clr.runtime+clr.nativecorelib -c Release /p:_RepoToolManifest="$RUNNER_TEMP/no-optional-tools" > "$reports/stock-build.log" 2>&1 || { tail -100 "$reports/stock-build.log"; exit 1; }
core="$runtime/artifacts/bin/coreclr/linux.x64.Release"
bootstrap=( "$runtime"/.dotnet/shared/Microsoft.NETCore.App/12.0.* )
test "${#bootstrap[@]}" -eq 1
variants=(stock known48 known4816)
layouts=(layout-A layout-B layout-C)
if [[ "$REPLICA" == 2 ]]; then layouts=(layout-C layout-B layout-A); fi
printf 'upstream=%s\n' "$BASE_SHA" > "$reports/layouts.txt"
stockdir="$RUNNER_TEMP/${layouts[0]}"
for i in "${!variants[@]}"; do
  variant=${variants[$i]}
  layout=${layouts[$i]}
  printf '%s=%s\n' "$variant" "$layout" >> "$reports/layouts.txt"
  patch="$bench/$variant.patch"
  dest="$RUNNER_TEMP/$layout"
  if [[ "$variant" == stock ]]; then
    mkdir -p "$dest"
    cp -a "${bootstrap[0]}/." "$dest/"
    cp "$core/corerun" "$core/libcoreclr.so" "$core/libclrjit.so" "$core/libclrgc.so" "$dest/"
  else
    cp -al "$stockdir" "$dest"
    # Each layout needs its own writable CoreLib inode.
    rm "$dest/System.Private.CoreLib.dll"
    git apply "$patch"
    ./build.sh clr.nativecorelib -c Release /p:_RepoToolManifest="$RUNNER_TEMP/no-optional-tools" > "$reports/$variant-build.log" 2>&1 || { tail -100 "$reports/$variant-build.log"; exit 1; }
    git apply --reverse "$patch"
  fi
  cp "$core/System.Private.CoreLib.dll" "$dest/"
  if [[ "$variant" != stock ]]; then
    diff -qr -x System.Private.CoreLib.dll "$stockdir" "$dest"
    if cmp -s "$stockdir/System.Private.CoreLib.dll" "$dest/System.Private.CoreLib.dll"; then exit 1; fi
  fi
done
sha256sum "$RUNNER_TEMP"/layout-{A,B,C}/System.Private.CoreLib.dll | tee "$reports/corelib-hashes.txt"
test "$(cut -d' ' -f1 "$reports/corelib-hashes.txt" | sort -u | wc -l)" -eq 3
