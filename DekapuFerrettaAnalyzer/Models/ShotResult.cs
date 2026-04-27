using System.Numerics;

namespace DekapuFerrettaAnalyzer.Models;

public sealed class ShotResult
{
    public const int MaxLines = 12;
    public const int TierCap = 5;

    public ShotType Shot { get; }
    public BigInteger TotalOutcomes { get; }
    public BigInteger[] OutcomeCounts { get; }
    public Fraction[] Distribution { get; }
    public Fraction Expected { get; }
    public Fraction TierScore { get; }

    /// <summary>E[L | L≥1] - ヒット時の条件付き期待ライン数。ヒット率0なら null</summary>
    public Fraction? ConditionalExpected { get; }

    /// <summary>1抽選後の「アクティブマス数の変化量」の期待値。リセット込み。負なら蓄積を消費している。</summary>
    public Fraction ExpectedDeltaActive { get; }
    /// <summary>1抽選後の「リーチライン数（あと1マスでライン成立）」の変化量の期待値。</summary>
    public Fraction ExpectedDeltaReach { get; }
    /// <summary>1抽選後の盤面で「2本以上のリーチが集中する非アクティブマス数」の期待値。次回大ティアの種。</summary>
    public Fraction ExpectedTriggerCells { get; }
    /// <summary>1抽選で成立により非アクティブに戻されるマス数の期待値 (中央★は除く)。蓄積消費コスト。</summary>
    public Fraction ExpectedResetCells { get; }

    public ShotResult(
        ShotType shot,
        BigInteger[] outcomeCounts,
        BigInteger deltaActiveSum,
        BigInteger deltaReachSum,
        BigInteger triggerCellsSum,
        BigInteger resetCellsSum)
    {
        Shot = shot;
        OutcomeCounts = outcomeCounts;

        BigInteger total = BigInteger.Zero;
        foreach (var c in outcomeCounts) total += c;
        TotalOutcomes = total;

        Distribution = new Fraction[outcomeCounts.Length];
        var expectedNumerator = BigInteger.Zero;
        var tierScoreNumerator = BigInteger.Zero;
        var hitCount = BigInteger.Zero;
        for (int i = 0; i < outcomeCounts.Length; i++)
        {
            Distribution[i] = new Fraction(outcomeCounts[i], total);
            expectedNumerator += outcomeCounts[i] * i;
            int tier = i > TierCap ? TierCap : i;
            tierScoreNumerator += outcomeCounts[i] * (tier * tier);
            if (i >= 1) hitCount += outcomeCounts[i];
        }
        Expected = new Fraction(expectedNumerator, total);
        TierScore = new Fraction(tierScoreNumerator, total);

        ConditionalExpected = hitCount.IsZero ? null : new Fraction(expectedNumerator, hitCount);

        ExpectedDeltaActive = new Fraction(deltaActiveSum, total);
        ExpectedDeltaReach = new Fraction(deltaReachSum, total);
        ExpectedTriggerCells = new Fraction(triggerCellsSum, total);
        ExpectedResetCells = new Fraction(resetCellsSum, total);
    }
}
