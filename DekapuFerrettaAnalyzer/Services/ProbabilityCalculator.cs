using System.Numerics;
using DekapuFerrettaAnalyzer.Models;

namespace DekapuFerrettaAnalyzer.Services;

public static class ProbabilityCalculator
{
    private static readonly int[] RandomCandidates = BuildRandomCandidates();
    private static readonly uint[] CandidateBits = BuildCandidateBits();

    private static int[] BuildRandomCandidates()
    {
        var list = new List<int>(24);
        for (int n = 1; n <= BoardLayout.CellCount; n++)
            if (n != BoardLayout.CenterNumber)
                list.Add(n);
        return list.ToArray();
    }

    private static uint[] BuildCandidateBits()
    {
        var arr = new uint[RandomCandidates.Length];
        for (int i = 0; i < arr.Length; i++)
            arr[i] = BoardLayout.Bit(RandomCandidates[i]);
        return arr;
    }

    private sealed class Accum
    {
        public long[] Counts = null!;
        public long DeltaActiveSum;
        public long DeltaReachSum;
        public long TriggerCellsSum;
        public long ResetCellsSum;
    }

    public static ShotResult Calculate(uint activeMask, ShotType shot)
    {
        activeMask |= BoardLayout.CenterBit;

        int preReach = AnalyzeBoard(activeMask, BoardLayout.LineMasks, out int preTriggers);

        // Precompute which lines are already complete in the pre-state.
        // Bit i is set iff lineMasks[i] is fully active in activeMask.
        // These lines can never produce "newly completed" outcomes regardless of N/picks.
        var lineMasks = BoardLayout.LineMasks;
        ushort preCompleteBits = 0;
        for (int i = 0; i < lineMasks.Length; i++)
            if ((activeMask & lineMasks[i]) == lineMasks[i])
                preCompleteBits |= (ushort)(1 << i);

        var counts = new long[ShotResult.MaxLines + 1];
        long deltaActiveSum = 0;
        long deltaReachSum = 0;
        long triggerCellsSum = 0;
        long resetCellsSum = 0;

        int k = shot.RandomPickCount();

        if (k == 0)
        {
            CalculateDeterministic(activeMask, shot, preReach, preTriggers, preCompleteBits,
                counts, ref deltaActiveSum, ref deltaReachSum, ref triggerCellsSum, ref resetCellsSum);
        }
        else
        {
            CalculateRandom(activeMask, k, preReach, preTriggers, preCompleteBits,
                counts, ref deltaActiveSum, ref deltaReachSum, ref triggerCellsSum, ref resetCellsSum);
        }

        var bigCounts = new BigInteger[counts.Length];
        for (int i = 0; i < counts.Length; i++)
            bigCounts[i] = new BigInteger(counts[i]);

        return new ShotResult(
            shot,
            bigCounts,
            new BigInteger(deltaActiveSum),
            new BigInteger(deltaReachSum),
            new BigInteger(triggerCellsSum),
            new BigInteger(resetCellsSum));
    }

    private static void CalculateDeterministic(
        uint activeMask, ShotType shot, int preReach, int preTriggers, ushort preCompleteBits,
        long[] counts, ref long deltaActiveSum, ref long deltaReachSum,
        ref long triggerCellsSum, ref long resetCellsSum)
    {
        var lineMasks = BoardLayout.LineMasks;

        for (int n = 1; n <= BoardLayout.CellCount; n++)
        {
            uint nBit = BoardLayout.Bit(n);
            if ((activeMask & nBit) != 0)
            {
                counts[0]++;
                triggerCellsSum += preTriggers;
                continue;
            }

            uint addMask = nBit | ShotEffects.GetDeterministicAddMask(shot, n);
            ProcessOutcome(activeMask, addMask, lineMasks, preReach, preCompleteBits,
                counts,
                ref deltaActiveSum, ref deltaReachSum, ref triggerCellsSum, ref resetCellsSum);
        }
    }

