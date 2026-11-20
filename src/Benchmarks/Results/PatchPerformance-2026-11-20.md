# Patch performance experiments on Apple M3 Max

The writer and generic dispatch changes were subsequently integrated into the library, with reusable destinations exposed through the static apply API. [Production-path timings and allocations](PatchOperations-Integrated-2026-11-20.md).

On November 20, 2026 we measured four changes to DeltaSharp. Reusing the apply destination reduced both latency and allocations on every tested workload. Smaller initial writer buffers delivered the largest creation-side allocation reduction. Isolating the search loop improved .NET 11 creation times on four follow-up workloads with little change on .NET 8. The standard forward matcher offered no consistent speed advantage. The experiment leaves the production encoder and decoder unchanged.

## Reusing the apply destination

The existing caller-buffer overload had the clearest result. Across five inputs, reusing a non-overlapping array left 40 allocated bytes per call and reduced mean time by 9–36%. On the 1 MiB sparse target it cut 154.0 µs to 99.2 µs and 1,049,849 allocated bytes to 40. On the two book editions it cut 250.5 µs to 181.3 µs. The preallocated destination is still required once. Each returned `DeltaResult.Data` points into it, so consume the result before writing the next patch to that buffer. The implementation copies overlapping source or patch bytes when needed. [Apply implementation](../../DeltaSharp/DeltaApply.cs).

```csharp
var restored = DeltaSharp.DeltaSharp.Binary.Apply.Apply(source, patch.Data.Span, destination);
// Read restored.Data before the next operation reuses destination.
```

## Sizing the writer

The current capacity hint is the smaller of half the source length and the target length. A 1 MiB sparse update emits a 2,808-byte Binary patch yet retains a 524,288-byte array through the result. Capping the initial capacity at 4 KiB cut measured allocation from 525,475 to 4,264 B/op and mean creation time from 340.3 to 298.7 µs in the primary run. The 16 KiB and 64 KiB caps gave similar times on this input and retained progressively larger arrays.

For the 46,294-byte book patch, all three caps retained a 65,536-byte array, down from 626,975 bytes. Starting at 64 KiB avoided resizing for this patch and gave the lowest allocation (65,704 B/op). Starting at 4 KiB allocated 127,240 B/op across successive buffers. That tradeoff makes the initial-capacity choice workload-dependent. The earlier short trial's book slowdown with 4 KiB did not reproduce in this primary run.

For dense and unrelated 1 MiB inputs, all three caps still retained about 2 MiB. The final literal consumes the grown buffer almost exactly. Appending the checksum then doubles the array. The Binary writer prototype combines the 4 KiB cap with reserving ten bytes of headroom when an insert of at least 4 KiB would require at least twice the current capacity. Ten bytes accommodate a command byte plus the maximum bytes the writer reserves for an encoded integer. The buffer sizing does not change the emitted commands. [Writer prototype](../../Experiments/PatchPerformance/literal-trailer.patch).

On the 1 MiB unrelated case, the patch remained **1,048,592 bytes**. The current writer allocated **3,670,857 B/op**, the 4 KiB cap alone allocated **3,151,292 B/op**, and adding headroom brought that down to **1,052,888 B/op**. The returned array shrank from **2,097,172 to 1,048,596 bytes**. The dense-change case had essentially the same allocations. For these inputs most of the benefit is memory; encoding still spends about 19 ms searching for matches. I would carry the 4 KiB cap plus literal headroom into a production candidate, apply the policy in the built-in writers, and preserve the capacity hint seen by third-party writers.

## Forward matching

Keep the existing SIMD matcher for these workloads. On .NET 11 the library's `CommonPrefixLength` replacement measured 0.8% slower on sparse edits and 0.3% faster on the book in the primary run. The other five cases were 1–4% slower. The approximately 12% sparse-edit improvement seen in the earlier short trial did not reproduce here. On .NET 8.0.25 the replacement was slower on all seven workloads, with changes ranging from +3% on dense edits to +43% on 32 KiB unrelated inputs. The change affects only the forward match; both paths keep the same backward matcher. [Forward match implementation](../../DeltaSharp/Util/SpanHelper.cs). In a second .NET 8 run of the 32 KiB unrelated case, the replacement was again slower (289.9 vs 230.0 µs).

## Hash-table dispatch and search-loop layout

