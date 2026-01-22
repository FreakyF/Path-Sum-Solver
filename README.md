# High-Performance SIMD Solver: Minimum Path Sum

## 1. Problem Definition

The objective is to minimize the path sum from the top-left to the bottom-right of an $m \times n$ grid filled with
non-negative integers.

**Constraints:**

* Movement is restricted to **Down** ($r+1$) or **Right** ($c+1$).
* Diagonal, Up, or Left movements are prohibited.

**Example:**
For the following $3 \times 3$ grid:
$$
\mathbf{A} = \begin{bmatrix}
1 & 3 & 1 \\
1 & 5 & 1 \\
4 & 2 & 1
\end{bmatrix}
$$
The optimal path is $1 \to 3 \to 1 \to 1 \to 1$, yielding a minimum sum of **7**.

---

## 2. Motivation

I did this to prove a point, and I made a damn good one.

People act like C# is only for boring CRUD apps.

That's a skill issue. The language isn't the bottleneck. The dev is.

---

## 3. Algorithms

### 3.1 Standard Approach (Reference)

The idiomatic solution uses **Row-Major Dynamic Programming**.
We iterate through the grid row by row, then column by column.
$$
D_{r,c} = A_{r,c} + \min(D_{r-1,c}, D_{r,c-1})
$$

* **Pros:** Simple to implement, cache-friendly for row access.
* **Cons:** **Strict Serial Dependency**. We cannot compute cell $(r,c)$ until its immediate left neighbor $(r, c-1)$ is
  finished. This creates a "Read-After-Write" (RAW) hazard that prevents vectorization within the row. The CPU forces a
  pipeline stall, processing one cell at a time.

### 3.2 Optimized Approach (Wavefront/Diagonal)

To utilize SIMD (Single Instruction, Multiple Data), we reparameterize the iteration space from $(r, c)$ to
diagonals $k = r + c$.

* **The Shift:** We process the grid in "waves" moving from the top-left to bottom-right.
* **Independence:** In diagonal $k$, every cell depends only on cells from diagonal $k-1$.
* **Result:** All cells in the current diagonal are mutually independent. This allows us to compute an entire wavefront
  in parallel using 256-bit wide vector registers.

---

## 4. Applied C# / Low-Level Tricks

- **Native AOT** - Compiles directly to machine code, eliminating JIT compiler overhead and the code warmup process.
- **Core Isolation (Core Pinning)** - Assigns the process exclusively to Core #7 with Real-Time priority to minimize OS
  jitter and context switching.
- **AVX2 Intrinsics** - Processes 32 values (`short`) in a single clock cycle using 256-bit wide hardware registers.
- **Branchless Logic** - Complete removal of `if` statements from the main loop (replaced by data padding), eliminating
  *Branch Misprediction* penalties.
- **Streamed Weights** - Weights are precomputed into a linear buffer, allowing for perfect *Hardware Prefetching* and
  reducing ALU load.
- **NativeMemory & Unsafe** - Bypasses the Garbage Collector and *Bounds Checking* for maximum RAM access speed using
  raw pointers.
- **Overshooting (Padding)** - Extends buffers to always process full 256-bit vectors, eliminating the need for complex
  edge-handling code.
- **Manual Loop Unrolling** - Processes two vector blocks per iteration to saturate the CPU execution pipeline (
  *Instruction Level Parallelism*).
- **SkipLocalsInit** - Prevents the runtime from zero-initializing stack memory upon every function entry, saving clock
  cycles in high-frequency calls.
- **Memory Alignment (64-byte)** - Uses `AlignedAlloc` to ensure data starts on cache-line boundaries, enabling the use
  of `LoadAlignedVector256` for maximum throughput.
- **Double Buffering** - Manages state by alternating between two memory buffers, ensuring the "new" diagonal is
  calculated from the "old" one without race conditions.
- **Vector Conversion (SIMD Promotion)** - Promotes 8-bit weights to 16-bit vectors on-the-fly, optimizing memory
  bandwidth while preventing arithmetic overflow.
