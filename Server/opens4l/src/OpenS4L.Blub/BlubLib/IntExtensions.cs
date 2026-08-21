namespace OpenS4L.Blub;

public static class IntExtensions
{
	public static string ToFormattedSize(this int @this)
	{
		return Utilities.ToFormattedSize(@this);
	}

	public static string ToFormattedSize(this uint @this)
	{
		return Utilities.ToFormattedSize(@this);
	}
}