    private static void CalculateRandom(
        uint activeMask, int k, int preReach, int preTriggers, ushort preCompleteBits,
        long[] counts, ref long deltaActiveSum, ref long deltaReachSum,
        ref long triggerCellsSum, ref long resetCellsSum)
    {
        var lineMasks = BoardLayout.LineMasks;
        var candidateBits = CandidateBits;
        int candidateCount = candidateBits.Length;

        long combinationCount = BinomialLong(candidateCount, k);
        int bucketLen = counts.Length;

        var partials = new Accum[BoardLayout.CellCount];

        Parallel.For(1, BoardLayout.CellCount + 1, n =>
        {
            var acc = new Accum { Counts = new long[bucketLen] };
            partials[n - 1] = acc;

            uint nBit = BoardLayout.Bit(n);
            if ((activeMask & nBit) != 0)
            {
                acc.Counts[0] += combinationCount;
                acc.TriggerCellsSum += preTriggers * combinationCount;
                return;
            }

            var indices = new int[k];
            for (int i = 0; i < k; i++) indices[i] = i;

            while (true)
            {
                uint addMask = nBit;
                for (int i = 0; i < k; i++) addMask |= candidateBits[indices[i]];

                ProcessOutcome(activeMask, addMask, lineMasks, preReach, preCompleteBits,
                    acc.Counts,
                    ref acc.DeltaActiveSum, ref acc.DeltaReachSum,
                    ref acc.TriggerCellsSum, ref acc.ResetCellsSum);

                int j = k - 1;
                while (j >= 0 && indices[j] == candidateCount - k + j) j--;
                if (j < 0) break;
                indices[j]++;
                for (int i = j + 1; i < k; i++) indices[i] = indices[i - 1] + 1;
            }
        });

        for (int n = 0; n < BoardLayout.CellCount; n++)
        {
            var p = partials[n];
            for (int i = 0; i < bucketLen; i++)
                counts[i] += p.Counts[i];
            deltaActiveSum += p.DeltaActiveSum;
            deltaReachSum += p.DeltaReachSum;
            triggerCellsSum += p.TriggerCellsSum;
            resetCellsSum += p.ResetCellsSum;
        }
    }

    /// <summary>
    /// Processes a single outcome (one specific N + shot effect):
    /// counts new lines, computes the post-spin board state (after line resets),
    /// and accumulates accumulation metrics.
    /// </summary>
    private static void ProcessOutcome(
        uint activeMask, uint addMask, uint[] lineMasks,
        int preReach, ushort preCompleteBits,
        long[] counts,
        ref long deltaActiveSum, ref long deltaReachSum,
        ref long triggerCellsSum, ref long resetCellsSum)
    {
        uint newMask = activeMask | addMask;

        uint resetMask = 0;
        int newLines = 0;
        // Skip lines already complete in pre-state — they cannot be "newly" completed.
        for (int i = 0; i < lineMasks.Length; i++)
        {
            if ((preCompleteBits & (1 << i)) != 0) continue;
            uint lm = lineMasks[i];
            if ((newMask & lm) == lm)
            {
                newLines++;
                resetMask |= lm;
            }
        }

        counts[newLines]++;

        uint postMask = (newMask & ~resetMask) | BoardLayout.CenterBit;
        int postReach = AnalyzeBoard(postMask, lineMasks, out int postTriggers);

        // delta = (cells newly added) - (cells removed by reset, excluding the always-active center)
        int newCells = BitOperations.PopCount(addMask & ~activeMask);
        int resetCells = BitOperations.PopCount(resetMask & ~BoardLayout.CenterBit);

        deltaActiveSum += newCells - resetCells;
        deltaReachSum += postReach - preReach;
        triggerCellsSum += postTriggers;
        resetCellsSum += resetCells;
    }

    /// <summary>
    /// For a given board state: count reach-lines (exactly 4 of 5 cells active)
    /// and count cells that are the missing cell of 2+ reach-lines (multi-line trigger candidates).
    /// Uses two bitmasks (reachOnce / reachMulti) instead of a per-cell counter array.
    /// </summary>
    private static int AnalyzeBoard(uint mask, uint[] lineMasks, out int triggerCells)
    {
        int reachCount = 0;
        uint reachOnce = 0;   // cells that are missing in exactly 1 reach line
        uint reachMulti = 0;  // cells that are missing in 2+ reach lines

        for (int i = 0; i < lineMasks.Length; i++)
        {
            uint lm = lineMasks[i];
            uint inLine = mask & lm;
            if (BitOperations.PopCount(inLine) == 4)
            {
                reachCount++;
                uint missing = lm & ~inLine; // exactly one bit
                if ((reachOnce & missing) != 0)
                {
                    reachOnce ^= missing;       // demote: was 1, now 2+
                    reachMulti |= missing;
                }
                else if ((reachMulti & missing) == 0)
                {
                    reachOnce |= missing;       // first reach line through this cell
                }
                // else: already 2+, stays in multi
            }
        }
        triggerCells = BitOperations.PopCount(reachMulti);
        return reachCount;
    }

    private static long BinomialLong(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;
        if (k > n - k) k = n - k;

        long result = 1;
        for (int i = 0; i < k; i++)
        {
            result *= n - i;
            result /= i + 1;
        }
        return result;
    }
}
