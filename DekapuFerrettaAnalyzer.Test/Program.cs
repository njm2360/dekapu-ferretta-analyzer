using System.Numerics;
using DekapuFerrettaAnalyzer.Models;
using DekapuFerrettaAnalyzer.Services;

namespace DekapuFerrettaAnalyzer.Test;

internal static class Program
{
    private static int _failed;

    private static void Main()
    {
        TestEmptyBoardBaseline();
        TestEmptyBoardCrossNoCompletes();
        TestOneAwayColumn0Baseline();
        TestOneAwayColumn0Vertical();
        TestRandom2GeqRandom1();
        TestRandom4GeqRandom2();
        TestSymmetryHorizontalVertical();
        TestFullBoardAllLose();
        TestLineMaskCount();
        TestVerticalLineGuaranteesColumn();
        TestHorizontalLineGuaranteesRow();
        TestGiantCovers3x3();
        TestRandom8GeqRandom4();
        TestRandom8TwelveLineScenario();
        BenchmarkRandom8();

        if (_failed == 0)
            Console.WriteLine("\nALL TESTS PASSED");
        else
            Console.WriteLine($"\n{_failed} TEST(S) FAILED");

        Environment.Exit(_failed == 0 ? 0 : 1);
    }

    private static void Assert(bool condition, string label)
    {
        if (condition)
        {
            Console.WriteLine($"  PASS  {label}");
        }
        else
        {
            Console.WriteLine($"  FAIL  {label}");
            _failed++;
        }
    }

    private static void TestEmptyBoardBaseline()
    {
        Console.WriteLine("[empty board, baseline]");
        var r = ProbabilityCalculator.Calculate(0u, ShotType.None);
        Assert(r.TotalOutcomes == 25, "total = 25");
        Assert(r.OutcomeCounts[0] == 25, "all outcomes = 0 lines");
        Assert(r.Expected.Numerator.IsZero, "E[L] = 0");
    }

    private static void TestEmptyBoardCrossNoCompletes()
    {
        Console.WriteLine("[empty board, cross]");
        var r = ProbabilityCalculator.Calculate(0u, ShotType.Cross);
        Assert(r.TotalOutcomes == 25, "total = 25");
        Assert(r.OutcomeCounts[0] == 25, "no line completes from empty even with cross");
    }

    private static void TestOneAwayColumn0Baseline()
    {
        Console.WriteLine("[col0 missing only 5, baseline]");
        uint mask = BoardLayout.Bit(1) | BoardLayout.Bit(2) | BoardLayout.Bit(3) | BoardLayout.Bit(4);
        var r = ProbabilityCalculator.Calculate(mask, ShotType.None);
        Assert(r.TotalOutcomes == 25, "total = 25");
        Assert(r.OutcomeCounts[1] == 1, "exactly 1 outcome (n=5) gives 1 line");
        Assert(r.OutcomeCounts[0] == 24, "remaining 24 outcomes give 0 lines");
        Assert(r.Expected == new Fraction(1, 25), "E[L] = 1/25");
    }

    private static void TestOneAwayColumn0Vertical()
    {
        Console.WriteLine("[col0 missing only 5, vertical]");
        uint mask = BoardLayout.Bit(1) | BoardLayout.Bit(2) | BoardLayout.Bit(3) | BoardLayout.Bit(4);
        var r = ProbabilityCalculator.Calculate(mask, ShotType.Vertical);
        // Only n=5 hit makes col 0 line. n=4 LOSE (already active) and n=1 LOSE.
        // n=5 (r=4,c=0): vertical adds (3,0)=4 (already), (0,0)=1 (already wrap). Plus n=5. Col 0 complete.
        Assert(r.OutcomeCounts[1] == 1, "vertical: only n=5 contributes 1 line");
    }

    private static void TestRandom2GeqRandom1()
    {
        Console.WriteLine("[col0 one-away: random2 expected >= random1]")
;
        uint mask = BoardLayout.Bit(1) | BoardLayout.Bit(2) | BoardLayout.Bit(3) | BoardLayout.Bit(4);
        var r1 = ProbabilityCalculator.Calculate(mask, ShotType.Random1);
        var r2 = ProbabilityCalculator.Calculate(mask, ShotType.Random2);
        Assert(r2.Expected >= r1.Expected, $"E[Random2]={r2.Expected.ToDouble():F6} >= E[Random1]={r1.Expected.ToDouble():F6}");
    }

