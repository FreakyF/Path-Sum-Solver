# Path-Sum-Solver | High-Performance SIMD Optimization

A high-performance system for computing minimum path sums on integer grids using hardware-accelerated wavefront propagation and AVX2 intrinsics.

## 🧩 The Challenge: Minimum Path Sum
The system is designed to solve the classic optimization problem of finding the minimum path sum from the top-left ($A_{0,0}$) to the bottom-right ($A_{m,n}$) of a grid filled with non-negative integers.

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


## 📺 Performance Benchmark
*Comparison between a standard JIT-compiled DP approach and the optimized Native AOT SIMD kernel.*

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

## 🏗️ Architecture & Context
*High-level system design and execution model.*

* **Objective:** Perform high-speed path sum computation on large-scale integer grids.
* **Architecture Pattern:** Data-Oriented Design (DOD) utilizing **SIMD Wavefront Processing**.
* **Data Flow:** Procedural Seed → Linearized Weight Stream → Double-Buffered AVX2 Kernel → Scalar Reduction.

## ⚖️ Design Decisions & Trade-offs
*Technical justifications for engineering choices.*

* **Memory Management: Native Memory & Unsafe Pointers**
    * **Context:** High-frequency memory access in the hot loop ($10^6$ iterations).
    * **Decision:** Utilization of `NativeMemory` with `unsafe` pointer arithmetic.
    * **Rationale:** Eliminated Garbage Collector (GC) pauses and Array Bounds Check (ABC) overhead to ensure deterministic latency.
    * **Trade-off:** Sacrificed managed safety for raw access speed and explicit cache-line alignment.

* **Execution Strategy: Diagonal Wavefront Iteration**
    * **Context:** Standard Row-Major traversal has a strict serial dependency ($D_{r,c}$ depends on $D_{r,c-1}$).
    * **Decision:** Transition to **Diagonal Wavefront Iteration**.
    * **Rationale:** Diagonal iteration isolates dependencies to the previous wavefront, enabling parallel SIMD execution within the current diagonal.
    * **Trade-off:** Sacrificed spatial locality of the source grid for Instruction-Level Parallelism (ILP), requiring an auxiliary pre-linearization step.

* **Compilation Model: Native AOT**
    * **Context:** Requirement for minimal cold-start performance jitter.
    * **Decision:** Ahead-of-Time (AOT) compilation.
    * **Rationale:** Produces a self-contained executable with aggressive optimization hints locked at compile time.
    * **Trade-off:** Sacrificed binary portability for stable instruction emission and reduced startup time.

## 🧠 Engineering Challenges
*Analysis of non-trivial technical hurdles addressed.*

* **Challenge: Control Flow Hazards**
    * **Problem:** Conditional logic for grid boundaries causes CPU branch mispredictions, flushing the instruction pipeline.
    * **Implementation:** **Branchless Sentinel Strategy**. Padded simulation buffers with `Infinity` values ($20000$) beyond valid grid boundaries. The kernel computes $\min(\text{Valid}, \infty)$ without branching.
    * **Outcome:** Zero branch instructions in the inner loop; consistent throughput regardless of grid position.

* **Challenge: Read-After-Write Latency**
    * **Problem:** Dependency chains between vector instructions stall execution ports.
    * **Implementation:** **Manual Loop Unrolling (2x)**. The kernel processes two independent 256-bit vectors (**32 cells**) per iteration using independent register sets.
    * **Outcome:** Saturated CPU execution ports by allowing Out-of-Order (OoO) execution to hide memory load latency.

## 🛠️ Tech Stack & Ecosystem
* **Core:** C# (Unsafe Context, System.Runtime.Intrinsics)
* **Infrastructure:** .NET Native AOT / Linux `taskset` (Core Isolation)
* **Tooling:** AVX2 Intrinsics, `perf`, `make`

## 🧪 Quality & Standards
* **Testing Strategy:** Dual-Implementation Verification. The optimized SIMD kernel is strictly validated against a scalar Reference Implementation (Standard DP) using identical procedural seeds.
* **Observability:** High-resolution hardware timestamping measuring microsecond-level execution time.
* **Engineering Principles:** Zero-Allocation on Hot Paths, Cache-Line Alignment (64-byte), and Hardware Intrinsics over Compiler Auto-Vectorization.

## 🙋‍♂️ Author

**Kamil Fudala**

- [GitHub](https://github.com/FreakyF)
- [LinkedIn](https://www.linkedin.com/in/kamil-fudala/)

## ⚖️ License

This project is licensed under the [MIT License](LICENSE).
