# Native Linux x64 results for the minimal CommonPrefixLength patch

**Recommendation: take the minimal patch upstream.** On two separate x64 VMs it reduced mean time on mixed 4–7-byte comparisons by 14.8% and 14.3%, and on mixed 0–32-byte comparisons by 9.6% and 9.4%. The earlier Apple M3 Max measurements on the same upstream revision gave reductions of 24.7% and 8.9%. The patch keeps the existing length handling and replaces the four scalar byte comparisons inside one loop with an unaligned 32-bit XOR and a bit count on mismatch. [Patch including boundary tests](../../Experiments/UpstreamCommonPrefixLength/four-byte-loop.patch).

**The four-byte first-mismatch regression is reproducible.** When four-byte inputs differed at byte zero, the patch took an extra 0.143 ns on runner 1 and 0.382 ns on runner 2 (+10.5% and +28.2%). The stock loop can immediately return on that first byte. Both patched launches on each VM showed the slowdown. Its magnitude varied by VM. On *mixed lengths* of 4–7 with immediate mismatches, time rose by just 1.5% and 0.5%. For mixed 0–32 immediate mismatches, it moved by −0.5% and +4.9%. Include these cases in the upstream issue or PR.

**Framework impact.** The public ASN.1 DER decoder verified a set of 64 seven-byte encoded values with a shared prefix in 300.9 ns versus 314.7 ns on runner 1 and 304.0 ns versus 311.7 ns on runner 2. That is 4.4% and 2.5% less time per set, alongside 6.3% on Arm64. On mixed DER values the differences were smaller (2.0% and 1.1%) and their error intervals overlap. These are synthetic inputs to the framework decoder. With hardware intrinsics disabled, 64-byte equal spans took about 70% less time; with normal hardware settings that case improved only 1–2%.

## Method and evidence

