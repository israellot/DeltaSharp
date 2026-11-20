#!/usr/bin/env bash
set -euo pipefail
reports="$RUNNER_TEMP/reports"
cd "$GITHUB_WORKSPACE/src/Experiments/UpstreamCommonPrefixLength"
# Pin the driver SDK independently of the experimental runtime under test.
printf '%s\n' '{"sdk":{"version":"11.0.100","rollForward":"latestPatch"}}' > global.json
dotnet build -c Release
for layout in layout-A layout-B; do
  for intrinsics in 0 1; do
    DOTNET_EnableHWIntrinsic="$intrinsics" "$RUNNER_TEMP/$layout/corerun" bin/Release/net11.0/PrefixCheck.dll --verify | tee -a "$reports/verification.txt"
  done
done
cpu=$(awk '/Cpus_allowed_list:/ {print $2}' /proc/self/status | cut -d, -f1 | cut -d- -f1)
echo "Benchmark CPU: $cpu" | tee -a "$reports/machine.txt"
common=(--coreRun "$RUNNER_TEMP/layout-A/corerun" "$RUNNER_TEMP/layout-B/corerun" --launchCount 2 --warmupCount 5 --iterationCount 9 --iterationTime 120 --exporters json)
for mode in default scalar; do
  filters=('*')
  if [[ "$mode" == scalar ]]; then
    export DOTNET_EnableHWIntrinsic=0
    filters=('*15-last*' '*64-equal*' '*1024-equal*')
  fi
  taskset -c "$cpu" dotnet run -c Release --no-build -- --filter "${filters[@]}" "${common[@]}" --artifacts "$reports/$mode" > "$reports/$mode-console.log" 2>&1 || { tail -100 "$reports/$mode-console.log"; exit 1; }
  tail -6 "$reports/$mode-console.log"
done