    private static void TestRandom4GeqRandom2()
    {
        Console.WriteLine("[col0 one-away: random4 expected >= random2]");
        uint mask = BoardLayout.Bit(1) | BoardLayout.Bit(2) | BoardLayout.Bit(3) | BoardLayout.Bit(4);
        var r2 = ProbabilityCalculator.Calculate(mask, ShotType.Random2);
        var r4 = ProbabilityCalculator.Calculate(mask, ShotType.Random4);
        Assert(r4.Expected >= r2.Expected, $"E[Random4]={r4.Expected.ToDouble():F6} >= E[Random2]={r2.Expected.ToDouble():F6}");
    }

    private static void TestSymmetryHorizontalVertical()
    {
        Console.WriteLine("[symmetry: row1 missing 6 vs col0 missing 1 -> vert/horz swap]");
        // Board with row 0 having 6,11,16,21 active (missing 1). Need horz shot help with N=1 wrapping to 21.
        uint mask = BoardLayout.Bit(6) | BoardLayout.Bit(11) | BoardLayout.Bit(16) | BoardLayout.Bit(21);
        var rH = ProbabilityCalculator.Calculate(mask, ShotType.Horizontal);

        // Mirror: col 0 missing 1: active 2,3,4,5
        uint mask2 = BoardLayout.Bit(2) | BoardLayout.Bit(3) | BoardLayout.Bit(4) | BoardLayout.Bit(5);
        var rV = ProbabilityCalculator.Calculate(mask2, ShotType.Vertical);

        Assert(rH.Expected == rV.Expected, $"E[H on row]={rH.Expected.ToDouble():F6} == E[V on col]={rV.Expected.ToDouble():F6}");
    }

    private static void TestFullBoardAllLose()
    {
        Console.WriteLine("[full board: all outcomes LOSE]");
        uint mask = (1u << 25) - 1; // all 25 cells active
        foreach (var s in new[] { ShotType.None, ShotType.Vertical, ShotType.Cross, ShotType.Random4 })
        {
            var r = ProbabilityCalculator.Calculate(mask, s);
            Assert(r.OutcomeCounts[0] == r.TotalOutcomes, $"{s}: all outcomes = 0 lines");
            Assert(r.Expected.Numerator.IsZero, $"{s}: E[L] = 0");
        }
    }

    private static void TestVerticalLineGuaranteesColumn()
    {
        Console.WriteLine("[empty board, VerticalLine: each hit completes its column]");
        var r = ProbabilityCalculator.Calculate(0u, ShotType.VerticalLine);
        // Center column: 13 always active. N=11,12,14,15 hit -> column 2 fully active -> col 2 complete (1 line)
        //                                    plus row through N completes only if all 5 row cells active (no, row not filled)
        // Other columns: N hits and that column gets filled -> column complete (1 line)
        // N=13 LOSE.
        // So 24 hits, each gives at least 1 line.
        long zeroes = r.OutcomeCounts[0].IsZero ? 0 : (long)r.OutcomeCounts[0];
        Assert(zeroes == 1, $"VerticalLine on empty board: only N=13 (LOSE) gives 0 lines, got {zeroes}");
        long total = (long)r.TotalOutcomes;
        Assert(total == 25, $"total = 25, got {total}");
    }

    private static void TestHorizontalLineGuaranteesRow()
    {
        Console.WriteLine("[empty board, HorizontalLine: each hit completes its row]");
        var r = ProbabilityCalculator.Calculate(0u, ShotType.HorizontalLine);
        long zeroes = (long)r.OutcomeCounts[0];
        Assert(zeroes == 1, $"HorizontalLine on empty board: only N=13 (LOSE) gives 0 lines, got {zeroes}");
    }

