using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace PathSum.Simd;

// ReSharper disable once ClassNeverInstantiated.Global
public unsafe class Program
{
    private const int N = 1024;
    private const short Inf = 20000;

    public static void Main()
    {
        Console.WriteLine("--- VERIFICATION PHASE ---");

        if (!VerifyReferenceLogic()) return;

        if (!VerifyAVXOnManualGrid()) return;

        if (!VerifyLargeScale(123)) return;

        Console.WriteLine("\n>>> ALL TESTS PASSED. STARTING BENCHMARK. <<<");

        nuint bufferBytes = (N + 64) * sizeof(short); 
        void* b1 = NativeMemory.AlignedAlloc(bufferBytes, 64);
        void* b2 = NativeMemory.AlignedAlloc(bufferBytes, 64);
        nuint wBytes = 4 * 1024 * 1024;
        byte* wStream = (byte*)NativeMemory.AlignedAlloc(wBytes, 64);

        try 
        {
            new Span<short>(b1, N + 64).Fill(Inf);
            new Span<short>(b2, N + 64).Fill(Inf);
            
            PrecomputeWeightsPad(N, 123, wStream);

            short startVal = ((0 ^ 0 ^ 123) & 15) + 1;

            SolveBranchless(N, (short*)b1, (short*)b2, wStream, startVal);
            
            RunBench(N, (short*)b1, (short*)b2, wStream, startVal);
        }
        finally
        {
            NativeMemory.AlignedFree(b1);
            NativeMemory.AlignedFree(b2);
            NativeMemory.AlignedFree(wStream);
        }
    }

    private static bool VerifyAVXOnManualGrid()
    {
        int[,] grid = {
            { 1, 3, 1 },
            { 1, 5, 1 },
            { 4, 2, 1 }
        };
        int size = 3;
        short startVal = (short)grid[0,0];

        nuint bytes = (nuint)((size + 64) * sizeof(short));
        void* b1 = NativeMemory.AlignedAlloc(bytes, 64);
        void* b2 = NativeMemory.AlignedAlloc(bytes, 64);
        new Span<short>(b1, size + 64).Fill(Inf);
        new Span<short>(b2, size + 64).Fill(Inf);

        byte* wStream = (byte*)NativeMemory.AlignedAlloc(4096, 64);
        int cursor = 0;

        for (int k = 1; k < size; k++)
        {
            int count = k + 1;
            for (int j = 0; j < count; j++)
            {
                int r = j;
                int c = k - r;
                wStream[cursor++] = (byte)grid[r, c];
            }
            while ((cursor & 31) != 0) wStream[cursor++] = 0;
        }
        for (int k = size; k < 2 * size - 1; k++)
        {
            int rStart = k - size + 1;
            int count = size - rStart;
            for (int j = 0; j < count; j++)
            {
                int r = rStart + j;
                int c = k - r;
                wStream[cursor++] = (byte)grid[r, c];
            }
            while ((cursor & 31) != 0) wStream[cursor++] = 0;
        }

        int result = SolveBranchless(size, (short*)b1, (short*)b2, wStream, startVal);

        NativeMemory.AlignedFree(b1);
        NativeMemory.AlignedFree(b2);
        NativeMemory.AlignedFree(wStream);

        if (result == 7)
        {
            Console.WriteLine("[PASS] Test 3x3 (AVX Engine): Result 7 is correct.");
            return true;
        }
        else
        {
            Console.WriteLine($"[FAIL] Test 3x3 (AVX Engine): Got {result}, Expected 7.");
            return false;
        }
    }

    private static bool VerifyReferenceLogic()
    {
        int[,] dp = new int[3,3];
        dp[0,0] = 1;
        dp[0,1] = dp[0,0] + 3; dp[0,2] = dp[0,1] + 1;
        dp[1,0] = dp[0,0] + 1; dp[2,0] = dp[1,0] + 4;
        
        dp[1,1] = Math.Min(dp[0,1], dp[1,0]) + 5; 
        dp[1,2] = Math.Min(dp[0,2], dp[1,1]) + 1;
        dp[2,1] = Math.Min(dp[1,1], dp[2,0]) + 2; 
        dp[2,2] = Math.Min(dp[1,2], dp[2,1]) + 1;

        if (dp[2,2] == 7)
        {
            Console.WriteLine("[PASS] Test 3x3 (Reference Logic): Result 7 is correct.");
            return true;
        }
        return false;
    }

