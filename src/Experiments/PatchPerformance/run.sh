#!/usr/bin/env bash
set -euo pipefail

# Build isolated copies so no benchmark policy or switch enters the shipped encoder.
experiment=$(cd "$(dirname "$0")" && pwd)
repo=$(git -C "$experiment" rev-parse --show-toplevel)
framework=${1:-net11.0}
case "$framework" in
    net11.0) variants=(Current Cap4K Cap16K Cap64K Cap4KTrailer Prefix Generic Split) ;;
    net8.0) variants=(Current Prefix Split) ;;
    *) echo "Expected net11.0 or net8.0" >&2; exit 2 ;;
esac
work=$(mktemp -d)
artifacts=${2:-"$repo/BenchmarkDotNet.Artifacts/patch-performance-$framework"}
mkdir -p "$artifacts" "$work/stock" "$work/dlls" "$work/runner"
artifacts=$(cd "$artifacts" && pwd)
trap 'rm -rf "$work"' EXIT
ref=7acaf871b4e3d2cb3e141b2af08481e4e8f59096
git -C "$repo" archive "$ref:src/DeltaSharp" | tar -x -C "$work/stock"
rm "$work/stock/DeltaSharp.csproj"
export DELTA_TRIAL_DLLS="$work/dlls"
export DELTA_TRIAL_DATA="$repo/src/Benchmarks/Data"

for variant in "${variants[@]}"; do
    dir="$work/$variant"
    cp -R "$work/stock" "$dir"
    cp "$experiment/Codec.cs" "$dir"
    case "$variant" in
        Generic) (cd "$dir" && patch -s -p1 < "$experiment/generic.patch") ;;
        Split) (cd "$dir" && patch -s -p1 < "$experiment/split-control.patch") ;;
        Prefix) sed 's/SpanHelper.MatchForward(next, origin.Slice(p))/next.CommonPrefixLength(origin.Slice(p))/' "$dir/DeltaCreate.cs" > "$dir/changed"
                mv "$dir/changed" "$dir/DeltaCreate.cs" ;;
        Cap*) cap=${variant#Cap}; cap=${cap%%K*}
               sed "s@Math.Min(origin.Length / 2, target.Length)@Math.Min($((cap * 1024)), Math.Min(origin.Length / 2, target.Length))@" "$dir/DeltaCreate.cs" > "$dir/changed"
               mv "$dir/changed" "$dir/DeltaCreate.cs"
               if [[ "$variant" == Cap4KTrailer ]]; then
                   (cd "$dir" && patch -s -p1 < "$experiment/literal-trailer.patch")
               fi ;;
    esac
    cat > "$dir/$variant.csproj" <<PROJECT
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
PROJECT
    dotnet build "$dir/$variant.csproj" -c Release -v quiet
    cp "$dir/bin/Release/$framework/$variant.dll" "$DELTA_TRIAL_DLLS"
done

cd "$work/runner"
cp "$experiment/Program.cs" .
cat > Runner.csproj <<PROJECT
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$framework</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.16.0-preview.2" />
  </ItemGroup>
</Project>
PROJECT
dotnet build -c Release -v quiet
dotnet run --no-build -c Release -- --verify | tee "$artifacts/verification.csv"
if (( $# > 2 )); then
    filters=("${@:3}")
elif [[ "$framework" == net11.0 ]]; then
    filters=('*')
else
    filters=('*Creation*')
fi
dotnet run --no-build -c Release -- --filter "${filters[@]}" --launchCount 2 --warmupCount 5 --iterationCount 8 --iterationTime 150 --exporters json --artifacts "$artifacts"
