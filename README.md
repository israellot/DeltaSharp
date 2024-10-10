# DeltaSharp
C# binary Delta Encode library
Optional compatibility with Fossil delta format

## Delta Encoding

Delta encoding is a technique used to efficiently represent data by storing only the differences (deltas) between a source (or base) version and a target version, rather than the entire target data. This method generates deltas by comparing two data sets—typically a reference file and a modified version. Instead of transmitting or saving the entire new file, delta encoding captures only the changes, significantly reducing the amount of data to be handled.

In this process, the algorithm identifies common sequences between the source and target files and encodes the target file by referencing sections from the source. It also stores new data or small modifications separately. This results in an encoded file that contains instructions on how to recreate the target file from the source using copy and add commands. Delta encoding is widely used in version control systems and data synchronization protocols where files evolve over time, and sending the full version repeatedly would be inefficient.

The main advantage of delta encoding is its ability to work efficiently with large datasets where many elements remain unchanged between versions, leading to substantial bandwidth and storage savings.

## Motivation

This library draws inspiration from the popular Fossil Delta algorigthm, utilized in Fossil, a SCM developed by SQLite team.
(https://sqlite.org/src/file/ext/misc/fossildelta.c)

By leveraging vector processing, modern hashing and specialized hashtable implementations, it achieves outstanding performance with a balanced memory footprint, making this library suitable for high performance demand scenarios. Some known use cases: 

- Real time data syncronization over network
- Software binary patching
- File version tracking
- Data compression using templates

## Delta format

All supported formats follow the following structure 

```
[Final Length Identifier] [Length Value]
[Command1] [arg1] [arg2] ([arg3])
[Command2] [arg1] [arg2] ([arg3])
...
[End Identifier] [Checksum Value]
```

### ASCII

The most readable format is ASCII, mainly used for debug and demonstrations.
An encoded delta in this format looks like : 

```
l 43               ( indicates final length of 43 bytes )
i 8 The lazy       ( inserts 8 byte literal 'The Lazy' )
c 9 26             ( copy 26 bytes from source starting at position 9 )
i 9 quick dog      ( inserts 9 bytes literal 'quick dog' )
v 50540973         ( ends with checksum 50540973 )
```

### Binary

Binary format is the most space efficient. It uses single byte for commands and encodes numbers using a variable length format.

### Fossil

Fossil format adds compatibility with the popular Fossil delta format.

## Basic Usage

### Create a delta
```csharp
 var source = "The quick brown fox jumps over the lazy dog"u8;
 var target = "The lazy brown fox jumps over the quick dog"u8;

 var delta = DeltaSharp.CreateASCII(source, target);

 Console.Write(Encoding.UTF8.GetString(delta.Data.Span));

// l 43
// i 8 The lazy
// c 9 26
// i 9 quick dog
// v 50540973

```

### Apply a delta
```csharp
var source = "The quick brown fox jumps over the lazy dog"u8;
var target = DeltaSharp.ApplyASCII(source, delta.Data.Span);
Console.Write(Encoding.UTF8.GetString(target.Data.Span));

//The lazy brown fox jumps over the quick dog
```
### Practical example

Let's say you have a version of the famous book Moby Dick, and there's a new revised edition out there.
Instead of storing both, you can keep the first one and store a delta that you can apply to get the newer edition.

```csharp
var httpClient = new HttpClient();

//moby dick older edition
var source = await httpClient.GetByteArrayAsync("https://www.gutenberg.org/cache/epub/15/pg15.txt");

//moby dick newer edition
var target = await httpClient.GetByteArrayAsync("https://www.gutenberg.org/cache/epub/2701/pg2701.txt");

Console.WriteLine($"Source {(float)source.Length/(1024*1024):f2}MB");
Console.WriteLine($"Target {(float)target.Length/ (1024 * 1024):f2}MB");

var sw = Stopwatch.StartNew();
var delta = DeltaSharp.DeltaSharp.CreateBinary(source, target);
sw.Stop();

Console.WriteLine($"Delta {(float)delta.Data.Length/1024:f2}kb");
Console.WriteLine($"Processed {(float)source.Length / ((1024 * 1024) * sw.Elapsed.TotalSeconds):f2}MB/s");

//------------------------------------------------------------------------------
// Source 1,22 MB
// Target 1,22 MB
// Delta 45,82 kB
// Processed 473,87 MB/s

```

Storing both editions now costs us only 45kB more instead of 1.22MB. 
Context dependent, but it is important to remember further compression can be achieved by compressing the delta itself using zlib or another compression library. 

## Performance

In general, delta encoding performance is very dependent on inputs and much common data they actually share. It is important to benchmark your specific scenario.
Best results are achieved when small edits are done to source data. Worst case is when inputs share no common subsequence at all. 
Bellow are the results for the roundtrip benchmark included in the repository, it gives a general idea of expected performance for various input sizes.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4249/23H2/2023Update/SunValley3)
Intel Core i7-10875H CPU 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 9.0.100-rc.1.24452.12
  [Host]     : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2