The generic prototype selects the index width once per patch, then calls a search helper constrained by `where TTable : IProbeTable, allows ref struct`. The production wrapper selects the width inside each `Match` and `More`. The prototype also moves the search loop out of the method that initializes and disposes the index. A second prototype makes just that method split and passes the original wrapper to the helper. [Generic prototype](../../Experiments/PatchPerformance/generic.patch) · [Method-split control](../../Experiments/PatchPerformance/split-control.patch).

Fresh .NET 11 comparisons within the same batch:

| Workload | Current | Split only | Generic dispatch |
| --- | ---: | ---: | ---: |
| 1 KiB unrelated | 4.38 µs | 3.26 µs | 3.40 µs |
| 32 KiB repeated records | 2.349 ms | 1.954 ms | 2.294 ms |
| Moby-Dick | 1.208 ms | 0.894 ms | 0.919 ms |
| 1 MiB unrelated | 19.508 ms | 17.398 ms | 16.694 ms |

Generic dispatch improved on the original in both .NET 11 batches. Against the split-only control, it won on the 1 MiB unrelated case by 4% and had higher mean times on the other three. Moving the loop alone explains much of the observed gain. Which JIT decisions dominate would need separate confirmation under tiered compilation. The 1 KiB case allocated 40 additional bytes per operation in both refactors. The experiment uses the default index widths; the optional 24-bit path would need separate coverage before adopting a specialized implementation.

On .NET 8 the split-only means were within 1.4% of the existing code on all four follow-up workloads, including 1.032 ms vs 1.031 ms for the book. I would prioritize the simpler method split for a production trial and measure it on x64 before adopting it. Keep generic dispatch as a separate option for applications dominated by unrelated inputs.

A separate .NET 11 Arm64 JIT inspection with tiering disabled confirmed that the generic helper produced three specialized bodies (848, 908 and 884 bytes for 8-, 16- and 32-bit indices). The split-only helper produced one 1,260-byte body. Those are helper code sizes under FullOpts; the timing runs used normal tiering.

## How to reproduce the results

From the repository root, use the .NET 11 SDK, the corresponding .NET 11 and .NET 8 runtimes, Git, Bash, tar and patch. The runner extracts the production encoder at `7acaf871b4e3d2cb3e141b2af08481e4e8f59096` and builds each alternative in a temporary directory. No experimental code is compiled into the package.

```sh
bash src/Experiments/PatchPerformance/run.sh net11.0
bash src/Experiments/PatchPerformance/run.sh net8.0
```

The first command runs all four investigations, including the large-literal writer. The second compares the stock encoder, the forward matcher, and the method-split control on .NET 8. BenchmarkDotNet writes its per-iteration JSON, CSV and machine information to `BenchmarkDotNet.Artifacts/patch-performance-net*/results`. The adjacent `verification.csv` records encoded length and backing-array capacity. [Benchmark and verification cases](../../Experiments/PatchPerformance/Program.cs) · [Runner and variant definitions](../../Experiments/PatchPerformance/run.sh).

### Measurement conditions

- Apple M3 Max, 14 physical cores, macOS 27.0.1, SDK 11.0.100, Arm64 Release, Workstation GC, BenchmarkDotNet 0.16.0-preview.2. The runtime versions actually measured were 11.0.0 and 8.0.25.
- Each case used two fresh processes, five warmups and eight measured iterations per process with a 150 ms target. Reported times are BenchmarkDotNet means after its default outlier handling. Each case uses the same delegate boundary; input preparation, reflection and correctness checks run outside the timed operations. Each process reuses its input and warms the index pools. Timing runs were sequential on one machine.
- Timing uses Binary patches with the default exhaustive search and the default 8-, 16- and 32-bit table selection. Verification also covers Fossil and ASCII, both search modes, boundaries at 2,047/2,048 and 524,279/524,280 bytes, tiny inputs, and large literals adjacent to COPY and checksum commands. Each variant is checked against the stock patch bytes, and both apply paths reconstruct the target.
- Allocated bytes are managed bytes per operation measured by BenchmarkDotNet. Retained capacity is the length of the actual array behind the returned patch, inspected outside the timed loop. These metrics exclude memory already retained by warmed pools. Application uses a separate, already allocated destination per worker and consumes each result before reusing that destination.
- The .NET 11 large-literal variant was measured in a follow-up batch. Repeated controls from that batch are identified in the detailed results. All comparisons concern these workloads and this processor; close differences, especially when error intervals overlap, do not establish a speed advantage.

