using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenS4L.Blub;

public static class StringExtensions
{
	public static string[] GetArgs(this string @this)
	{
		List<string> list = new List<string>();
		using (StringReader stringReader = new StringReader(@this))
		{
			while (stringReader.Peek() != -1)
			{
				if (stringReader.Peek() == 32)
				{
					stringReader.Read();
				}
				StringBuilder stringBuilder = new StringBuilder();
				if (stringReader.Peek() == 34)
				{
					stringReader.Read();
					while (stringReader.Peek() != 34 && stringReader.Peek() != -1)
					{
						stringBuilder.Append((char)stringReader.Read());
					}
					stringReader.Read();
				}
				else
				{
					while (stringReader.Peek() != -1 && stringReader.Peek() != 32)
					{
						stringBuilder.Append((char)stringReader.Read());
					}
					stringReader.Read();
				}
				list.Add(stringBuilder.ToString());
			}
		}
		return list.ToArray();
	}

	public static bool Contains(this string @this, string value, StringComparison comparisonType)
	{
		return @this.IndexOf(value, comparisonType) != -1;
	}

	public static byte[] HexToArray(this string @this)
	{
		return (from x in Enumerable.Range(0, @this.Length / 2)
			select Convert.ToByte(@this.Substring(x * 2, 2), 16)).ToArray();
	}
}
