using System;

namespace OpenS4L.Blub;

public static class Utilities
{
	private const double Terabyte = 1099511627776.0;

	private const double Gigabyte = 1073741824.0;

	private const double Megabyte = 1048576.0;

	private const double Kilobyte = 1024.0;

	public static bool IsMono { get; }

	public static OperatingSystem OperatingSystem { get; }

	static Utilities()
	{
		switch (Environment.OSVersion.Platform)
		{
		case PlatformID.Win32NT:
			switch (Environment.OSVersion.Version.Major)
			{
			case 5:
				switch (Environment.OSVersion.Version.Minor)
				{
				case 0:
					OperatingSystem = OperatingSystem.Win2000;
					break;
				case 1:
					OperatingSystem = OperatingSystem.WinXP;
					break;
				case 2:
					OperatingSystem = OperatingSystem.Win2003;
					break;
				default:
					OperatingSystem = OperatingSystem.Unknown;
					break;
				}
				break;
			case 6:
				switch (Environment.OSVersion.Version.Minor)
				{
				case 0:
					OperatingSystem = OperatingSystem.WinVista;
					break;
				case 1:
					OperatingSystem = OperatingSystem.Win7;
					break;
				case 2:
					OperatingSystem = OperatingSystem.Win8;
					break;
				case 3:
					OperatingSystem = OperatingSystem.Win81;
					break;
				default:
					OperatingSystem = OperatingSystem.Unknown;
					break;
				}
				break;
			case 10:
				OperatingSystem = OperatingSystem.Win10;
				break;
			default:
				OperatingSystem = OperatingSystem.Unknown;
				break;
			}
			break;
		case PlatformID.MacOSX:
			OperatingSystem = OperatingSystem.MacOSX;
			break;
		case PlatformID.Unix:
			OperatingSystem = OperatingSystem.Unix;
			break;
		default:
			OperatingSystem = OperatingSystem.Unknown;
			break;
		}
		IsMono = Type.GetType("Mono.Runtime") != null;
	}

	internal static string ToFormattedSize(ulong value)
	{
		string arg;
		double num;
		if ((double)value >= 1099511627776.0)
		{
			arg = "TB";
			num = 1099511627776.0;
		}
		else if ((double)value >= 1073741824.0)
		{
			arg = "GB";
			num = 1073741824.0;
		}
		else if ((double)value >= 1048576.0)
		{
			arg = "MB";
			num = 1048576.0;
		}
		else if ((double)value >= 1024.0)
		{
			arg = "KB";
			num = 1024.0;
		}
		else
		{
			arg = "B";
			num = 1.0;
		}
		double num2 = (double)value / num;
		return $"{num2:0.##} {arg}";
	}

	internal static string ToFormattedSize(long value)
	{
		string arg;
		double num;
		if ((double)value >= 1099511627776.0)
		{
			arg = "TB";
			num = 1099511627776.0;
		}
		else if ((double)value >= 1073741824.0)
		{
			arg = "GB";
			num = 1073741824.0;
		}
		else if ((double)value >= 1048576.0)
		{
			arg = "MB";
			num = 1048576.0;
		}
		else if ((double)value >= 1024.0)
		{
			arg = "KB";
			num = 1024.0;
		}
		else
		{
			arg = "B";
			num = 1.0;
		}
		double num2 = (double)value / num;
		return $"{num2:0.##} {arg}";
	}
}
