#!/usr/bin/env bash
set -euo pipefail
reports="$RUNNER_TEMP/reports"
cd "$GITHUB_WORKSPACE/src/Experiments/JitKnownPrefixCaller"
node extract.mjs
printf '%s\n' '{"sdk":{"version":"11.0.100","rollForward":"latestPatch"}}' > global.json
dotnet build -c Release
dll=bin/Release/net11.0/CohesionProbe.dll
for layout in layout-{A,B,C}; do
  "$RUNNER_TEMP/$layout/corerun" "$dll" --verify | tee -a "$reports/verification.txt"
  DOTNET_JitDisasm='Pages:FirstDifference Pages:JointRunBytes PageImageCodec:FirstDifference SqlSchemaProvisioner:*' DOTNET_JitStdOutFile="$reports/$layout-tier.asm" "$RUNNER_TEMP/$layout/corerun" "$dll" --jit
done
cpu=$(awk '/Cpus_allowed_list:/ {print $2}' /proc/self/status | cut -d, -f1 | cut -d- -f1)
echo "Benchmark CPU: $cpu" | tee -a "$reports/machine.txt"
taskset -c "$cpu" dotnet run -c Release --no-build -- --filter '*' --coreRun "$RUNNER_TEMP"/layout-{A,B,C}/corerun --launchCount 2 --warmupCount 5 --iterationCount 9 --iterationTime 100 --exporters json --artifacts "$reports/pages" > "$reports/pages-console.log" 2>&1 || { tail -100 "$reports/pages-console.log"; exit 1; }
tail -6 "$reports/pages-console.log"
