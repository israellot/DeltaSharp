# Patch creation and application on .NET 11 and Apple M3 Max

Measured November 20, 2026 using the production encoder at `7acaf87` and its default exhaustive search. BenchmarkDotNet ran one process per case with six warmups and twelve measurement iterations at normal process priority.

These measurements precede the writer allocation and hash-table dispatch changes. The [integrated measurements](PatchOperations-Integrated-2026-11-20.md) also include application into a reusable destination.

## Inputs and patch sizes

All sizes are bytes. MobyDick uses the bundled `pg15.txt` as the source and `pg2701.txt` as the target. SparseEdits flips one byte every 4,096 bytes of a 1 MiB pseudorandom source. Unrelated uses two separately generated 1 MiB buffers. Both synthetic workloads use seed 71237.

| Workload | Source | Target | Binary patch | Fossil patch | ASCII patch |
| --- | ---: | ---: | ---: | ---: | ---: |
| MobyDick | 1,253,951 | 1,253,974 | 46,294 | 50,532 | 83,692 |
| SparseEdits | 1,048,576 | 1,048,576 | 2,808 | 2,762 | 5,122 |
| Unrelated | 1,048,576 | 1,048,576 | 1,048,592 | 1,048,593 | 1,048,609 |

## Timing and allocations

Setup prepares the inputs and verifies byte-for-byte reconstruction. Create measures encoding in memory. Apply uses the patch prepared during setup and includes output allocation and checksum verification. Error is half the 99.9% confidence interval for the mean. Allocated is managed memory allocated per operation.

```

BenchmarkDotNet v0.16.0-preview.2, macOS 27.0.1 (26A434) [Darwin 27.0.0]
Apple M3 Max, 1 CPU, 14 logical and 14 physical cores
Memory: 36 GB Total, 0.69 GB Available
.NET SDK 11.0.100
  [Host]     : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a
  Job-ESCNQV : .NET 11.0.0 (11.0.0, 11.0.26.53018), Arm64 RyuJIT armv8.0-a

Arguments=/p:Enable24BitHashtable=false  IterationCount=12  IterationTime=350ms
LaunchCount=1  WarmupCount=6

```
| Method | Workload    | Format | Mean        | Error     | StdDev    | Gen0     | Gen1     | Gen2     | Allocated  |
|------- |------------ |------- |------------:|----------:|----------:|---------:|---------:|---------:|-----------:|
| **Create** | **MobyDick**    | **ASCII**  |    **957.1 μs** |  **15.64 μs** |  **11.31 μs** | **107.1429** | **107.1429** | **107.1429** |  **612.58 KB** |
| Apply  | MobyDick    | ASCII  |    232.2 μs |   2.86 μs |   2.24 μs | 145.1613 | 145.1613 | 145.1613 | 1225.93 KB |
| **Create** | **MobyDick**    | **Binary** |    **851.9 μs** |  **30.20 μs** |  **23.58 μs** | **110.0000** | **110.0000** | **110.0000** |  **612.58 KB** |
| Apply  | MobyDick    | Binary |    230.9 μs |   3.76 μs |   2.72 μs | 146.2766 | 146.2766 | 146.2766 | 1225.94 KB |
| **Create** | **MobyDick**    | **Fossil** |    **923.5 μs** |  **90.28 μs** |  **70.48 μs** |  **98.5577** |  **98.5577** |  **98.5577** |  **612.59 KB** |
| Apply  | MobyDick    | Fossil |    215.3 μs |  12.52 μs |   9.77 μs | 149.4048 | 149.4048 | 149.4048 | 1225.97 KB |
| **Create** | **SparseEdits** | **ASCII**  |    **319.0 μs** |  **12.06 μs** |   **9.41 μs** | **115.3169** | **115.3169** | **115.3169** |  **513.18 KB** |
| Apply  | SparseEdits | ASCII  |    147.1 μs |   9.23 μs |   6.67 μs | 126.6129 | 126.6129 | 126.6129 | 1025.14 KB |
| **Create** | **SparseEdits** | **Binary** |    **320.4 μs** |   **6.94 μs** |   **5.42 μs** | **114.4231** | **114.4231** | **114.4231** |  **513.17 KB** |
| Apply  | SparseEdits | Binary |    146.2 μs |   9.39 μs |   6.21 μs | 136.3255 | 136.3255 | 136.3255 | 1025.25 KB |
| **Create** | **SparseEdits** | **Fossil** |    **319.1 μs** |   **5.10 μs** |   **3.98 μs** | **115.0362** | **115.0362** | **115.0362** |   **513.2 KB** |
| Apply  | SparseEdits | Fossil |    147.2 μs |   4.68 μs |   3.38 μs | 140.7285 | 140.7285 | 140.7285 | 1025.27 KB |
| **Create** | **Unrelated**   | **ASCII**  | **16,618.3 μs** |  **95.02 μs** |  **62.85 μs** | **437.5000** | **437.5000** | **437.5000** | **3586.17 KB** |
| Apply  | Unrelated   | ASCII  |    134.9 μs |   4.18 μs |   3.26 μs | 117.8892 | 117.8892 | 117.8892 | 1025.07 KB |
| **Create** | **Unrelated**   | **Binary** | **16,709.8 μs** | **227.58 μs** | **164.55 μs** | **437.5000** | **437.5000** | **437.5000** | **3586.09 KB** |
| Apply  | Unrelated   | Binary |    127.1 μs |   1.11 μs |   0.87 μs | 114.2361 | 114.2361 | 114.2361 | 1025.06 KB |
| **Create** | **Unrelated**   | **Fossil** | **17,172.0 μs** | **343.46 μs** | **248.35 μs** | **437.5000** | **437.5000** | **437.5000** | **3586.15 KB** |
| Apply  | Unrelated   | Fossil |    130.8 μs |   4.65 μs |   3.36 μs | 115.6832 | 115.6832 | 115.6832 | 1025.07 KB |

## Reproduce

From the repository root, using the .NET 11 SDK, run the following command. The setup output includes input and patch lengths for each case. [Benchmark source](../Benchmarks/PatchOperations.cs).

```sh
dotnet run -c Release --project src/Benchmarks/SharpFossilBenchmarks.csproj -- --filter '*PatchOperations*' --launchCount 1 --warmupCount 6 --iterationCount 12 --iterationTime 350
```
