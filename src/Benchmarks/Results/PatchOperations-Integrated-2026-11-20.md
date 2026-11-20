# Integrated patch operations on .NET 11 and Apple M3 Max

Measured November 20, 2026 after integrating capped writer buffers, format-specific checksum headroom, and generic table dispatch. Create measures the production exhaustive encoder. Apply allocates its destination. ApplyInto reuses a separate array allocated during setup. Both application paths verify the checksum. Setup also checks byte-for-byte reconstruction for every workload and format.

## Inputs and patch sizes

MobyDick uses the bundled pg15.txt as the source and pg2701.txt as the target. SparseEdits changes one byte every 4 KiB in a 1 MiB pseudorandom buffer. Unrelated uses two separately generated 1 MiB buffers. Both synthetic cases use seed 71237. All sizes are bytes.

| Workload | Source | Target | Binary patch | Fossil patch | ASCII patch |
| --- | ---: | ---: | ---: | ---: | ---: |
| MobyDick | 1,253,951 | 1,253,974 | 46,294 | 50,532 | 83,692 |
| SparseEdits | 1,048,576 | 1,048,576 | 2,808 | 2,762 | 5,122 |
| Unrelated | 1,048,576 | 1,048,576 | 1,048,592 | 1,048,593 | 1,048,609 |

## Timings and allocations

For the 1 MiB sparse edit, Binary creation allocated 4,304 bytes (previously 513.17 KiB). For unrelated 1 MiB inputs, the Binary patch held 1,048,592 bytes in a backing array of 1,048,596 bytes. Reusing the apply destination left 40 allocated bytes per call in every measured case.

Mean Binary creation time fell from 0.320 to 0.278 ms for sparse edits and from 16.710 to 15.593 ms for unrelated inputs. The book measured 0.888 ms versus 0.852 ms before integration, with overlapping timing intervals. These observations come from separate single-launch runs on the same machine.

BenchmarkDotNet ran one launch, six warmups, and twelve measurement iterations per case. Error is half the 99.9% confidence interval for the mean. Allocated measures managed bytes per operation and excludes the destination prepared once for ApplyInto. [Earlier measurements before integration](PatchOperations-2026-11-20.md).

