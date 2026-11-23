# CommonPrefixLength guards on Arm64 and x64

**The remainder guard fixes the original four-byte first-mismatch case and preserves most mixed-input gains. No guard tested avoided all material regressions.** I would take the remainder guard and the original XOR patch to maintainers as alternatives in a performance issue. I would hold a PR if its acceptance depends on matching stock in all the measured cases. [Remainder-guard patch against upstream with tests](https://github.com/israellot/DeltaSharp/blob/c04ee4b2cdd2de512c545cfdbde2557c4417c9ce/src/Experiments/UpstreamCommonPrefixLength/remainder-guard.patch).

The remainder guard leaves the original checks for one to three leading bytes in place. When there are zero leading bytes, it returns zero for an empty input or a mismatch in the first byte. Without that guard, the original XOR patch goes straight to a four-byte comparison at lengths 4, 8 and 12. Keeping the original leading-byte path recovered the three-byte performance that the early-empty guard lost on Intel.

| Mean time change versus stock, remainder guard | Intel Xeon 6975P-C | AMD EPYC 9V74 | Apple M3 Max |
| --- | ---: | ---: | ---: |
| 4 bytes, mismatch at byte 0 | −0.5% | −0.9% | −0.3% |
| 3 bytes, mismatch at the last byte | −0.2% | −0.1% | −0.1% |
| Mixed 4–7 bytes, varied mismatch positions | −9.9% | −17.2% | −25.9% |
| Mixed 0–32 bytes, varied mismatch positions | −6.3% | −12.1% | −12.1% |
| Shared-prefix DER set, per 64-value set | −4.8% | −4.8% | −6.0% |
| Mixed DER set | +0.2% | +0.3% | +0.1% |
| 4 bytes, mismatch at the last byte | **+7.6%** | −5.4% | −9.1% |
| 5 bytes, mismatch at byte 1 | +5.4% | **+9.2%** | Outside Arm matrix |
| Mixed 0–32 bytes, immediate mismatch | −3.7% | −2.5% | **+3.5%**, **+9.9%** on repeat |

### Remaining costs

- On Intel the four-byte last mismatch took **1.943 vs 1.806 ns**. The 16-byte last mismatch was also slower (1.602 vs 1.510 ns), despite identical SIMD source. Its magnitude did not replicate on AMD or Arm. The experiment supports reporting the timing difference, without attributing it to specific instructions.
- On AMD the five-byte mismatch at byte one took **1.864 vs 1.707 ns**, with consistent launch medians. Byte zero matches and the differing byte is the first byte of the next four-byte group. The screen also exposed this weakness in the original minimal patch. Intel's +5.4% result here has overlapping error intervals.
- On Arm, the mixed 0–32-byte immediate mismatch went from **1.308 to 1.437 ns** in a fresh reversed-order run, with separated 99.9% intervals. Fixed 4-, 8- and 12-byte immediate mismatches stayed close to stock. The fixed-length results alone would miss the varying-length control.
- Disabling hardware intrinsics produced large improvements in the dedicated software-fallback cases. For 1,024 equal bytes, the remainder guard reduced time by 56% on Intel, 61% on AMD, and 53% on Arm. A separately scoped software-only patch is a possible direction if normal hardware paths must keep their current behavior. Such a patch would need its own benchmark and would give up the normal-path DER and mixed-input gains.

### Why the other guards were less attractive

- Guarding exactly length four left lengths eight and twelve exposed. Taking the first-byte check from the loaded XOR mask often kept the early-mismatch penalty. Checking the first byte before every word load helped several word-boundary cases but cost throughput elsewhere.
- Checking the first byte once for every nonempty input improved immediate mismatches at lengths 4, 8 and 12 on all four x64 VMs in the first two runs. The five-byte mismatch at byte one was slower in all four. It also slowed empty spans on Arm. Returning immediately on empty input repaired that Arm weakness and kept strong mixed gains. However, on two Intel Xeon 6975P-C VMs the three-byte last mismatch then rose by 15.0% and 14.2%, about 0.26 ns in each case, with consistent launch medians.
- Checking only the first iteration *inside* the XOR loop kept the three-byte cost close to stock, but brought back the original four-byte immediate-mismatch penalty: +9.5% on Intel, +8.3% on AMD, and +9.6% on Arm. The source location of the guard mattered in these builds.

Across three GitHub Actions runs, six x64 VMs completed **768** BenchmarkDotNet cases. Eight Arm64 batches completed **344**, including controls with hardware intrinsics disabled and reverse-order repeats. All tested variants passed the focused prefix checks. The expanded first-position and word-boundary workloads in this investigation complement the [earlier x64 results](CommonPrefixLength-x64-2026-11-20.md). [Four-guard screen](https://github.com/israellot/DeltaSharp/actions/runs/45094061934) · [Early-empty follow-up](https://github.com/israellot/DeltaSharp/actions/runs/45095265408) · [Remainder and first-iteration follow-up](https://github.com/israellot/DeltaSharp/actions/runs/45097490918).

## Test design

The comparison uses the scalar byte helper at upstream `3d10ab44c1e907e5654f9100442112c41be4cd54`. The source and focused upstream test file were still identical on November 22 at `bdd9b750`. Each candidate adds the same six boundary-test cases. The production DeltaSharp encoder and the .NET SIMD source are outside these changes.

| Candidate | Change to the original four-byte XOR patch |
| --- | --- |
| Exact four | Return zero when length is four and the first byte differs. |
| General first-byte guard | Check byte zero once for every nonempty scalar input, then handle the remaining one or two leading bytes as before. |
| Word mask | When a word differs, check its first byte in the already loaded XOR result before counting zero bits. |
| Word-first precheck | Compare the first byte of every full word before loading the 32-bit pair. |
| Early empty | Return zero immediately for an empty span or a mismatch at byte zero. This modifies the general guard's control flow. |
| Remainder guard | Preserve the original `length % 4` prefix checks and add an `else if` for empty input or a mismatch at byte zero. Only inputs with a remainder of zero take this new branch. |
| First iteration | Preserve the original prefix checks. Inside the word loop, check byte zero only when the loop index is zero. |

The original code handles the first `length % 4` bytes before its four-byte loop. At lengths 4, 8 and 12, byte zero therefore lands in the word loop. At length 5, byte one lands at the beginning of a word. The expanded matrix times these positions and mismatches at offsets four and eight in longer inputs. The four-byte mixed case uses seeded, uniformly drawn first-mismatch positions including equality. The first-heavy four-byte case draws byte-zero mismatches with 90% probability and last-byte mismatches or equal inputs with 5% probability each. The two ASN.1 sets exercise the actual framework sort validation on generated values.

Each x64 run used two standard Ubuntu 24.04 GitHub VMs. Within each VM the runner built stock and the candidates from one snapshot, verified that the layouts differed only in CoreLib, reversed layout assignments between replicas, and pinned timing to one logical processor. BenchmarkDotNet used two process launches, five warmups and nine measured iterations per launch, with a 100 ms iteration target. Every layout passed 761,472 byte/ushort/uint checks with intrinsics enabled and again with them disabled. Direct timings are per comparison. DER timings are per 64-value set.

The x64 screen used a Xeon Platinum 8573C and an AMD EPYC 7763. The early-empty comparison used two Xeon 6975P-C VMs. Each follow-up used fresh stock controls. Compare each variant against its own batch's stock measurement. The Arm64 measurements use an Apple M3 Max and a 120 ms iteration target. The artifact records the per-case means and BenchmarkDotNet error margins (half of its 99.9% confidence interval). The x64 data also include both launch medians. These independent launches are useful when a VM reports a noisy mean. A small positive mean or overlapping intervals alone cannot establish either a reliable slowdown or equivalence.

## Comparison across variants

Each cell gives the percentage change in mean time on x64 runner 1 / runner 2, in that order. Negative values are faster. The first five rows use the screen. Early-empty and the last two rows use their respective fresh controls. The underlying means, error margins, and CPU models are in the [measurement file](CommonPrefixLength-guards-2026-11-22-data.json).

| Guard | 4-first | 3-last | 5-at1 | 4-last | 12-first | vary4to7 | vary0to32 | DerSet/Shared7 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| minimal | -0.1 / +19.5 | -1.3 / -1.0 | +10.7 / +15.0 | -7.5 / -17.9 | +21.8 / +19.2 | -17.4 / -18.7 | -4.5 / -22.3 | -9.0 / -4.2 |
| exact-four | -16.0 / -1.1 | -3.5 / +3.5 | +15.5 / +23.8 | +0.6 / -5.2 | +26.8 / +27.8 | -7.7 / -13.2 | +0.3 / -14.1 | -4.4 / -4.5 |
| general-first | -15.3 / -4.8 | -6.7 / +0.6 | +7.9 / +16.1 | +3.0 / -3.4 | -3.0 / -5.9 | -17.9 / -19.8 | -5.3 / -21.0 | -8.0 / -8.4 |
| word-mask | +1.1 / +19.0 | -2.5 / -3.6 | +9.9 / +7.0 | -12.9 / -16.0 | +17.8 / +19.0 | -12.4 / -7.7 | -0.1 / -15.5 | -3.1 / -6.8 |
| word-first | -6.8 / -0.1 | -4.2 / -3.6 | +6.9 / -0.8 | -5.4 / -5.3 | +8.6 / +2.4 | -10.7 / -9.2 | -3.3 / -16.0 | -4.6 / -4.2 |
| early-empty | -9.5 / -9.6 | +15.0 / +14.2 | +2.2 / +6.1 | +3.2 / +4.4 | -9.5 / -10.0 | -14.6 / -13.5 | -7.9 / -0.8 | -2.8 / -2.7 |
| remainder-guard | -0.5 / -0.9 | -0.2 / -0.1 | +5.4 / +9.2 | +7.6 / -5.4 | -0.1 / +1.0 | -9.9 / -17.2 | -6.3 / -12.1 | -4.8 / -4.8 |
| first-iteration | +9.5 / +8.3 | -0.6 / +0.4 | +15.6 / +15.9 | +3.0 / -4.9 | +9.9 / +9.8 | -3.5 / -12.6 | -4.6 / -10.9 | -3.4 / -5.4 |

For Arm64, each result is relative to stock in the corresponding batch.

| Guard | 4-first | 3-last | 12-first | vary4to7 | vary0to32 | vary0to32-first | DER Shared7 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| minimal | +0.2 | -0.0 | +0.3 | -24.3 | -9.3 | -1.9 | -5.0 |
| exact-four | -10.1 | +8.7 | +8.8 | -17.3 | -8.6 | +3.6 | -3.6 |
| general-first | -9.9 | +0.1 | -10.9 | -26.2 | -13.2 | -3.1 | -6.4 |
| word-mask | +9.8 | +0.2 | +8.0 | -15.3 | -5.2 | +1.3 | -4.6 |
| word-first | -0.6 | -0.2 | +1.5 | -14.0 | -4.7 | -4.0 | -2.8 |
| early-empty | -10.1 | +1.2 | -9.9 | -25.5 | -12.3 | +0.7 | -6.5 |
| remainder-guard | -0.3 | -0.1 | -0.2 | -25.9 | -12.1 | +3.5 | -6.0 |
| first-iteration | +9.6 | +0.1 | +10.3 | -9.8 | -7.6 | +1.8 | -2.4 |