- GitHub Actions ran two Ubuntu 24.04 VMs with four logical CPUs each, both reporting **Intel Xeon 6975P-C** under KVM. The benchmark driver was pinned to logical CPU 0 in each VM. The measured runtime was .NET 12.0.0-dev, X64 RyuJIT, x86-64-v4. The two VMs replicate a single CPU model. [Successful run, job logs and downloadable raw artifacts](https://github.com/israellot/DeltaSharp/actions/runs/44807350890).
- Both runners independently built stock and patched Release CoreLib from upstream commit `3d10ab44c1e907e5654f9100442112c41be4cd54`, with shared native runtime and JIT binaries on each runner. The jobs verified that only System.Private.CoreLib.dll differed between the layout directories. Runner 1 measured stock first, and runner 2 measured the patch first. [Workflow and exact job configuration](https://github.com/israellot/DeltaSharp/blob/adc3ceba629428259315a123f67f5c220e4582ee/.github/workflows/x64-prefix-benchmark.yml).
- Both stock and patched builds passed **761,472 byte/ushort/uint prefix checks per configuration**, with intrinsics enabled and disabled on each VM. BenchmarkDotNet completed all 96 timing cases across both VMs: 42 default and six software-fallback cases per VM. Each case used two process launches, five warmups, nine measured iterations and a 120 ms iteration target. [Benchmark and correctness driver](https://github.com/israellot/DeltaSharp/blob/adc3ceba629428259315a123f67f5c220e4582ee/src/Experiments/UpstreamCommonPrefixLength/Program.cs).
- Direct timings are per comparison over 256 cached span pairs. A DER timing covers an entire 64-value set. The tables show arithmetic means and BenchmarkDotNet's half-width of the 99.9% confidence interval. Negative percentages indicate less time. The noisy empty-span result on runner 2 was not replicated on runner 1, and its confidence interval overlaps stock.

## Measurements

### Default hardware intrinsics

| Workload | Runner 1 stock (ns) | Patch (ns) | Time change | Runner 2 stock (ns) | Patch (ns) | Time change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DerSet/Mixed | 283.054 ± 1.208 | 277.294 ± 5.355 | -2.0% | 285.928 ± 1.923 | 282.733 ± 1.536 | -1.1% |
| DerSet/Shared7 | 314.655 ± 4.279 | 300.935 ± 5.863 | -4.4% | 311.685 ± 2.602 | 303.958 ± 3.614 | -2.5% |
| Prefix/0-equal | 1.236 ± 0.011 | 1.233 ± 0.014 | -0.2% | 1.230 ± 0.010 | 1.410 ± 0.331 | +14.7% |
| Prefix/1-equal | 1.492 ± 0.020 | 1.489 ± 0.004 | -0.2% | 1.488 ± 0.008 | 1.490 ± 0.014 | +0.2% |
| Prefix/1024-equal | 40.707 ± 0.319 | 39.350 ± 1.236 | -3.3% | 41.012 ± 0.572 | 40.589 ± 1.153 | -1.0% |
| Prefix/15-last | 4.257 ± 0.065 | 2.708 ± 0.017 | -36.4% | 4.209 ± 0.024 | 2.708 ± 0.014 | -35.7% |
| Prefix/16-last | 1.552 ± 0.034 | 1.569 ± 0.037 | +1.1% | 1.601 ± 0.020 | 1.576 ± 0.036 | -1.5% |
| Prefix/3-last | 1.771 ± 0.027 | 1.833 ± 0.088 | +3.5% | 1.749 ± 0.011 | 1.843 ± 0.094 | +5.4% |
| Prefix/4-first | 1.354 ± 0.005 | 1.497 ± 0.016 | +10.5% | 1.355 ± 0.005 | 1.737 ± 0.012 | +28.2% |
| Prefix/4-last | 1.815 ± 0.049 | 1.629 ± 0.139 | -10.3% | 1.793 ± 0.021 | 1.621 ± 0.135 | -9.6% |
| Prefix/5-last | 2.020 ± 0.006 | 1.848 ± 0.099 | -8.5% | 2.024 ± 0.007 | 1.852 ± 0.105 | -8.5% |
| Prefix/6-last | 2.424 ± 0.106 | 2.130 ± 0.017 | -12.1% | 2.312 ± 0.021 | 2.236 ± 0.111 | -3.3% |
| Prefix/64-equal | 2.750 ± 0.031 | 2.713 ± 0.015 | -1.3% | 2.769 ± 0.020 | 2.716 ± 0.038 | -1.9% |
| Prefix/7-equal | 2.628 ± 0.025 | 2.154 ± 0.006 | -18.1% | 2.606 ± 0.038 | 2.161 ± 0.007 | -17.1% |
| Prefix/7-first | 1.224 ± 0.003 | 1.224 ± 0.003 | -0.0% | 1.233 ± 0.007 | 1.227 ± 0.004 | -0.4% |
| Prefix/7-last | 2.641 ± 0.068 | 2.190 ± 0.027 | -17.1% | 2.546 ± 0.154 | 2.174 ± 0.010 | -14.6% |
| Prefix/8-last | 2.705 ± 0.011 | 1.812 ± 0.026 | -33.0% | 2.734 ± 0.024 | 1.813 ± 0.015 | -33.7% |
| Prefix/vary0to32 | 2.010 ± 0.030 | 1.816 ± 0.013 | -9.6% | 1.993 ± 0.012 | 1.806 ± 0.044 | -9.4% |
| Prefix/vary0to32-first | 1.422 ± 0.070 | 1.415 ± 0.005 | -0.5% | 1.353 ± 0.011 | 1.419 ± 0.010 | +4.9% |
| Prefix/vary4to7 | 2.005 ± 0.014 | 1.709 ± 0.013 | -14.8% | 1.993 ± 0.013 | 1.708 ± 0.011 | -14.3% |
| Prefix/vary4to7-first | 1.388 ± 0.017 | 1.409 ± 0.024 | +1.5% | 1.429 ± 0.011 | 1.436 ± 0.007 | +0.5% |

### Hardware intrinsics disabled

| Workload | Runner 1 stock (ns) | Patch (ns) | Time change | Runner 2 stock (ns) | Patch (ns) | Time change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Prefix/1024-equal | 198.950 ± 7.187 | 84.365 ± 4.808 | -57.6% | 200.195 ± 10.035 | 81.015 ± 0.766 | -59.5% |
| Prefix/15-last | 4.192 ± 0.038 | 2.581 ± 0.009 | -38.4% | 4.224 ± 0.056 | 2.589 ± 0.015 | -38.7% |
| Prefix/64-equal | 23.351 ± 0.272 | 7.013 ± 0.106 | -70.0% | 23.294 ± 0.262 | 7.081 ± 0.081 | -69.6% |
