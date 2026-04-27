using System.Numerics;
using DekapuFeletta.Models;

namespace DekapuFeletta.Services;

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

        int preActive = BitOperations.PopCount(activeMask);
        Span<int> preReachByCell = stackalloc int[BoardLayout.CellCount];
        int preReach = AnalyzeBoard(activeMask, BoardLayout.LineMasks, preReachByCell, out int preTriggers);

        var counts = new long[ShotResult.MaxLines + 1];
        long deltaActiveSum = 0;
        long deltaReachSum = 0;
        long triggerCellsSum = 0;
        long resetCellsSum = 0;

        int k = shot.RandomPickCount();

        if (k == 0)
        {
            CalculateDeterministic(activeMask, shot, preActive, preReach, preTriggers,
                counts, ref deltaActiveSum, ref deltaReachSum, ref triggerCellsSum, ref resetCellsSum);
        }
        else
        {
            CalculateRandom(activeMask, k, preActive, preReach, preTriggers,
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
        uint activeMask, ShotType shot, int preActive, int preReach, int preTriggers,
        long[] counts, ref long deltaActiveSum, ref long deltaReachSum,
        ref long triggerCellsSum, ref long resetCellsSum)
    {
        var lineMasks = BoardLayout.LineMasks;
        Span<int> reachByCell = stackalloc int[BoardLayout.CellCount];

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
            ProcessOutcome(activeMask, addMask, lineMasks, preActive, preReach,
                reachByCell, counts,
                ref deltaActiveSum, ref deltaReachSum, ref triggerCellsSum, ref resetCellsSum);
        }
    }

    private static void CalculateRandom(
        uint activeMask, int k, int preActive, int preReach, int preTriggers,
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

            Span<int> reachByCell = stackalloc int[BoardLayout.CellCount];
            var indices = new int[k];
            for (int i = 0; i < k; i++) indices[i] = i;

            while (true)
            {
                uint addMask = nBit;
                for (int i = 0; i < k; i++) addMask |= candidateBits[indices[i]];

                ProcessOutcome(activeMask, addMask, lineMasks, preActive, preReach,
                    reachByCell, acc.Counts,
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
        int preActive, int preReach,
        Span<int> reachByCell, long[] counts,
        ref long deltaActiveSum, ref long deltaReachSum,
        ref long triggerCellsSum, ref long resetCellsSum)
    {
        uint newMask = activeMask | addMask;

        uint resetMask = 0;
        int newLines = 0;
        for (int i = 0; i < lineMasks.Length; i++)
        {
            uint lm = lineMasks[i];
            if ((newMask & lm) == lm && (activeMask & lm) != lm)
            {
                newLines++;
                resetMask |= lm;
            }
        }

        counts[newLines]++;

        uint postMask = (newMask & ~resetMask) | BoardLayout.CenterBit;
        int postActive = BitOperations.PopCount(postMask);
        int postReach = AnalyzeBoard(postMask, lineMasks, reachByCell, out int postTriggers);
        int resetCells = BitOperations.PopCount(resetMask & ~BoardLayout.CenterBit);

        deltaActiveSum += postActive - preActive;
        deltaReachSum += postReach - preReach;
        triggerCellsSum += postTriggers;
        resetCellsSum += resetCells;
    }

    private static int AnalyzeBoard(uint mask, uint[] lineMasks, Span<int> reachByCell, out int triggerCells)
    {
        reachByCell.Clear();
        int reachCount = 0;
        for (int i = 0; i < lineMasks.Length; i++)
        {
            uint lm = lineMasks[i];
            uint inLine = mask & lm;
            if (BitOperations.PopCount(inLine) == 4)
            {
                reachCount++;
                uint missing = lm & ~inLine;
                int idx = BitOperations.TrailingZeroCount(missing);
                reachByCell[idx]++;
            }
        }
        int triggers = 0;
        for (int i = 0; i < BoardLayout.CellCount; i++)
            if (reachByCell[i] >= 2) triggers++;
        triggerCells = triggers;
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
