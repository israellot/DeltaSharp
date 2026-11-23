#!/usr/bin/env bash
set -euo pipefail
reports="$RUNNER_TEMP/reports"
cd "$GITHUB_WORKSPACE/src/Experiments/JitKnownPrefix"
# Pin the driver SDK independently of the experimental runtime under test.
printf '%s\n' '{"sdk":{"version":"11.0.100","rollForward":"latestPatch"}}' > global.json
dotnet build -c Release
for layout in layout-{A,B,C,D}; do
  echo "$layout" >> "$reports/verification.txt"
  for intrinsics in 0 1; do
    DOTNET_EnableHWIntrinsic="$intrinsics" "$RUNNER_TEMP/$layout/corerun" bin/Release/net11.0/PrefixKnown.dll --verify | tee -a "$reports/verification.txt"
  done
  DOTNET_JitDisasm='PrefixSites:* FixedCaller:FrontCode8' DOTNET_JitStdOutFile="$reports/$layout-tier.asm" "$RUNNER_TEMP/$layout/corerun" bin/Release/net11.0/PrefixKnown.dll --jit
done
cpu=$(awk '/Cpus_allowed_list:/ {print $2}' /proc/self/status | cut -d, -f1 | cut -d- -f1)
echo "Benchmark CPU: $cpu" | tee -a "$reports/machine.txt"
common=(--coreRun "$RUNNER_TEMP"/layout-{A,B,C,D}/corerun --launchCount 3 --warmupCount 5 --iterationCount 9 --iterationTime 100 --exporters json)
taskset -c "$cpu" dotnet run -c Release --no-build -- --filter '*Dynamic.BothUnknown*Mix0to32*' '*Dynamic.BothUnknown*First0to32*' '*Dynamic.OneKnown*Mix0to32*' '*Fixed.Known8*First*' '*Fixed.Known16*Varied*' "${common[@]}" --artifacts "$reports/default" > "$reports/default-console.log" 2>&1 || { tail -100 "$reports/default-console.log"; exit 1; }
tail -6 "$reports/default-console.log"