| Workload | Source and target | Purpose |
| --- | --- | --- |
| Random1K | Two independently generated 1 KiB buffers | 8-bit table, mostly failed candidate matches |
| Random32K | Two independently generated 32 KiB buffers | 16-bit table, mostly failed candidate matches |
| Records32K | 512 copies of a 64-byte record with two different edit positions | 16-bit table, many candidates sharing a prefix |
| Sparse1M | 1 MiB pseudorandom source, one changed byte every 4 KiB | 32-bit table, long matches and a small patch |
| Dense1M | 1 MiB pseudorandom source, one changed byte every four bytes | 32-bit table, very few reusable bytes |
| MobyDick | Bundled pg15.txt → pg2701.txt, 1,253,974-byte target | Real text, mixed COPY and INSERT commands |
| Random1M | Two independently generated 1 MiB buffers | 32-bit table, large literal |

All synthetic inputs use seed 71237. The checksum-reservation experiment changes only the Binary writer relative to the 4 KiB cap alone. Other formats participate in equivalence checks.

The final verification matrix passed 1,638 differential comparisons on .NET 11 (seven candidates × 39 input pairs × three formats × two modes), and 468 on .NET 8 (two candidates). For every comparison, both candidate and stock patches also reconstructed the target using allocating and caller-buffer apply. The measurements below contain 103 completed BenchmarkDotNet cases across the primary and follow-up batches.

## Detailed results

The error column is half of BenchmarkDotNet’s 99.9% confidence interval for the mean. Repeated controls are shown with the batch in which they were measured.

### .NET 11 application

| Input | Variant | Mean (µs) | ± error (µs) | Allocated (B/op) |
| --- | --- | ---: | ---: | ---: |
| Random1K | Allocate | 0.145 | 0.001 | 1,088 |
| Random1K | Reuse | 0.105 | 0.001 | 40 |
| Records32K | Allocate | 10.28 | 0.092 | 32,832 |
| Records32K | Reuse | 9.33 | 0.074 | 40 |
| Sparse1M | Allocate | 154.0 | 2.18 | 1,049,849 |
| Sparse1M | Reuse | 99.17 | 0.911 | 40 |
| MobyDick | Allocate | 250.5 | 6.77 | 1,255,345 |
| MobyDick | Reuse | 181.3 | 1.46 | 40 |
| Random1M | Allocate | 134.3 | 1.25 | 1,049,643 |
| Random1M | Reuse | 88.47 | 1.39 | 40 |

### .NET 11 creation

