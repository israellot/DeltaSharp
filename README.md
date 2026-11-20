# DeltaSharp

[![CI](https://github.com/israellot/DeltaSharp/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/israellot/DeltaSharp/actions/workflows/ci.yml?query=branch%3Amain+event%3Apush)
[![NuGet version](https://img.shields.io/nuget/v/DeltaSharp)](https://www.nuget.org/packages/DeltaSharp/)
[![Line coverage](https://israellot.github.io/DeltaSharp/badge_shieldsio_linecoverage_blue.svg)](https://israellot.github.io/DeltaSharp/)

DeltaSharp is a binary diff and patch library for .NET. Create a patch from two versions of a file and send it to anyone who already has the original. Applying the patch reconstructs the updated file byte for byte. DeltaSharp supports native Binary patches, Fossil-compatible deltas, and an ASCII format with readable commands.

## Install and apply a patch

DeltaSharp 1.1.0 includes assemblies for .NET 8, 9, 10, 11, and .NET Standard 2.0. To try it in a .NET 11 console project, install [DeltaSharp from NuGet](https://www.nuget.org/packages/DeltaSharp/).

```sh
dotnet package add DeltaSharp
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

See the [Moby-Dick example](#example-patch-two-copies-of-moby-dick) for patch size and the [benchmarks](#performance) for timings and allocations.

## Performance

On .NET 11 running on an Apple M3 Max, DeltaSharp created a **46,294-byte Binary patch** between the two bundled editions of *Moby-Dick* in **0.888 ms**. The patch was **3.69%** of the 1,253,974-byte target. Reconstructing the book took **0.250 ms** with a new output array or **0.177 ms** when reusing a destination.

| Input | Format | Patch bytes | Create (ms) | Apply (ms) | Apply into reused buffer (ms) |
| --- | --- | ---: | ---: | ---: | ---: |
| *Moby-Dick* editions | Binary | 46,294 | 0.888 | 0.250 | 0.177 |
| *Moby-Dick* editions | Fossil | 50,532 | 0.764 | 0.222 | 0.162 |
| *Moby-Dick* editions | ASCII | 83,692 | 0.987 | 0.243 | 0.183 |
| 1 MiB, one byte changed every 4 KiB | Binary | 2,808 | 0.278 | 0.145 | 0.100 |
| 1 MiB, unrelated source and target | Binary | 1,048,592 | 15.593 | 0.129 | 0.090 |

With 256 scattered edits, the Binary patch was 0.27% of the target. With unrelated bytes, the patch was 16 bytes larger than the target.

Mean times measured November 20, 2026 with BenchmarkDotNet 0.16.0-preview.2 on macOS 27.0.1, .NET 11.0.0 Arm64, and SDK 11.0.100. Create uses exhaustive search. Both apply timings include checksum verification and use a prebuilt patch. Input preparation and file I/O take place outside timing. [Full results, including allocations and uncertainty](src/Benchmarks/Results/PatchOperations-Integrated-2026-11-20.md).

With the .NET 11 SDK installed, run this command from the repository root to reproduce the measurements.

```sh
dotnet run -c Release --project src/Benchmarks/SharpFossilBenchmarks.csproj -- --filter '*PatchOperations*' --launchCount 1 --warmupCount 6 --iterationCount 12 --iterationTime 350
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

The built-in writers start with at most 4 KiB of patch storage and grow as needed. When a large literal determines the next buffer size, they reserve room for the format's checksum. This avoids a second full-buffer allocation just to finish the patch. Returned patches own their backing storage and can be kept across subsequent calls.

For untrusted patches, pass an application-specific output-size limit in bytes:

```csharp
var restored = Delta.ApplyBinary(source, patch.Data.Span,
    maxOutputLength: 64 * 1024 * 1024); // Example budget: 64 MiB.
```

`ApplyFossil` and `ApplyASCII` accept the same parameter. With `using DeltaSharp;`, it is also available as `codec.Apply.Apply(source, patch.Data.Span, maxOutputLength: limit)` on an `IDelta` instance. The declared size is checked before output allocation; exceeding the limit throws `DeltaApplyException` with `Error == OutputLengthLimitExceeded`. Zero permits only empty output, and negative limits throw `ArgumentOutOfRangeException`. Existing overloads retain their behavior. The limit applies to each call's output buffer, not total process memory or concurrent calls. Output-length and checksum validation still run for patches within the limit.

`GetOutputLength()` only reads the declared size; it does not validate the rest of the patch. The overload accepting `Memory<byte>` writes only the declared output range and returns a view of that memory. Overlapping source or patch data is copied internally before writing; failed applications may leave partially written output. Applying stops at the first valid checksum, so trailing bytes are not validated.

## Reuse the output buffer

When applying several patches, allocate one destination large enough for the largest target you accept. Starting with DeltaSharp 1.1.0, `ApplyBinaryInto`, `ApplyFossilInto`, and `ApplyASCIIInto` write into that destination and return the written prefix with its verified checksum. This example writes successive target files to a stream. Each patch uses the same original source.

```csharp
using Delta = DeltaSharp.DeltaSharp;

static void WriteTargets(byte[] source, IEnumerable<ReadOnlyMemory<byte>> patches,
    Stream output, byte[] destination)
{
    foreach (var patch in patches)
    {
        var restored = Delta.ApplyBinaryInto(source, patch.Span, destination);
        output.Write(restored.Data.Span);
    }
}
```

Consume each result before reusing the destination. Copy `restored.Data` if you need to keep an independent result. Use separate buffers for simultaneous calls. An undersized destination throws `DeltaApplyException` with `OutputMemorySizeLessThanRequired` before writing. A destination that overlaps the source or patch can require an internal copy. In NuGet 1.0.10, the same operation is available as `Delta.Binary.Apply.Apply(source, patch.Span, destination)`.

## Choosing search effort

Existing calls use `DeltaSearchMode.Exhaustive`, which searches the full source
candidate chain unless it finds a match for all remaining target bytes. For a
limit of 256 candidates per target position, select `DeltaSearchMode.Bounded`:

```csharp
var patch = Delta.CreateBinary(source, target, DeltaSharp.DeltaSearchMode.Bounded);
```

The same overload is available on `CreateASCII` and `CreateFossil`. Both modes
reconstruct the target exactly. Bounded mode can avoid excessive searching on
repetitive data, but may produce much larger patches and is not faster for every
input. Exhaustive mode preserves the existing search behavior; it does not
guarantee the globally smallest patch. The limit bounds candidate visits, not
elapsed time.

To reuse a codec with bounded search, construct `new DeltaBinary(DeltaSearchMode.Bounded)`
(or `DeltaASCII` / `DeltaFossil`) and call its `Create.Create(source, target)` method.
The existing parameterless constructors remain exhaustive. Custom generic
creators support `new DeltaCreate<TWriter, TChecksum>(checksum, searchMode)` and
a per-call `Create(source, target, searchMode)` override. Modes are not global
settings; selecting one for a call does not change another call's mode.

On .NET 9–11, the encoder chooses the hash-table index width once per patch and runs a generic search loop specialized for that table. .NET 8 and .NET Standard 2.0 use the same search loop with the portable table wrapper. All targets preserve candidate order and search-mode limits.

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

## Benchmark tools and experiments

For measurements of patch writer capacity, forward matching, hash-table dispatch, and caller-owned output buffers, see the [patch performance investigation](src/Benchmarks/Results/PatchPerformance-2026-11-20.md).

<details>
<summary>Hash tables and search modes</summary>

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

Use `--filter '*DeltaSearch*'` to compare full patch creation in both search
modes. These benchmarks include repetitive changes and a useful match beyond
candidate 256, demonstrating both the speed benefit and the larger-patch case.

</details>

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

After merging a version bump to `main`, tag the release commit with `v` followed by the project version (for example, `v1.1.0`), then push that tag. The **Publish DeltaSharp** workflow checks the version, runs the tests, builds the package, and publishes it through the `nuget` GitHub environment. NuGet grants the workflow a short-lived credential scoped to new versions of DeltaSharp.

To check the build and NuGet authentication without publishing, select **Actions → Publish DeltaSharp → Run workflow** on `main`.