```

BenchmarkDotNet v0.16.0-preview.2, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M3 Max, 1 CPU, 14 logical and 14 physical cores
Memory: 36 GB Total, 0.51 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-ESCNQV : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

Arguments=/p:Enable24BitHashtable=false  IterationCount=12  IterationTime=350ms
LaunchCount=1  WarmupCount=6

```
| Method    | Workload    | Format | Mean         | Error      | StdDev     | Gen0     | Gen1     | Gen2     | Allocated |
|---------- |------------ |------- |-------------:|-----------:|-----------:|---------:|---------:|---------:|----------:|
| **Create**    | **MobyDick**    | **ASCII**  |    **987.13 μs** |  **86.503 μs** |  **67.536 μs** |  **30.0000** |  **15.0000** |  **15.0000** |  **258448 B** |
| Apply     | MobyDick    | ASCII  |    242.88 μs |   5.810 μs |   4.536 μs | 139.0449 | 139.0449 | 139.0449 | 1255267 B |
| ApplyInto | MobyDick    | ASCII  |    182.90 μs |   1.574 μs |   1.138 μs |        - |        - |        - |      40 B |
| **Create**    | **MobyDick**    | **Binary** |    **888.16 μs** |  **32.721 μs** |  **25.547 μs** |  **13.0208** |   **2.6042** |        **-** |  **127280 B** |
| Apply     | MobyDick    | Binary |    250.02 μs |   9.672 μs |   6.994 μs | 140.4412 | 140.4412 | 140.4412 | 1255312 B |
| ApplyInto | MobyDick    | Binary |    176.84 μs |   2.608 μs |   2.036 μs |        - |        - |        - |      40 B |
| **Create**    | **MobyDick**    | **Fossil** |    **763.90 μs** |  **32.947 μs** |  **25.723 μs** |  **14.5833** |   **2.0833** |        **-** |  **127280 B** |
| Apply     | MobyDick    | Fossil |    222.18 μs |  14.559 μs |  11.367 μs | 141.5094 | 141.5094 | 141.5094 | 1255317 B |
| ApplyInto | MobyDick    | Fossil |    162.35 μs |   1.466 μs |   1.145 μs |        - |        - |        - |      40 B |
| **Create**    | **SparseEdits** | **ASCII**  |    **285.32 μs** |   **3.137 μs** |   **2.449 μs** |   **0.8013** |        **-** |        **-** |   **12520 B** |
| Apply     | SparseEdits | ASCII  |    153.80 μs |  13.115 μs |  10.239 μs | 139.7804 | 139.7804 | 139.7804 | 1049862 B |
| ApplyInto | SparseEdits | ASCII  |     97.17 μs |   0.381 μs |   0.276 μs |        - |        - |        - |      40 B |
| **Create**    | **SparseEdits** | **Binary** |    **278.19 μs** |   **1.975 μs** |   **1.428 μs** |        **-** |        **-** |        **-** |    **4304 B** |
| Apply     | SparseEdits | Binary |    144.89 μs |   2.744 μs |   1.815 μs | 141.1565 | 141.1565 | 141.1565 | 1049875 B |
| ApplyInto | SparseEdits | Binary |     99.82 μs |   0.305 μs |   0.221 μs |        - |        - |        - |      40 B |
| **Create**    | **SparseEdits** | **Fossil** |    **278.10 μs** |   **2.412 μs** |   **1.744 μs** |        **-** |        **-** |        **-** |    **4304 B** |
| Apply     | SparseEdits | Fossil |    149.45 μs |  10.317 μs |   7.460 μs | 134.2905 | 134.2905 | 134.2905 | 1049816 B |
| ApplyInto | SparseEdits | Fossil |     96.14 μs |   0.924 μs |   0.611 μs |        - |        - |        - |      40 B |
| **Create**    | **Unrelated**   | **ASCII**  | **16,057.16 μs** | **474.365 μs** | **370.353 μs** |  **93.7500** |  **93.7500** |  **93.7500** | **1053397 B** |
| Apply     | Unrelated   | ASCII  |    128.98 μs |   1.270 μs |   0.918 μs | 113.1657 | 113.1657 | 113.1657 | 1049621 B |
| ApplyInto | Unrelated   | ASCII  |     89.50 μs |   1.722 μs |   1.344 μs |        - |        - |        - |      40 B |
| **Create**    | **Unrelated**   | **Binary** | **15,593.28 μs** |  **71.958 μs** |  **56.180 μs** |  **93.7500** |  **93.7500** |  **93.7500** | **1053381 B** |
| Apply     | Unrelated   | Binary |    128.65 μs |   2.885 μs |   2.252 μs | 116.0201 | 116.0201 | 116.0201 | 1049647 B |
| ApplyInto | Unrelated   | Binary |     90.30 μs |   1.102 μs |   0.860 μs |        - |        - |        - |      40 B |
| **Create**    | **Unrelated**   | **Fossil** | **15,862.09 μs** |  **79.800 μs** |  **57.701 μs** |  **93.7500** |  **93.7500** |  **93.7500** | **1053381 B** |
| Apply     | Unrelated   | Fossil |    129.99 μs |   4.170 μs |   3.256 μs | 114.1467 | 114.1467 | 114.1467 | 1049630 B |
| ApplyInto | Unrelated   | Fossil |     88.75 μs |   1.559 μs |   1.217 μs |        - |        - |        - |      40 B |

## Reproduce

Run from the repository root with the .NET 11 SDK. [Benchmark source](../Benchmarks/PatchOperations.cs).

```sh
dotnet run -c Release --project src/Benchmarks/SharpFossilBenchmarks.csproj -- --filter '*PatchOperations*' --launchCount 1 --warmupCount 6 --iterationCount 12 --iterationTime 350
```
