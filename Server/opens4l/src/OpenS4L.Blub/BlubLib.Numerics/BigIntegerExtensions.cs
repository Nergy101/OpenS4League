using System.Numerics;

namespace OpenS4L.Blub.Numerics;

public static class BigIntegerExtensions
{
	public static BigInteger Abs(this BigInteger @this)
	{
		return BigInteger.Abs(@this);
	}

	public static BigInteger Add(this BigInteger @this, BigInteger right)
	{
		return BigInteger.Add(@this, right);
	}

	public static BigInteger Subtract(this BigInteger @this, BigInteger right)
	{
		return BigInteger.Subtract(@this, right);
	}

	public static BigInteger Multiply(this BigInteger @this, BigInteger right)
	{
		return BigInteger.Multiply(@this, right);
	}

	public static BigInteger Divide(this BigInteger @this, BigInteger divisor)
	{
		return BigInteger.Divide(@this, divisor);
	}

	public static BigInteger Remainder(this BigInteger @this, BigInteger divisor)
	{
		return BigInteger.Remainder(@this, divisor);
	}

	public static BigInteger DivRem(this BigInteger @this, BigInteger divisor, out BigInteger remainder)
	{
		return BigInteger.DivRem(@this, divisor, out remainder);
	}

	public static BigInteger Negate(this BigInteger @this)
	{
		return BigInteger.Negate(@this);
	}

	public static double Log(this BigInteger @this)
	{
		return BigInteger.Log(@this);
	}

	public static double Log(this BigInteger @this, double baseValue)
	{
		return BigInteger.Log(@this, baseValue);
	}

	public static double Log10(this BigInteger @this)
	{
		return BigInteger.Log10(@this);
	}

	public static BigInteger GreatestCommonDivisor(this BigInteger @this, BigInteger right)
	{
		return BigInteger.GreatestCommonDivisor(@this, right);
	}

	public static BigInteger Max(this BigInteger @this, BigInteger right)
	{
		return BigInteger.Max(@this, right);
	}

	public static BigInteger Min(this BigInteger @this, BigInteger right)
	{
		return BigInteger.Min(@this, right);
	}

	public static BigInteger ModPow(this BigInteger @this, BigInteger exponent, BigInteger modulus)
	{
		return BigInteger.ModPow(@this, exponent, modulus);
	}

	public static BigInteger Pow(this BigInteger @this, int exponent)
	{
		return BigInteger.Pow(@this, exponent);
	}
}
