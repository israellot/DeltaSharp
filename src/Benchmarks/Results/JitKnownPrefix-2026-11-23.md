# Specializing CommonPrefixLength for JIT-known byte lengths

**The JIT can select the four- and eight-byte fast paths at compilation time.** With explicit fixed slices on both sides, optimized Arm64 callers use two loads, XOR, bit reversal, a leading-zero count, and a shift. The corresponding x64 instructions are a word load, XOR, TZCNT and a shift. At the inspected dynamic sites the JIT emitted the ordinary comparison, with no additional tests selecting four or eight bytes. A constant eight-byte left slice and a shorter runtime-sized right input correctly use the shorter comparison length.

The best first proposal is the **4/8-byte version**. It has large gains when mismatch positions vary, a measurable benefit to an existing caller, and a smaller change than specializing 16 bytes too. It does not eliminate the underlying immediate-mismatch tradeoff on every CPU. The 16-byte candidate was faster on the M3 Max and the tested Intel CPU for varied mismatches, but the EPYC 7763's fixed-16 equal-input case was 13.6% slower. Stock can return directly when the SIMD equality mask is all ones. The candidate always computes the mismatch index from that mask.

| 4/8 candidate, change in mean time versus stock | Apple M3 Max | AMD EPYC 7763 | Intel Xeon 6975P-C |
| --- | ---: | ---: | ---: |
| Fixed four, varied mismatch position | −50.9% | −29.4% | −37.2% |
| Fixed eight, varied mismatch position | −58.0% | −44.4% | −51.2% |
| Fixed eight, first byte differs | −3.3% | **+12.3%** | −3.6% |
| Fixed eight, last byte differs | −60.2% | −46.4% | −48.5% |
| Synthetic front coding, adjacent eight-byte keys | −47.6% | −60.8% | −49.7% |
| DER validation, runtime-sized seven-byte values | −1.3% | −0.1% | +0.3% |

In the extracted Cohesion page diagnostic, when the last header byte differs, the 4/8 candidate cut **2.229 to 1.069 ns on M3 Max** and **3.783 to 1.859 ns on EPYC 9V74**. The repeated Arm64 comparison gave 2.260 vs 1.063 ns. When the first header byte differed, the Arm64 time fell 1.5% and the AMD time rose 3.6% (1.816 to 1.881 ns). If the header matched and almost the whole page had to be scanned, the Arm64 +2.4% result changed to −0.2% on repeat. Whole-page WAL-sizing means changed by only a few percent.

The fully dynamic Arm64 mixed-length control measured +5.5% for 4/8 in the main run, then −2.2% on repeat. On Intel, the standalone sampled dynamic caller had identical normalized instructions for all three builds, but the primary dynamic mixed-length result was +13.8% for 4/8 and −0.9% for 4/8/16. On AMD the original caller had different register allocation and was 12 code bytes larger than either candidate. The observed size differences do not establish a timing mechanism. Use the independent placebo runs below when judging dynamic performance.


On two further Xeon 6975P-C VMs, the 4/8 candidate's fully dynamic mixed-length result was **+9.6% and −2.9%** against stock. Running byte-identical stock as a separate layout gave +5.3% and +2.0% on that workload. The immediate-mismatch dynamic results were −11.4% and +11.8% for the candidate, tracking the identical stock copies at −11.8% and +11.5%. The emitted-code evidence supports compile-time selection. The timing evidence does not justify promising zero regression on all variable inputs. An upstream proposal should include the +13.8% primary result along with the repeats and the AMD known-eight first-mismatch cost (+0.238 ns).

