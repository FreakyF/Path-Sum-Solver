using System;
using System.Diagnostics;

namespace PathSum.Standard;

// ReSharper disable once ClassNeverInstantiated.Global
public class Program
{
    private const int N = 1024;
    private const int Seed = 123;

    private static void Main()
    {
        Console.WriteLine("--- VERIFICATION PHASE ---");

        var sourceGrid = PrecomputeWeights(N, Seed);

        var verifyGrid = CloneGrid(sourceGrid);
        var result = MinPathSum(verifyGrid);

        Console.WriteLine($"[CHECK] Result for {N}x{N}: {result}");

        if (VerifySmallScale())
        {
            Console.WriteLine("[PASS] Test 3x3: Result 7 is correct.");
        }

        Console.WriteLine("\n>>> STARTING BENCHMARK <<<");

        var workGrid = CloneGrid(sourceGrid);

        RunBench(sourceGrid, workGrid);
    }

    private static void RunBench(int[][] source, int[][] work)
    {
        const int iter = 1000;

        MinPathSum(CloneGrid(source));

        var start = Stopwatch.GetTimestamp();

        for (var i = 0; i < iter; i++)
        {
            ResetGrid(work, source);

            MinPathSum(work);
        }

        var end = Stopwatch.GetTimestamp();

        var us = (double)(end - start) / Stopwatch.Frequency * 1_000_000 / iter;
        Console.WriteLine($"[AVG] {us:F2} us");
    }


    private static int MinPathSum(int[][] grid)
    {
        var rows = grid.Length;
        var cols = grid[0].Length;

        InitializeEdges(grid, rows, cols);
        FillRemainingPaths(grid, rows, cols);

        return grid[rows - 1][cols - 1];
    }

    private static void InitializeEdges(int[][] grid, int rows, int cols)
    {
        for (var j = 1; j < cols; j++)
        {
            grid[0][j] += grid[0][j - 1];
        }

        for (var i = 1; i < rows; i++)
        {
            grid[i][0] += grid[i - 1][0];
        }
    }

    private static void FillRemainingPaths(int[][] grid, int rows, int cols)
    {
        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                grid[i][j] += Math.Min(grid[i - 1][j], grid[i][j - 1]);
            }
        }
    }

    private static int[][] PrecomputeWeights(int size, int seed)
    {
        var grid = new int[size][];
        for (var r = 0; r < size; r++)
        {
            grid[r] = new int[size];
            for (var c = 0; c < size; c++)
            {
                grid[r][c] = ((r ^ c ^ seed) & 15) + 1;
            }
        }

        return grid;
    }

    private static void ResetGrid(int[][] destination, int[][] source)
    {
        for (var i = 0; i < source.Length; i++)
        {
            Array.Copy(source[i], destination[i], source[i].Length);
        }
    }

    private static int[][] CloneGrid(int[][] grid)
    {
        var clone = new int[grid.Length][];
        for (var i = 0; i < grid.Length; i++) clone[i] = (int[])grid[i].Clone();
        return clone;
    }

    private static bool VerifySmallScale()
    {
        int[][] grid =
        [
            [1, 3, 1],
            [1, 5, 1],
            [4, 2, 1]
        ];
        return MinPathSum(grid) == 7;
    }
}