    private static void TestGiantCovers3x3()
    {
        Console.WriteLine("[empty board, Giant: activates 3x3 (9 cells)]");
        // Giant from empty board: activates 9 cells. No 5-cell line can be all in a 3x3 area unless aligned.
        // A row spans 5 columns, but Giant only covers 3 consecutive columns -> can't complete a row.
        // Similarly columns. Diagonals: 3x3 covers at most 3 cells of a 5-diagonal. So no line completes.
        var r = ProbabilityCalculator.Calculate(0u, ShotType.Giant);
        Assert(r.OutcomeCounts[0] == 25, "Giant from empty: no line completes (3x3 can't fill a 5-line)");
    }

    private static void TestRandom8GeqRandom4()
    {
        Console.WriteLine("[col0 one-away: random8 expected >= random4]");
        uint mask = BoardLayout.Bit(1) | BoardLayout.Bit(2) | BoardLayout.Bit(3) | BoardLayout.Bit(4);
        var r4 = ProbabilityCalculator.Calculate(mask, ShotType.Random4);
        var r8 = ProbabilityCalculator.Calculate(mask, ShotType.Random8);
        Assert(r8.Expected >= r4.Expected, $"E[Random8]={r8.Expected.ToDouble():F6} >= E[Random4]={r4.Expected.ToDouble():F6}");
    }

    private static void TestRandom8TwelveLineScenario()
    {
        Console.WriteLine("[5-missing transversal board: Random8 hits 12 lines]");
        // Missing = {1, 9, 15, 17, 23} forms transversal hitting both diagonals (excluding center 13)
        uint allBits = (1u << 25) - 1;
        uint missing = BoardLayout.Bit(1) | BoardLayout.Bit(9) | BoardLayout.Bit(15) | BoardLayout.Bit(17) | BoardLayout.Bit(23);
        uint mask = allBits & ~missing;

        var r4 = ProbabilityCalculator.Calculate(mask, ShotType.Random4);
        var r8 = ProbabilityCalculator.Calculate(mask, ShotType.Random8);

        // Random4 P(L=12): N must hit one of 5 missing (5 ways), picks must be exactly the other 4 missing (1 / C(24,4))
        // = 5 / (25 * 10626) = 1/53130
        Assert(r4.OutcomeCounts[12] == 5, $"Random4 12-line outcomes = 5, got {r4.OutcomeCounts[12]}");

        // Random8 P(L=12): N must hit one of 5 missing, picks must include the other 4 missing (any 4 of remaining 4 mandatory + 4 free from 20)
        // For each of the 5 N values: C(20, 4) = 4845 ways to choose the 4 extra picks
        // Total = 5 * 4845 = 24225
        Assert(r8.OutcomeCounts[12] == 24225, $"Random8 12-line outcomes = 24225, got {r8.OutcomeCounts[12]}");
    }

    private static void BenchmarkRandom8()
    {
        Console.WriteLine("[benchmark Random8 on a populated board]");
        uint mask = 0;
        for (int n = 1; n <= 25; n++) if ((n % 2) == 1) mask |= BoardLayout.Bit(n);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = ProbabilityCalculator.Calculate(mask, ShotType.Random8);
        sw.Stop();
        long total = (long)r.TotalOutcomes;
        Console.WriteLine($"  Random8: {total:N0} outcomes in {sw.ElapsedMilliseconds} ms (E[L]={r.Expected.ToDouble():F4})");
        Assert(total == 25L * 735471, $"Random8 total = 25 * C(24,8) = 18,386,775, got {total}");
    }

    private static void TestLineMaskCount()
    {
        Console.WriteLine("[line definitions]");
        Assert(BoardLayout.LineMasks.Length == 12, "12 lines (5 rows + 5 cols + 2 diagonals)");
        // Each line should have exactly 5 cells set
        foreach (var m in BoardLayout.LineMasks)
        {
            Assert(BitOperations.PopCount(m) == 5, $"line mask has 5 cells (mask={m:X})");
        }
        // All lines should include exactly 25 cells when ORed (each cell on 1+ lines)
        uint union = 0;
        foreach (var m in BoardLayout.LineMasks) union |= m;
        Assert(union == ((1u << 25) - 1), "lines cover all 25 cells");
    }
}
