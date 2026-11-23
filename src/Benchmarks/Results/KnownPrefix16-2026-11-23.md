# Comparing exactly 16 bytes for a common prefix

Use a 128-bit byte-equality comparison, extract its 16 lane bits, invert the unsigned 32-bit mask, and count its trailing zero bits. This was the fastest implementation for mixed mismatch positions on all three tested machines. If every byte matches, the equality mask is 0x0000FFFF, so the first set bit in the inverted mask is bit 16. The operation therefore returns 16 for equality without a separate branch.

Inputs consisted of 256 cached byte-array pairs. Each comparison used slices of exactly 16 bytes, with the two arrays accessed at offsets one and three. In the mixed case, the first mismatch was drawn uniformly from positions 0–15 plus equality (seed 71237). In the other cases the mismatch position or equality was fixed throughout. Each kernel used the same no-inline call boundary and retained the span-length checks. The scalar XOR checks the first eight bytes and inspects the last eight only if needed. Vector XOR checks the two 64-bit lanes of a 128-bit XOR result. Both have a data-dependent choice of lane.

Native .NET 12.0.0-alpha.1.26557.102 Release on Apple M3 Max (Arm64) and two GitHub-hosted Ubuntu 24.04 x64 VMs. BenchmarkDotNet 0.16.0-preview.2, two launches, five warmups and nine measured iterations per launch with 120 ms target. The table shows nanoseconds per comparison (including iteration over pairs) and BenchmarkDotNet’s half-width of the 99.9% confidence interval. All four implementations passed 34,816 checks per machine covering every mismatch position and every bit, equality, and independent offsets. These are fixed-length microbenchmarks. Integration into CoreLib and performance for dynamic lengths were outside the experiment. [Source](../../Experiments/Exact16/Program.cs) · [Native x64 workflow and artifacts](https://github.com/israellot/DeltaSharp/actions/runs/45192101395).

## Apple M3 Max

| First mismatch | Stock API | Two scalar XORs | SIMD equality mask | Vector XOR and lane scan |
| --- | ---: | ---: | ---: | ---: |
| first | 1.651 ± 0.026 | 1.371 ± 0.016 | 1.485 ± 0.003 | 1.364 ± 0.006 |
| index7 | 1.628 ± 0.016 | 1.372 ± 0.005 | 1.515 ± 0.022 | 1.368 ± 0.009 |
| index8 | 1.626 ± 0.006 | 1.387 ± 0.015 | 1.522 ± 0.008 | 1.374 ± 0.010 |
| last | 1.679 ± 0.018 | 1.396 ± 0.004 | 1.506 ± 0.014 | 1.369 ± 0.011 |
| equal | 1.546 ± 0.010 | 1.395 ± 0.004 | 1.497 ± 0.020 | 1.361 ± 0.005 |
| mixed | 1.857 ± 0.012 | 1.882 ± 0.028 | 1.497 ± 0.011 | 1.894 ± 0.025 |

## Intel Xeon 6973P-C

| First mismatch | Stock API | Two scalar XORs | SIMD equality mask | Vector XOR and lane scan |
| --- | ---: | ---: | ---: | ---: |
| first | 1.624 ± 0.067 | 1.570 ± 0.073 | 1.504 ± 0.023 | 1.683 ± 0.045 |
| index7 | 1.626 ± 0.040 | 1.685 ± 0.046 | 1.529 ± 0.018 | 1.708 ± 0.063 |
| index8 | 1.588 ± 0.023 | 1.652 ± 0.019 | 1.546 ± 0.053 | 1.839 ± 0.057 |
| last | 1.605 ± 0.052 | 1.719 ± 0.086 | 1.494 ± 0.014 | 1.756 ± 0.013 |
| equal | 1.568 ± 0.026 | 1.716 ± 0.030 | 1.532 ± 0.025 | 1.864 ± 0.045 |
| mixed | 1.565 ± 0.052 | 1.724 ± 0.070 | 1.477 ± 0.040 | 1.628 ± 0.012 |

## Intel Xeon 6975P-C

| First mismatch | Stock API | Two scalar XORs | SIMD equality mask | Vector XOR and lane scan |
| --- | ---: | ---: | ---: | ---: |
| first | 1.571 ± 0.018 | 1.556 ± 0.012 | 1.437 ± 0.012 | 1.664 ± 0.013 |
| index7 | 1.573 ± 0.016 | 1.555 ± 0.010 | 1.428 ± 0.010 | 1.660 ± 0.011 |
| index8 | 1.558 ± 0.009 | 1.590 ± 0.010 | 1.443 ± 0.012 | 1.828 ± 0.009 |
| last | 1.562 ± 0.012 | 1.604 ± 0.013 | 1.446 ± 0.028 | 1.824 ± 0.009 |
| equal | 1.578 ± 0.057 | 1.611 ± 0.062 | 1.445 ± 0.013 | 1.825 ± 0.011 |
| mixed | 1.530 ± 0.011 | 1.751 ± 0.008 | 1.447 ± 0.020 | 1.729 ± 0.020 |
