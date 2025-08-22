using System;
using UnityEngine;

[Serializable]
public struct BigNumber : IComparable<BigNumber>
{
    public double mantissa; // [0, 1000)
    public int tier;        // 0 = no suffix, 1=A(1e3), 2=B(1e6), ...

    public static readonly BigNumber Zero = new BigNumber(0, 0);
    public static readonly BigNumber One = new BigNumber(1, 0);

    public BigNumber(double mantissa, int tier)
    {
        this.mantissa = mantissa;
        this.tier = tier;
        Normalize();
    }

    public static BigNumber FromDouble(double v)
    {
        if (v <= 0) return Zero;
        int t = 0;
        while (v >= 1000.0)
        {
            v /= 1000.0;
            t++;
        }
        return new BigNumber(v, t);
    }

    public static BigNumber FromSuffix(double mantissa, int suffixIndex /*1=A, 2=B...*/)
        => new BigNumber(mantissa, Mathf.Max(0, suffixIndex));

    public void Normalize()
    {
        if (mantissa <= 0)
        {
            mantissa = 0; tier = 0; return;
        }
        while (mantissa >= 1000.0)
        {
            mantissa /= 1000.0;
            tier++;
        }
        while (mantissa > 0 && mantissa < 1.0 && tier > 0)
        {
            mantissa *= 1000.0;
            tier--;
        }
    }

    public static BigNumber operator +(BigNumber a, BigNumber b)
    {
        if (a.mantissa == 0) return b;
        if (b.mantissa == 0) return a;
        if (a.tier == b.tier)
            return new BigNumber(a.mantissa + b.mantissa, a.tier);

        // Add the smaller into the larger
        BigNumber hi = a.tier > b.tier ? a : b;
        BigNumber lo = a.tier > b.tier ? b : a;
        int diff = hi.tier - lo.tier;

        if (diff > 16) // too small to matter
            return hi;

        double scaled = lo.mantissa / Math.Pow(1000.0, diff);
        return new BigNumber(hi.mantissa + scaled, hi.tier);
    }

    public static BigNumber operator -(BigNumber a, BigNumber b)
    {
        if (b.mantissa == 0) return a;
        if (a.tier == b.tier)
            return new BigNumber(a.mantissa - b.mantissa, a.tier);

        BigNumber hi = a.tier > b.tier ? a : b;
        BigNumber lo = a.tier > b.tier ? b : a;
        int diff = hi.tier - lo.tier;

        if (a.tier < b.tier) // result negative -> clamp to zero for currencies
            return Zero;

        double scaled = lo.mantissa / Math.Pow(1000.0, diff);
        return new BigNumber(hi.mantissa - scaled, hi.tier);
    }

    public static BigNumber operator *(BigNumber a, BigNumber b)
        => new BigNumber(a.mantissa * b.mantissa, a.tier + b.tier);

    public static BigNumber operator *(BigNumber a, double m)
        => new BigNumber(a.mantissa * m, a.tier);

    public static BigNumber operator /(BigNumber a, double d)
        => d <= 0 ? a : new BigNumber(a.mantissa / d, a.tier);

    public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;
    public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
    public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
    public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;

    public int CompareTo(BigNumber other)
    {
        if (tier != other.tier) return tier.CompareTo(other.tier);
        return mantissa.CompareTo(other.mantissa);
    }

    public override string ToString()
    {
        if (mantissa == 0) return "0";
        if (tier == 0)
        {
            if (mantissa >= 100) return Math.Round(mantissa).ToString("0");
            if (mantissa >= 10) return mantissa.ToString("0.0##");
            return mantissa.ToString("0.###");
        }
        return $"{mantissa:0.###}{SuffixFromTier(tier)}";
    }

    public static string SuffixFromTier(int t)
    {
        if (t <= 0) return "";
        // 1->A, 2->B... 26->Z, 27->AA...
        t--; // zero-based
        string s = "";
        while (t >= 0)
        {
            s = (char)('A' + (t % 26)) + s;
            t = (t / 26) - 1;
        }
        return s;
    }
}
