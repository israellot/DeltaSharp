# DeltaSharp

DeltaSharp is a C# library for binary diff and patch. When two machines already have the same base file, the sender can transmit a patch and the receiver can use it to reconstruct the updated file byte for byte.

## Install and apply a patch

DeltaSharp 1.0.9 includes assemblies for .NET 8, 9, 10, 11, and .NET Standard 2.0. To try it in a .NET 8 or later console project, install [DeltaSharp from NuGet](https://www.nuget.org/packages/DeltaSharp/):

```sh
dotnet add package DeltaSharp
```

Replace `Program.cs` with this example and run `dotnet run`:

```csharp
using System;
using Delta = DeltaSharp.DeltaSharp;

var source = "The quick brown fox jumps over the lazy dog"u8;
var target = "The lazy brown fox jumps over the quick dog"u8;

var patch = Delta.CreateBinary(source, target);
var restored = Delta.ApplyBinary(source, patch.Data.Span);

Console.WriteLine(restored.Data.Span.SequenceEqual(target)); // True
```

## Choosing a patch format

| Format | When to use it | Create / apply |
| --- | --- | --- |
| Binary | Both endpoints use DeltaSharp. Commands use single-byte identifiers and variable-length integers. | `CreateBinary` / `ApplyBinary` |
| Fossil | Another tool reads or writes the Fossil delta format. | `CreateFossil` / `ApplyFossil` |
| ASCII | You want to inspect copy offsets, insert lengths, and checksums while debugging. Inserted bytes are stored literally. | `CreateASCII` / `ApplyASCII` |

Apply a patch with the matching format and the original source bytes. Each format describes how to copy byte ranges from the source and insert new bytes. DeltaSharp checks the reconstructed output against the checksum in the patch.

Fossil zero-length COPY commands are accepted as no-ops, matching the reference decoder. They do not copy through the end of the source. Source offsets, total output length, and checksums are still validated.

`DeltaResult.Crc32` is the historical name for the Fossil additive checksum used by all three formats; it is not a CRC-32 or a cryptographic integrity check. Authenticate patches separately when their origin is untrusted.

The API works in memory. Allow room for the source, target, and working buffers. The apply API limits the reconstructed output to `int.MaxValue` bytes, and available memory can impose a lower limit. Patch size depends on shared byte sequences. For unrelated inputs, a patch can exceed the size of the target.

For untrusted patches, inspect `Delta.Binary.Apply.GetOutputLength(patch.Data.Span)` (or the matching format) and enforce an application-specific size limit before calling the allocating apply overload. The header declares a size; reading it does not validate the rest of the patch. The overload accepting `Memory<byte>` writes only the declared output range and returns a view of that memory. Overlapping source or patch data is copied internally before writing; failed applications may leave partially written output. Applying stops at the first valid checksum, so trailing bytes are not validated.

## Example: patch two copies of *Moby-Dick*

The repository includes two Project Gutenberg texts in [the benchmark data directory](https://github.com/israellot/DeltaSharp/tree/main/src/Benchmarks/Data). To try them, use the following `Program.cs` in the same console project. Pass the data directory as the argument to `dotnet run -- /path/to/DeltaSharp/src/Benchmarks/Data`.

```csharp
using System;
using System.IO;
using Delta = DeltaSharp.DeltaSharp;

var source = File.ReadAllBytes(Path.Combine(args[0], "pg15.txt"));
var target = File.ReadAllBytes(Path.Combine(args[0], "pg2701.txt"));
var patch = Delta.CreateBinary(source, target);
var restored = Delta.ApplyBinary(source, patch.Data.Span);

Console.WriteLine($"Target: {target.Length} bytes; patch: {patch.Data.Length} bytes");
Console.WriteLine($"Restored byte for byte: {restored.Data.Span.SequenceEqual(target)}");
```

With the files in this repository, the 1,253,974-byte target produces a 46,294-byte Binary patch (about 3.7% of the target size).

## Performance

The round-trip benchmark creates and applies a patch after swapping two groups of four lines in generated text. The historical measurements below used BenchmarkDotNet 0.14.0 on Windows 11, an Intel Core i7-10875H, and .NET 8.0.8 with AVX2. The timings cover both operations together. To measure your machine, run `dotnet run -c Release --project src/Benchmarks/SharpFossilBenchmarks.csproj` from the repository root.

| Method | Bytes   | Mean       | Error     | StdDev    | StdErr    | Op/s      | Ratio | RatioSD | Gen0     | Code Size | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------- |-------- |-----------:|----------:|----------:|----------:|----------:|------:|--------:|---------:|----------:|---------:|---------:|-----------:|------------:|
| Fossil | 1024    |   1.201 us | 0.0220 us | 0.0195 us | 0.0052 us | 832,420.7 |  1.00 |    0.02 |   0.3166 |     759 B |   0.0019 |        - |    2.59 KB |        1.00 |
| Binary | 1024    |   1.111 us | 0.0114 us | 0.0095 us | 0.0026 us | 899,922.4 |  0.93 |    0.02 |   0.3147 |     759 B |        - |        - |    2.59 KB |        1.00 |
| Fossil | 8192    |   4.857 us | 0.0944 us | 0.1927 us | 0.0270 us | 205,902.4 |  1.00 |    0.05 |   2.0294 |     784 B |   0.0610 |        - |   16.59 KB |        1.00 |
| Binary | 8192    |   4.764 us | 0.0766 us | 0.0679 us | 0.0182 us | 209,908.9 |  0.98 |    0.04 |   2.0294 |     784 B |   0.0534 |        - |   16.59 KB |        1.00 |
| Fossil | 32768   |  16.785 us | 0.2164 us | 0.1918 us | 0.0513 us |  59,577.9 |  1.00 |    0.02 |   7.9041 |     784 B |   1.0986 |        - |    64.6 KB |        1.00 |
| Binary | 32768   |  16.872 us | 0.3259 us | 0.2889 us | 0.0772 us |  59,269.2 |  1.01 |    0.02 |   7.9041 |     784 B |   1.0986 |        - |   64.59 KB |        1.00 |
| Fossil | 131072  | 107.346 us | 1.4605 us | 1.2947 us | 0.3460 us |   9,315.7 |  1.00 |    0.02 |  41.6260 |     784 B |  41.6260 |  41.6260 |  192.66 KB |        1.00 |
| Binary | 131072  | 106.508 us | 1.5649 us | 1.4638 us | 0.3780 us |   9,389.0 |  0.99 |    0.02 |  41.6260 |     784 B |  41.6260 |  41.6260 |  192.65 KB |        1.00 |
| Fossil | 1048576 | 752.158 us | 7.3950 us | 6.5555 us | 1.7520 us |   1,329.5 |  1.00 |    0.01 | 332.0313 |     784 B | 332.0313 | 332.0313 | 1537.01 KB |        1.00 |
| Binary | 1048576 | 756.173 us | 9.7535 us | 8.6463 us | 2.3108 us |   1,322.4 |  1.01 |    0.01 | 332.0313 |     784 B | 332.0313 | 332.0313 |    1537 KB |        1.00 |

To run the hash-table comparisons, use the benchmark filter:

```sh
dotnet run -c Release --project src/Benchmarks/SharpFossilBenchmarks.csproj -- --filter '*Hashtable*'
```

Add `-p:Enable24BitHashtable=true` before `--` to include 24-bit comparisons.
The runner forwards this flag to BenchmarkDotNet's generated build. Use
`--list flat` after `--` to list benchmarks, or add `--job Dry` to the filter
command for a smoke run rather than a performance measurement.

Hash-table benchmarks measure construction, one lookup, and disposal with
warmed pools. Small-input cases compare all enabled index widths using the
same inputs from 512 through 2,047 bytes, including index-selection boundaries;
larger cases compare 32-bit and optional 24-bit storage from 512 KiB to 64 MiB.
The `Allocated` column measures
new allocations per operation, not memory retained in the pools. These cases
do not measure sustained lookup throughput or collision-heavy searches.

## Build and test

Use the .NET 11 SDK to build all five NuGet assemblies from the repository root:

```sh
dotnet build src/DeltaSharp/DeltaSharp.csproj -c Release
```

The package is written to `src/DeltaSharp/bin/Release/`. The `net8.0`, `net9.0`, `net10.0`, and `net11.0` assemblies use hardware-accelerated byte matching when available. The `netstandard2.0` assembly uses portable byte matching and depends on `System.Memory`. All five read and write the same patch formats.

With the .NET 8 through 11 runtimes installed, run the suite on each runtime, then exercise the .NET Standard assembly on the .NET 8 test host:

```sh
dotnet test src/UnitTests/SharpFossilUnitTests.csproj -c Release
dotnet test src/UnitTests/SharpFossilUnitTests.csproj -c Release -f net8.0 -p:TestNetStandard=true
```

The 24-bit hash table is excluded by default; inputs above the 16-bit table's
limit use the 32-bit table. To include the 24-bit implementation and restore its
size-based selection, build with:

```sh
dotnet build src/DeltaSharp/DeltaSharp.csproj -c Release -p:Enable24BitHashtable=true
```

`Enable24BitHashtable` defines the `DELTASHARP_HASHTABLE24` compile symbol across
the library, tests, and benchmarks. Pass the same property to `dotnet test` or
benchmark builds to include their 24-bit cases. This is a build-time option;
it does not change an already-built NuGet assembly.

## Publishing to NuGet

After merging a version bump to `main`, tag the release commit with `v` followed by the project version (for example, `v1.0.10`), then push that tag. The **Publish DeltaSharp** workflow checks the version, runs the tests, builds the package, and publishes it through the `nuget` GitHub environment. NuGet grants the workflow a short-lived credential scoped to new versions of DeltaSharp.

To check the build and NuGet authentication without publishing, select **Actions → Publish DeltaSharp → Run workflow** on `main`.