- **Native Pointer Arithmetic** - Uses direct pointer offsets instead of array indexing to bypass all high-level runtime
  abstractions and safety checks.
- **Aggressive Optimization Hints** - Uses `AggressiveOptimization` and `AggressiveInlining` attributes to force the
  compiler to apply the most radical optimization passes.
- **ASLR Disabled** - Operates with predictable memory addresses to help the hardware prefetcher and branch target
  buffer (BTB) optimize access patterns.

---

## 5. Technical Deep Dive

### 5.1 Data Dependency Analysis

The fundamental bottleneck in the standard algorithm is the **Loop-Carried Dependency**.
For $D_{r,c} = f(D_{r,c-1})$, the calculation of the current state requires the result of the immediately preceding
instruction. In a superscalar processor, this prevents the Reservation Stations from dispatching multiple instructions
simultaneously.

By rotating the execution domain to diagonals, we isolate the dependency to the *previous* iteration step.
$$
\forall \text{cell } i, j \in \text{Diagonal}_k: \text{Dependency}(i, j) \cap \text{Diagonal}_k = \emptyset
$$
This guarantees that the **Instruction Level Parallelism (ILP)** is limited only by the width of the SIMD registers (
AVX2 YMM) and the throughput of the execution ports, rather than logical constraints.

### 5.2 Memory Hierarchy & Layout

We map the 2D grid logic onto a **1D Double-Buffered Memory Arena**.
Since the wavefront size grows and shrinks ($1 \to \min(m,n) \to 1$), we allocate two aligned buffers of
size $N + \text{padding}$.

* **Spatial Locality:** Access patterns are purely linear (streamed). This triggers the CPU's hardware prefetcher (L1
  Stream Buffer) to aggressively pull data into the L1 cache before the instructions request it, minimizing Last-Level
  Cache (LLC) misses.
* **Alignment:** Buffers are allocated on 64-byte boundaries. This prevents "split loads" (where a single vector load
  spans two cache lines), ensuring that `vmovaps` (aligned move) instructions execute with minimum latency.

### 5.3 The SIMD Kernel Pipeline

The inner loop utilizes a "Load-Compute-Store" pipeline optimized for the x86 execution ports. We process `short` (
16-bit) integers, allowing 16 elements per 256-bit register.

The kernel logic performs the following in a single cycle:

1. **Dependency Load:** `vCenter` is loaded aligned. `vLeft` is loaded unaligned at `offset - 1`.
2. **Comparison:** `vpminsw` performs 16 signed comparisons in parallel.
3. **Accumulation:** `vpaddw` adds the weight vector.
4. **Write Back:** The result is stored in the "Next" buffer.

Crucially, we utilize **Sentinel Padding** (filling the buffer edges with $\infty$). This converts control-flow logic (
checking boundaries) into data-flow logic (arithmetic comparison). Since $\min(x, \infty) = x$, the branch predictor is
never invoked, eliminating pipeline flushes due to misprediction.

### 5.4 Latency Hiding via Unrolling

Even with SIMD, read-after-write latencies on registers can occur. To mitigate this, we manually unroll the loop by a
factor of 2.
We calculate two blocks (32 cells) per iteration:

* Block A uses register set $YMM_{0-3}$
* Block B uses register set $YMM_{4-7}$

This allows the Out-of-Order (OoO) scheduler to execute instructions for Block B while Block A is waiting for memory
loads, effectively maximizing the saturation of the arithmetic logic units (ALUs).

## 6. Benchmark Results

```text
➜  src  make up
Cleaning build artifacts...
Building Standard (JIT Mode)...
Building SIMD (Native AOT Mode)...

========================================================
   EXECUTING BENCHMARKS
========================================================
Standard Implementation: VERIFIED
SIMD Implementation:     VERIFIED

========================================================
   BENCHMARK RESULTS SUMMARY
========================================================
Status:      SUCCESS (Results Match)
Standard:    1544.26 us
SIMD:        49.71 us

>>> SIMD version is 31.06 x faster (96.00% reduction)
========================================================

```