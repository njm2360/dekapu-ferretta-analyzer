namespace DekapuFeletta.Models;

public enum ShotType
{
    None,
    Vertical,
    Horizontal,
    Cross,
    Random1,
    Random2,
    Random4,
    VerticalLine,
    HorizontalLine,
    Giant,
    Random8,
}

public enum ShotRarity
{
    Common,
    Rare,
    Legendary,
    Baseline,
}

public static class ShotTypeExtensions
{
    public static string DisplayName(this ShotType shot) => shot switch
    {
        ShotType.None => "ベースライン",
        ShotType.Vertical => "縦長ショット",
        ShotType.Horizontal => "横長ショット",
        ShotType.Cross => "クロスショット",
        ShotType.Random1 => "ランダムショット1",
        ShotType.Random2 => "ランダムショット2",
        ShotType.Random4 => "ランダムショット4",
        ShotType.VerticalLine => "縦ラインショット",
        ShotType.HorizontalLine => "横ラインショット",
        ShotType.Giant => "巨大ショット",
        ShotType.Random8 => "スーパーランダムショット",
        _ => shot.ToString(),
    };

    public static ShotRarity Rarity(this ShotType shot) => shot switch
    {
        ShotType.None => ShotRarity.Baseline,
        ShotType.VerticalLine or ShotType.HorizontalLine => ShotRarity.Rare,
        ShotType.Giant or ShotType.Random8 => ShotRarity.Legendary,
        _ => ShotRarity.Common,
    };

    public static bool IsPurchasable(this ShotType shot) => shot.Rarity() == ShotRarity.Common;

    public static int RandomPickCount(this ShotType shot) => shot switch
    {
        ShotType.Random1 => 1,
        ShotType.Random2 => 2,
        ShotType.Random4 => 4,
        ShotType.Random8 => 8,
        _ => 0,
    };
}