[Measurements with per-launch medians](JitKnownPrefix-2026-11-23-data.json) · [Primary AMD/Intel benchmarks and caller disassembly](https://github.com/israellot/DeltaSharp/actions/runs/45195614079) · [Real caller on AMD](https://github.com/israellot/DeltaSharp/actions/runs/45196313970) · [Independent placebo runs](https://github.com/israellot/DeltaSharp/actions/runs/45199312544).

## Implementation and scope

The change is in CoreLib. The private byte helper becomes a small inlinable wrapper around the original implementation. It asks `RuntimeHelpers.IsKnownConstant(length)` before selecting a fixed-size kernel. Here `length` is the smaller span length multiplied by the size of an element. The existing public generic method still chooses whether a bitwise comparison is valid and converts a byte offset back into an element count. This also allows two `uint` elements to use the eight-byte path and keeps floating-point equality semantics in the original generic path. The ordinary comparer overloads retain their existing dispatch.

For 4 and 8 bytes, two unaligned loads and an XOR identify the differing bits. Counting trailing zeros (leading zeros on big-endian CPUs) and dividing by eight yields the prefix length. An all-zero word returns the full width. The optional 16-byte case uses SIMD equality when Vector128 is accelerated. Bit 16 of the inverted equality mask makes a full match return 16. The scalar-only 16-byte case uses the stock fallback. The JIT evaluates the intrinsic while compiling. Specialization depends on what it can prove after inlining. A recurring input length alone does not establish that proof. [Runtime intrinsic](https://github.com/dotnet/runtime/blob/3d10ab44c1e907e5654f9100442112c41be4cd54/src/libraries/System.Private.CoreLib/src/System/Runtime/CompilerServices/RuntimeHelpers.cs#L178) · [JIT constant folding](https://github.com/dotnet/runtime/blob/3d10ab44c1e907e5654f9100442112c41be4cd54/src/coreclr/jit/morph.cpp#L8304).

## Measurement

Three Release CoreRun layouts use runtime commit `3d10ab44c1e907e5654f9100442112c41be4cd54`, the same .NET 12.0.0-dev VM/JIT within each machine, and the supporting assemblies from its bootstrap SDK. Only ReadyToRun System.Private.CoreLib.dll differs between variants. Both source patches and the focused test patch also apply to `2ba107f830a3c5bfefe42ae5cf1976504a50efb9`. The benchmark targets net11.0 so it can use the public API with each private runtime. BenchmarkDotNet 0.16.0-preview.2 used two launches, five warmups and nine measured iterations at a 100 ms iteration target. The public API benchmarks on x64 ran on two separately scheduled Ubuntu 24.04 VMs, pinned to one CPU. The assignment of variants to the first and last toolchain layouts was reversed on the second VM.

Fixed tests slice inside the non-inlined consumer, so the JIT sees the bounds. The dynamic tests carry lengths in data or accept spans of unknown length. `Value4`, `Value8` and `Value16` have constant runtime values but no known length at the call site. One-known cases have a fixed eight-byte left input and a variable right span which can be shorter. `FrontCode8` sums shared prefixes between adjacent sorted eight-byte keys and is a synthetic scenario, measured per key pair. `DerShared7` validates a whole 64-element set of seven-byte TLVs using the real framework decoder; decoded lengths are dynamic.

Tested equal spans, every mismatch position, low/high mismatch bits, unaligned offsets, one-sided dynamic spans shorter than the fixed side, unsupported constant lengths, multibyte elements, and floating-point signed zero and NaN. Each core run passed 1,923,589 harness assertions with intrinsics on and again with them off. Big-endian correctness follows from the indexing and bit-count operation and was not tested on big-endian hardware. Code-generation inspections use optimized Tier-1 caller listings. A fallback to the ordinary helper remains possible in tiers and callers that fail to expose a compile-time constant. Startup, NativeAOT, and cold-call performance were not benchmarked.

## An existing fixed-length caller

Cohesion's page diagnostic compares the first eight bytes of two 8,192-byte pages, followed by a variable-length body when the headers match. Its WAL-sizing method compares fixed 8- and 12-byte slices via a local function and also scans the variable body. The scenario driver extracts the actual methods and constants from pinned MIT-licensed source. It supplies synthetic pages, including equality, mismatches at header offsets 0 and 7, and a last-payload-byte mismatch. The figures are per method invocation. They do not measure overall database throughput. On Arm64 both the direct call and the eight-byte call through the inlined local function used the specialized code. [Page diagnostic](https://github.com/assimalign/cohesion/blob/9b3431a7d6cb6754b9bd0401bc2c9b024445e0c9/resources/Database/Assimalign.Cohesion.Database.Storage/src/Internal/Pages/PageImageCodec.cs#L245-L254) · [WAL size calculation](https://github.com/assimalign/cohesion/blob/9b3431a7d6cb6754b9bd0401bc2c9b024445e0c9/resources/Database/Assimalign.Cohesion.Database.Sql/src/Internal/Provisioning/SqlSchemaProvisioner.JointSpace.cs#L559-L574) · [x64 caller benchmark and artifacts](https://github.com/israellot/DeltaSharp/actions/runs/45196313970).

## Reproduce

The experiment includes two independently applicable source patches and a separate focused test patch. Use a stock Release CoreRun layout and one layout for each source patch. Build from the pinned commit and apply a patch with `git apply` from the runtime checkout. The driver uses the public API for both timing and code inspection. [4/8-byte patch](../../Experiments/JitKnownPrefix/known48.patch) · [4/8/16-byte patch](../../Experiments/JitKnownPrefix/known4816.patch) · [Focused tests](../../Experiments/JitKnownPrefix/known-length-tests.patch) · [x64 layout build script](https://github.com/israellot/DeltaSharp/blob/9a691f6bbc58b20deb471662a9ba003810970c1e/src/Experiments/JitKnownPrefix/build-x64.sh).

From `src/Experiments/JitKnownPrefix`, set `stock`, `known48` and `known4816` to those layout directories.

```sh
dotnet build -c Release
for layout in "$stock" "$known48" "$known4816"; do
  "$layout/corerun" bin/Release/net11.0/PrefixKnown.dll --verify
  DOTNET_EnableHWIntrinsic=0 "$layout/corerun" bin/Release/net11.0/PrefixKnown.dll --verify
  DOTNET_JitDisasm='PrefixSites:* FixedCaller:FrontCode8' \
    DOTNET_JitStdOutFile="$layout/prefix-callers.asm" \
    "$layout/corerun" bin/Release/net11.0/PrefixKnown.dll --jit
done
dotnet run -c Release --no-build -- --filter '*' \
  --coreRun "$stock/corerun" "$known48/corerun" "$known4816/corerun" \
  --launchCount 2 --warmupCount 5 --iterationCount 9 --iterationTime 100 --exporters json
```

The page benchmark lives in `src/Experiments/JitKnownPrefixCaller`. Run `node extract.mjs` there before building and use the same private CoreRun executables. Focused verification on Arm64 included 19 original runtime fixture cases and the two new test cases. [Primary x64 run](https://github.com/israellot/DeltaSharp/actions/runs/45195614079) · [Independent controls with two identical stock layouts](https://github.com/israellot/DeltaSharp/actions/runs/45199312544).

## Generated code at the call site

Tier-1 native code sizes below are bytes for the full caller method, including slice bounds checks. Each cell reads **stock / 4–8 / 4–8–16**.

| Method | M3 Max | EPYC 7763 | Xeon 6975P-C |
| --- | ---: | ---: | ---: |
| Both4 | 188 / 84 / 84 | 100 / 34 / 34 | 100 / 34 / 34 |
| Both8 | 188 / 84 / 84 | 101 / 37 / 37 | 101 / 37 / 37 |
| Both16 | 144 / 144 / 120 | 55 / 55 / 41 | 55 / 55 / 41 |
| FourEight | 188 / 84 / 84 | 100 / 34 / 34 | 100 / 34 / 34 |
| EightFour | 188 / 84 / 84 | 100 / 34 / 34 | 100 / 34 / 34 |
| SixteenEight | 188 / 84 / 84 | 101 / 37 / 37 | 101 / 37 / 37 |
| FixedDynamic | 520 / 520 / 516 | 335 / 327 / 327 | 327 / 327 / 327 |
| DynamicFixed | 520 / 520 / 516 | 332 / 324 / 324 | 324 / 324 / 324 |
| Unknown | 488 / 488 / 484 | 309 / 297 / 297 | 297 / 297 / 297 |
| Both7 | 272 / 272 / 272 | 155 / 155 / 155 | 155 / 155 / 155 |
| FourUShort | 184 / 88 / 88 | 111 / 40 / 40 | 111 / 40 / 40 |
| TwoUInt | 184 / 88 / 88 | 112 / 41 / 41 | 112 / 41 / 41 |
| OneULong | 176 / 80 / 80 | 110 / 39 / 39 | 110 / 39 / 39 |
| FrontCode8 | 280 / 184 / 184 | 194 / 102 / 102 | 194 / 102 / 102 |

The Arm64 4/8-only and stock builds produced the same normalized Tier-1 instructions in the sampled fully dynamic caller. The 4/8/16 version changed its code size slightly. A separate Arm64 probe with an eight-byte slice on one side and only a preceding length guard on the other did not generate the specialization in the inspected callers. Visibility of the constant through inlining matters. The two bounds checks in `Both4` and `Both8` protect the user’s explicit slices.

### Arm64 optimized eight-byte example

```asm
ldr     x0, [x0]
ldr     x1, [x2]
eor     x0, x0, x1
rbit    x0, x0
clz     x0, x0
asr     w0, w0, #3
```

### x64 optimized eight-byte example

```asm
mov      rax, qword ptr [rdi]
xor      rax, qword ptr [rdx]
tzcnt    rax, rax
sar      eax, 3
```

## Comparative timings

Each cell gives the percentage change in mean time for **4/8 only / 4/8/16**, measured against stock in the same batch. Negative is faster. The accompanying JSON contains absolute times, 99.9% error margins, and individual process-launch medians.

| Case | M3 Max | EPYC 7763 | Xeon 6975P-C |
| --- | ---: | ---: | ---: |
| Dynamic/BothUnknown/First0to32 | +0.5% / -0.9% | +7.8% / -4.8% | +6.7% / +10.0% |
| Dynamic/OneKnown/First0to32 | -0.3% / -0.0% | +6.6% / +2.7% | -3.3% / +0.4% |
| Dynamic/BothUnknown/Mix0to32 | +5.5% / +1.0% | +5.0% / -0.7% | +13.8% / -0.9% |
| Dynamic/OneKnown/Mix0to32 | +0.8% / +0.1% | -0.4% / -7.3% | -0.3% / -2.9% |
| Dynamic/BothUnknown/Value16 | -1.5% / -0.3% | +3.1% / +2.4% | +0.6% / +0.2% |
| Dynamic/OneKnown/Value16 | -0.0% / +2.3% | +5.4% / +10.9% | -3.1% / -0.6% |
| Dynamic/BothUnknown/Value4 | +1.1% / +0.6% | +10.6% / +9.8% | +4.7% / +2.1% |
| Dynamic/OneKnown/Value4 | +0.3% / +0.9% | +4.4% / +3.9% | -2.8% / -0.8% |
| Dynamic/BothUnknown/Value8 | +0.4% / -0.7% | +1.4% / +1.3% | +0.4% / -1.5% |
| Dynamic/OneKnown/Value8 | -1.2% / -0.9% | +9.9% / -0.1% | +3.5% / +0.2% |
| Fixed/Known4/Equal | -33.0% / -32.4% | -43.5% / -33.5% | -28.7% / -19.0% |
| Fixed/Known8/Equal | -51.4% / -51.3% | -44.6% / -46.6% | -49.0% / -51.7% |
| Fixed/Known16/Equal | +0.3% / -2.5% | +0.5% / +13.6% | +0.1% / -7.2% |
| Fixed/Known4and8/Equal | -29.8% / -32.5% | -28.1% / -28.3% | -29.5% / -29.5% |
| Fixed/Known4/First | -4.4% / -4.7% | -2.9% / -2.0% | -5.0% / -5.0% |
| Fixed/Known8/First | -3.3% / -0.6% | +12.3% / +12.5% | -3.6% / +1.7% |
| Fixed/Known16/First | +2.9% / -5.9% | -0.9% / +4.4% | +0.4% / -8.7% |
| Fixed/Known4and8/First | -0.1% / -3.2% | +3.7% / +6.6% | -9.5% / -9.4% |
| Fixed/Known4/Last | -26.5% / -24.6% | -17.2% / -16.6% | -23.7% / -26.8% |
| Fixed/Known8/Last | -60.2% / -60.1% | -46.4% / -47.5% | -48.5% / -49.0% |
| Fixed/Known16/Last | +1.7% / -5.4% | -4.1% / +6.7% | -0.2% / -8.2% |
| Fixed/Known4and8/Last | -28.1% / -27.9% | -18.8% / -20.5% | -26.7% / -26.4% |
| Fixed/Known4/Varied | -50.9% / -51.5% | -29.4% / -29.2% | -37.2% / -37.5% |
| Fixed/Known8/Varied | -58.0% / -58.1% | -44.4% / -44.4% | -51.2% / -51.2% |
| Fixed/Known16/Varied | +5.8% / -11.2% | +1.1% / -2.5% | -0.9% / -6.4% |
| Fixed/Known4and8/Varied | -48.5% / -47.9% | -27.8% / -26.3% | -41.0% / -40.0% |
| FixedCaller/FrontCode8 | -47.6% / -48.1% | -60.8% / -60.7% | -49.7% / -49.8% |
| FixedCaller/DerShared7 | -1.3% / -1.8% | -0.1% / -0.2% | +0.3% / +1.1% |

### Page-method timings

| Case | M3 Max, Cohesion | EPYC 9V74, Cohesion |
| --- | ---: | ---: |
| Pages/FirstDifference/EqualPages | +0.9% / -0.8% | -2.9% / -2.8% |
| Pages/JointRunBytes/EqualPages | -1.9% / -2.2% | +2.1% / -0.3% |
| Pages/FirstDifference/Header0 | -1.5% / -1.6% | +3.6% / +2.8% |
| Pages/JointRunBytes/Header0 | -0.7% / -0.4% | -0.6% / +1.4% |
| Pages/FirstDifference/Header7 | -52.1% / -51.2% | -50.9% / -50.3% |
| Pages/JointRunBytes/Header7 | -0.9% / -2.0% | -1.6% / +0.3% |
| Pages/FirstDifference/LastPayloadByte | +2.4% / +0.3% | -0.5% / +0.2% |
| Pages/JointRunBytes/LastPayloadByte | -0.3% / +2.4% | -2.1% / -3.2% |

### Separate Arm64 repeats

| Case | Arm64 repeats |
| --- | ---: |
| Dynamic/BothUnknown/First0to32 | -0.6% / -0.1% |
| Dynamic/BothUnknown/Mix0to32 | -2.2% / -1.3% |
| Fixed/Known16/Varied | +0.1% / -11.4% |
| FixedCaller/DerShared7 | -0.7% / -0.7% |

| Case | Arm64 page repeats |
| --- | ---: |
| Pages/FirstDifference/Header7 | -53.0% / -51.4% |
| Pages/FirstDifference/LastPayloadByte | -0.2% / -0.6% |

## Dynamic-control repeat with identical stock copies

Each runner had two stock directories containing the same CoreLib as verified by SHA-256, plus the 4/8 and 4/8/16 builds. There were three process launches per case. Compare the candidate and placebo against the same stock reference in each VM. A varying placebo illustrates sensitivity to run conditions; a quiet placebo does not control all code-placement changes introduced by compiling a different assembly.

### placebo1 — Intel(R) Xeon(R) 6975P-C

| Case | Stock mean (ns) | Identical stock | 4/8 | 4/8/16 |
| --- | ---: | ---: | ---: | ---: |
| Dynamic/BothUnknown/First0to32 | 2.376 | -11.8% | -11.4% | -6.2% |
| Dynamic/BothUnknown/Mix0to32 | 2.764 | +5.3% | +9.6% | +9.8% |
| Dynamic/OneKnown/Mix0to32 | 3.131 | -1.5% | -1.9% | +0.5% |
| Fixed/Known8/First | 1.666 | -6.6% | -17.8% | -18.6% |
| Fixed/Known16/Varied | 1.515 | +4.1% | +1.9% | -6.1% |

### placebo2 — Intel(R) Xeon(R) 6975P-C

| Case | Stock mean (ns) | Identical stock | 4/8 | 4/8/16 |
| --- | ---: | ---: | ---: | ---: |
| Dynamic/BothUnknown/First0to32 | 2.017 | +11.5% | +11.8% | +5.8% |
| Dynamic/BothUnknown/Mix0to32 | 2.862 | +2.0% | -2.9% | +1.7% |
| Dynamic/OneKnown/Mix0to32 | 3.155 | +2.8% | -4.5% | +0.1% |
| Fixed/Known8/First | 1.496 | +2.5% | -7.7% | +5.6% |
| Fixed/Known16/Varied | 1.545 | +1.7% | +1.0% | -8.2% |