    private static bool VerifyLargeScale(int seed)
    {
        int size = N;
        long[,] dp = new long[size, size];
        int startVal = ((0^0^seed)&15)+1;
        dp[0,0] = startVal;
        
        for(int c=1; c<size; c++) dp[0,c] = dp[0,c-1] + (((0^c^seed)&15)+1);
        for(int r=1; r<size; r++) dp[r,0] = dp[r-1,0] + (((r^0^seed)&15)+1);
        for(int r=1; r<size; r++)
            for(int c=1; c<size; c++)
                dp[r,c] = Math.Min(dp[r-1,c], dp[r,c-1]) + (((r^c^seed)&15)+1);
        
        long expected = dp[size-1, size-1];

        nuint bytes = (nuint)((size + 64) * sizeof(short));
        void* b1 = NativeMemory.AlignedAlloc(bytes, 64);
        void* b2 = NativeMemory.AlignedAlloc(bytes, 64);
        new Span<short>(b1, size+64).Fill(Inf);
        new Span<short>(b2, size+64).Fill(Inf);
        nuint wBytes = 4 * 1024 * 1024;
        byte* wStr = (byte*)NativeMemory.AlignedAlloc(wBytes, 64);
        PrecomputeWeightsPad(size, seed, wStr);

        int actual = SolveBranchless(size, (short*)b1, (short*)b2, wStr, (short)startVal);

        NativeMemory.AlignedFree(b1); NativeMemory.AlignedFree(b2); NativeMemory.AlignedFree(wStr);

        if (actual == expected)
        {
            Console.WriteLine($"[PASS] Test 1024x1024 (Procedural): Result {actual} matches reference.");
            return true;
        }
        Console.WriteLine($"[FAIL] Test 1024x1024: Got {actual}, Expected {expected}");
        return false;
    }

    private static void PrecomputeWeightsPad(int size, int seed, byte* stream)
    {
        int cursor = 0;
        void WriteDiag(int k, int rStart, int count)
        {
            for (int j = 0; j < count; j++)
            {
                int r = rStart + j;
                int c = k - r;
                stream[cursor++] = (byte)((((r ^ c ^ seed) & 15) + 1));
            }
            while ((cursor & 31) != 0) stream[cursor++] = 0; 
        }
        for (int k = 1; k < size; k++) WriteDiag(k, 0, k + 1);
        for (int k = size; k < 2 * size - 1; k++) WriteDiag(k, k - size + 1, size - (k - size + 1));
    }

    private static void RunBench(int size, short* b1, short* b2, byte* w, short startVal)
    {
        long start = Stopwatch.GetTimestamp();
        const int iter = 100_000;
        for (int i = 0; i < iter; i++) SolveBranchless(size, b1, b2, w, startVal);
        long end = Stopwatch.GetTimestamp();
        double us = (double)(end - start) / Stopwatch.Frequency * 1_000_000 / iter;
        Console.WriteLine($"[AVG] {us:F2} us");
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static int SolveBranchless(int size, short* buff1, short* buff2, byte* wStream, short startValue)
    {
        short* pOld = buff1 + 16;
        short* pNew = buff2 + 16;
        
        pOld[0] = startValue;

        byte* pW = wStream;
        nint nSize = size;

        for (nint k = 1; k < nSize; k++)
        {
            nint count = k + 1;
            nint vectorCount = (count + 31) & ~31;
            short* pEnd = pNew + vectorCount;
            short* pO = pOld;
            short* pN = pNew;

            do 
            {
                Vector256<sbyte> vw8 = Avx.LoadAlignedVector256((sbyte*)pW);
                Vector256<short> vwLo = Avx2.ConvertToVector256Int16(vw8.GetLower());
                Vector256<short> vwHi = Avx2.ConvertToVector256Int16(vw8.GetUpper());

                Vector256<short> vCenter0 = Avx.LoadAlignedVector256(pO);
                Vector256<short> vLeft0 = Avx.LoadVector256(pO - 1);
                Avx.StoreAligned(pN, Avx2.Add(Avx2.Min(vCenter0, vLeft0), vwLo));

                Vector256<short> vCenter1 = Avx.LoadAlignedVector256(pO + 16);
                Vector256<short> vLeft1 = Avx.LoadVector256(pO + 15);
                Avx.StoreAligned(pN + 16, Avx2.Add(Avx2.Min(vCenter1, vLeft1), vwHi));

                pO += 32; pN += 32; pW += 32;
            }
            while (pN < pEnd);

            short* temp = pOld; pOld = pNew; pNew = temp;
        }

        nint totalDiags = 2 * nSize - 1;
        for (nint k = nSize; k < totalDiags; k++)
        {
            nint rStart = k - nSize + 1;
            nint count = nSize - rStart;
            nint vectorCount = (count + 31) & ~31;
            
            short* pO = pOld + rStart; 
            short* pN = pNew + rStart;
            short* pEnd = pN + vectorCount;

            do
            {
                Vector256<sbyte> vw8 = Avx.LoadAlignedVector256((sbyte*)pW);
                Vector256<short> vwLo = Avx2.ConvertToVector256Int16(vw8.GetLower());
                Vector256<short> vwHi = Avx2.ConvertToVector256Int16(vw8.GetUpper());

                Vector256<short> vCenter0 = Avx.LoadVector256(pO);
                Vector256<short> vLeft0 = Avx.LoadVector256(pO - 1);
                Avx.Store(pN, Avx2.Add(Avx2.Min(vCenter0, vLeft0), vwLo));

                Vector256<short> vCenter1 = Avx.LoadVector256(pO + 16);
                Vector256<short> vLeft1 = Avx.LoadVector256(pO + 15);
                Avx.Store(pN + 16, Avx2.Add(Avx2.Min(vCenter1, vLeft1), vwHi));

                pO += 32; pN += 32; pW += 32;
            }
            while (pN < pEnd);

            short* temp = pOld; pOld = pNew; pNew = temp;
        }

        return pOld[size - 1];
    }
}