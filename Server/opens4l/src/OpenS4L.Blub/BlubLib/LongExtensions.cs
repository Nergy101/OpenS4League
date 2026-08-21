namespace OpenS4L.Blub;

public static class LongExtensions
{
	public static string ToFormattedSize(this long @this)
	{
		return Utilities.ToFormattedSize(@this);
	}

	public static string ToFormattedSize(this ulong @this)
	{
		return Utilities.ToFormattedSize(@this);
	}
}
