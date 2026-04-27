using System.Numerics;

namespace DekapuFerrettaAnalyzer.Models;

public readonly struct Fraction : IEquatable<Fraction>, IComparable<Fraction>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public static readonly Fraction Zero = new(BigInteger.Zero, BigInteger.One, reduce: false);
    public static readonly Fraction One = new(BigInteger.One, BigInteger.One, reduce: false);

    public Fraction(BigInteger numerator, BigInteger denominator)
        : this(numerator, denominator, reduce: true) { }

    private Fraction(BigInteger numerator, BigInteger denominator, bool reduce)
    {
        if (denominator.IsZero)
            throw new DivideByZeroException("Fraction denominator cannot be zero.");

        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        if (reduce && !numerator.IsZero)
        {
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            if (gcd > BigInteger.One)
            {
                numerator /= gcd;
                denominator /= gcd;
            }
        }
        else if (numerator.IsZero)
        {
            denominator = BigInteger.One;
        }

        Numerator = numerator;
        Denominator = denominator;
    }

    public static Fraction FromInt(long value) => new(new BigInteger(value), BigInteger.One, reduce: false);

    public static Fraction operator +(Fraction a, Fraction b)
        => new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Fraction operator -(Fraction a, Fraction b)
        => new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static Fraction operator *(Fraction a, Fraction b)
        => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

    public static Fraction operator /(Fraction a, Fraction b)
        => new(a.Numerator * b.Denominator, a.Denominator * b.Numerator);

    public static bool operator ==(Fraction a, Fraction b) => a.Equals(b);
    public static bool operator !=(Fraction a, Fraction b) => !a.Equals(b);
    public static bool operator <(Fraction a, Fraction b) => a.CompareTo(b) < 0;
    public static bool operator >(Fraction a, Fraction b) => a.CompareTo(b) > 0;
    public static bool operator <=(Fraction a, Fraction b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Fraction a, Fraction b) => a.CompareTo(b) >= 0;

    public int CompareTo(Fraction other)
        => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public bool Equals(Fraction other) => Numerator == other.Numerator && Denominator == other.Denominator;
    public override bool Equals(object? obj) => obj is Fraction f && Equals(f);
    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public double ToDouble()
    {
        if (Denominator == BigInteger.One) return (double)Numerator;
        return (double)Numerator / (double)Denominator;
    }

    public override string ToString() => $"{Numerator}/{Denominator}";
}
