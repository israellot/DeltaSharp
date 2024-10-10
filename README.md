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

## Limitations

The current implementation is a memory-only one. This means the max input it can process in a single shot is limited by the CLR 2GB object size.