| Method | Bytes   | Mean       | Error     | StdDev    | StdErr    | Op/s      | Ratio | RatioSD | Gen0     | Code Size | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|------- |-------- |-----------:|----------:|----------:|----------:|----------:|------:|--------:|---------:|----------:|---------:|---------:|-----------:|------------:|
| Fossil | 1024    |   1.201 us | 0.0220 us | 0.0195 us | 0.0052 us | 832,420.7 |  1.00 |    0.02 |   0.3166 |     759 B |   0.0019 |        - |    2.59 KB |        1.00 |
| Binary | 1024    |   1.111 us | 0.0114 us | 0.0095 us | 0.0026 us | 899,922.4 |  0.93 |    0.02 |   0.3147 |     759 B |        - |        - |    2.59 KB |        1.00 |
|        |         |            |           |           |           |           |       |         |          |           |          |          |            |             |
| Fossil | 8192    |   4.857 us | 0.0944 us | 0.1927 us | 0.0270 us | 205,902.4 |  1.00 |    0.05 |   2.0294 |     784 B |   0.0610 |        - |   16.59 KB |        1.00 |
| Binary | 8192    |   4.764 us | 0.0766 us | 0.0679 us | 0.0182 us | 209,908.9 |  0.98 |    0.04 |   2.0294 |     784 B |   0.0534 |        - |   16.59 KB |        1.00 |
|        |         |            |           |           |           |           |       |         |          |           |          |          |            |             |
| Fossil | 32768   |  16.785 us | 0.2164 us | 0.1918 us | 0.0513 us |  59,577.9 |  1.00 |    0.02 |   7.9041 |     784 B |   1.0986 |        - |    64.6 KB |        1.00 |
| Binary | 32768   |  16.872 us | 0.3259 us | 0.2889 us | 0.0772 us |  59,269.2 |  1.01 |    0.02 |   7.9041 |     784 B |   1.0986 |        - |   64.59 KB |        1.00 |
|        |         |            |           |           |           |           |       |         |          |           |          |          |            |             |
| Fossil | 131072  | 107.346 us | 1.4605 us | 1.2947 us | 0.3460 us |   9,315.7 |  1.00 |    0.02 |  41.6260 |     784 B |  41.6260 |  41.6260 |  192.66 KB |        1.00 |
| Binary | 131072  | 106.508 us | 1.5649 us | 1.4638 us | 0.3780 us |   9,389.0 |  0.99 |    0.02 |  41.6260 |     784 B |  41.6260 |  41.6260 |  192.65 KB |        1.00 |
|        |         |            |           |           |           |           |       |         |          |           |          |          |            |             |
| Fossil | 1048576 | 752.158 us | 7.3950 us | 6.5555 us | 1.7520 us |   1,329.5 |  1.00 |    0.01 | 332.0313 |     784 B | 332.0313 | 332.0313 | 1537.01 KB |        1.00 |
| Binary | 1048576 | 756.173 us | 9.7535 us | 8.6463 us | 2.3108 us |   1,322.4 |  1.01 |    0.01 | 332.0313 |     784 B | 332.0313 | 332.0313 |    1537 KB |        1.00 |
```

## Limitations

The current implementation is a memory-only one. This means the max input it can process in a single shot is restricted by the CLR 2GB object size limit.