| Input | Variant | Mean (µs) | ± error (µs) | Allocated (B/op) | Retained patch array (B) |
| --- | --- | ---: | ---: | ---: | ---: |
| Random1K | Current | 4.45 | 0.093 | 3,824 | 2,060 |
| Random1K | 4 KiB | 4.10 | 0.079 | 3,824 | 2,060 |
| Random1K | 16 KiB | 3.88 | 0.025 | 3,824 | 2,060 |
| Random1K | 64 KiB | 3.90 | 0.017 | 3,824 | 2,060 |
| Random1K | CommonPrefixLength | 4.54 | 0.088 | 3,824 | 2,060 |
| Random1K | Generic dispatch | 3.13 | 0.267 | 3,864 | 2,060 |
| Random32K | Current | 348.5 | 14.50 | 131,336 | 65,552 |
| Random32K | 4 KiB | 333.4 | 6.66 | 119,048 | 65,552 |
| Random32K | 16 KiB | 344.7 | 9.46 | 131,336 | 65,552 |
| Random32K | 64 KiB | 349.7 | 9.74 | 131,336 | 65,552 |
| Random32K | CommonPrefixLength | 354.7 | 7.88 | 131,336 | 65,552 |
| Random32K | Generic dispatch | 325.3 | 7.02 | 131,336 | 65,552 |
| Records32K | Current | 2,351.8 | 29.75 | 32,960 | 16,384 |
| Records32K | 4 KiB | 2,357.7 | 33.69 | 20,672 | 4,096 |
| Records32K | 16 KiB | 2,339.5 | 27.05 | 32,960 | 16,384 |
| Records32K | 64 KiB | 2,358.5 | 28.75 | 32,960 | 16,384 |
| Records32K | CommonPrefixLength | 2,381.4 | 60.57 | 32,960 | 16,384 |
| Records32K | Generic dispatch | 2,309.3 | 61.03 | 32,960 | 16,384 |
| Sparse1M | Current | 340.3 | 9.14 | 525,475 | 524,288 |
| Sparse1M | 4 KiB | 298.7 | 1.29 | 4,264 | 4,096 |
| Sparse1M | 16 KiB | 301.5 | 2.25 | 16,552 | 16,384 |
| Sparse1M | 64 KiB | 299.4 | 1.59 | 65,704 | 65,536 |
| Sparse1M | CommonPrefixLength | 342.8 | 8.75 | 525,521 | 524,288 |
| Sparse1M | Generic dispatch | 331.8 | 4.62 | 525,525 | 524,288 |
| Dense1M | Current | 19,554.2 | 185.1 | 3,670,857 | 2,097,172 |
| Dense1M | 4 KiB | 18,982.5 | 149.5 | 3,151,292 | 2,097,172 |
| Dense1M | 16 KiB | 19,041.4 | 752.6 | 3,164,610 | 2,097,172 |
| Dense1M | 64 KiB | 18,961.5 | 220.1 | 3,213,762 | 2,097,172 |
| Dense1M | CommonPrefixLength | 20,355.1 | 239.3 | 3,670,857 | 2,097,172 |
| Dense1M | Generic dispatch | 16,540.6 | 296.0 | 3,670,896 | 2,097,172 |
| MobyDick | Current | 1,128.1 | 22.43 | 627,248 | 626,975 |
| MobyDick | 4 KiB | 1,063.3 | 29.07 | 127,240 | 65,536 |
| MobyDick | 16 KiB | 1,079.6 | 65.03 | 114,904 | 65,536 |
| MobyDick | 64 KiB | 1,040.1 | 28.44 | 65,704 | 65,536 |
| MobyDick | CommonPrefixLength | 1,124.2 | 21.79 | 627,248 | 626,975 |
| MobyDick | Generic dispatch | 939.8 | 41.09 | 627,250 | 626,975 |
| Random1M | Current | 19,490.2 | 191.5 | 3,670,857 | 2,097,172 |
| Random1M | 4 KiB | 19,259.7 | 190.3 | 3,151,292 | 2,097,172 |
| Random1M | 16 KiB | 18,853.9 | 296.4 | 3,164,610 | 2,097,172 |
| Random1M | 64 KiB | 18,999.7 | 202.4 | 3,213,762 | 2,097,172 |
| Random1M | CommonPrefixLength | 19,698.4 | 83.38 | 3,670,857 | 2,097,172 |
| Random1M | Generic dispatch | 16,774.0 | 222.2 | 3,670,896 | 2,097,172 |

### .NET 8 creation

| Input | Variant | Mean (µs) | ± error (µs) | Allocated (B/op) | Retained patch array (B) |
| --- | --- | ---: | ---: | ---: | ---: |
| Random1K | Current | 2.85 | 0.005 | 3,864 | 2,060 |
| Random1K | CommonPrefixLength | 3.29 | 0.036 | 3,864 | 2,060 |
| Random32K | Current | 210.4 | 17.98 | 131,376 | 65,552 |
| Random32K | CommonPrefixLength | 300.2 | 37.12 | 131,376 | 65,552 |
| Records32K | Current | 1,991.6 | 57.95 | 33,000 | 16,384 |
| Records32K | CommonPrefixLength | 2,252.6 | 13.69 | 33,000 | 16,384 |
| Sparse1M | Current | 357.8 | 4.74 | 730,158 | 524,288 |
| Sparse1M | CommonPrefixLength | 398.8 | 19.38 | 730,723 | 524,288 |
| Dense1M | Current | 18,335.6 | 409.3 | 3,934,105 | 2,097,172 |
| Dense1M | CommonPrefixLength | 18,902.3 | 49.62 | 3,934,105 | 2,097,172 |
| MobyDick | Current | 874.5 | 31.34 | 639,259 | 626,975 |
| MobyDick | CommonPrefixLength | 1,051.9 | 74.75 | 640,450 | 626,975 |
| Random1M | Current | 17,159.5 | 155.2 | 3,904,936 | 2,097,172 |
| Random1M | CommonPrefixLength | 20,130.9 | 315.8 | 3,802,341 | 2,097,172 |

### .NET 11 large-literal writer and search-loop controls

| Input | Variant | Mean (µs) | ± error (µs) | Allocated (B/op) | Retained patch array (B) |
| --- | --- | ---: | ---: | ---: | ---: |
| Random1K | Current | 4.38 | 0.017 | 3,824 | 2,060 |
| Random1K | 4 KiB + trailer | 4.13 | 0.063 | 3,824 | 2,060 |
| Random1K | Generic dispatch | 3.40 | 0.018 | 3,864 | 2,060 |
| Random1K | Split only | 3.26 | 0.104 | 3,864 | 2,060 |
| Random32K | Current | 352.2 | 15.26 | 131,336 | 65,552 |
| Random32K | 4 KiB + trailer | 327.7 | 5.47 | 53,488 | 32,786 |
| Records32K | Current | 2,348.7 | 10.43 | 32,960 | 16,384 |
| Records32K | 4 KiB + trailer | 2,316.5 | 12.69 | 20,672 | 4,096 |
| Records32K | Generic dispatch | 2,293.7 | 25.50 | 32,960 | 16,384 |
| Records32K | Split only | 1,954.4 | 28.34 | 33,000 | 16,384 |
| Sparse1M | Current | 348.4 | 9.08 | 525,457 | 524,288 |
| Sparse1M | 4 KiB | 297.3 | 1.88 | 4,264 | 4,096 |
| Sparse1M | 4 KiB + trailer | 292.1 | 0.932 | 4,264 | 4,096 |
| Sparse1M | CommonPrefixLength | 341.7 | 3.16 | 525,507 | 524,288 |
| Dense1M | Current | 19,525.7 | 183.0 | 3,670,807 | 2,097,172 |
| Dense1M | 4 KiB | 18,894.8 | 104.1 | 3,151,292 | 2,097,172 |
| Dense1M | 4 KiB + trailer | 19,026.4 | 1,197.9 | 1,052,888 | 1,048,596 |
| MobyDick | Current | 1,207.6 | 31.85 | 627,246 | 626,975 |
| MobyDick | 4 KiB | 1,013.5 | 56.34 | 127,240 | 65,536 |
| MobyDick | 4 KiB + trailer | 1,038.3 | 56.27 | 127,240 | 65,536 |
| MobyDick | Generic dispatch | 919.4 | 26.53 | 627,249 | 626,975 |
| MobyDick | Split only | 893.8 | 13.81 | 627,250 | 626,975 |
| Random1M | Current | 19,507.9 | 249.2 | 3,670,857 | 2,097,172 |
| Random1M | 4 KiB | 18,797.9 | 366.5 | 3,151,292 | 2,097,172 |
| Random1M | 4 KiB + trailer | 18,676.2 | 130.7 | 1,052,888 | 1,048,596 |
| Random1M | Generic dispatch | 16,694.3 | 162.5 | 3,670,896 | 2,097,172 |
| Random1M | Split only | 17,397.6 | 240.1 | 3,670,936 | 2,097,172 |

### .NET 8 forward matcher repeat

| Input | Variant | Mean (µs) | ± error (µs) | Allocated (B/op) | Retained patch array (B) |
| --- | --- | ---: | ---: | ---: | ---: |
| Random32K | Current | 230.0 | 34.13 | 131,376 | 65,552 |
| Random32K | CommonPrefixLength | 289.9 | 23.84 | 131,376 | 65,552 |

### .NET 8 search-loop control

| Input | Variant | Mean (µs) | ± error (µs) | Allocated (B/op) | Retained patch array (B) |
| --- | --- | ---: | ---: | ---: | ---: |
| Random1K | Current | 2.94 | 0.020 | 3,864 | 2,060 |
| Random1K | Split only | 2.98 | 0.028 | 3,864 | 2,060 |
| Records32K | Current | 2,081.0 | 22.25 | 33,000 | 16,384 |
| Records32K | Split only | 2,061.2 | 29.70 | 33,000 | 16,384 |
| MobyDick | Current | 1,031.1 | 37.32 | 640,450 | 626,975 |
| MobyDick | Split only | 1,031.8 | 39.20 | 641,907 | 626,975 |
| Random1M | Current | 17,804.7 | 209.6 | 3,934,105 | 2,097,172 |
| Random1M | Split only | 17,689.1 | 151.6 | 3,904,942 | 2,097,172 |
