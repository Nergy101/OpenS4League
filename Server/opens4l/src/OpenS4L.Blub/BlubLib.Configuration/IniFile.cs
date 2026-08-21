using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OpenS4L.Blub.IO;

namespace OpenS4L.Blub.Configuration;

public class IniFile : DynamicObject, IReadOnlyDictionary<string, IniSection>, IEnumerable<KeyValuePair<string, IniSection>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, IniSection>>
{
	private static readonly Regex s_sectionRegex = new Regex("^\\[([a-zA-Z0-9_-]+)\\]");

	private static readonly Regex s_valueRegex = new Regex("^([a-zA-Z0-9.,_-]+)=(.*)");

	private readonly ConcurrentDictionary<string, IniSection> _dictionary = new ConcurrentDictionary<string, IniSection>();

	private string _filePath;

	public int Count => _dictionary.Count;

	public IEnumerable<string> Keys => _dictionary.Keys;

	public IEnumerable<IniSection> Values => _dictionary.Values;

	public IniSection this[string key] => GetSection(key);

	public static IniFile Load(string fileName)
	{
		IniFile iniFile;
		if (File.Exists(fileName))
		{
			using FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			iniFile = Load(stream);
		}
		else
		{
			iniFile = new IniFile();
		}
		iniFile._filePath = fileName;
		return iniFile;
	}

	public static IniFile Load(Stream stream)
	{
		return Load(stream, Encoding.Default);
	}

	public static IniFile Load(Stream stream, Encoding encoding)
	{
		IniFile iniFile = new IniFile();
		using StreamReader streamReader = new StreamReader(new NonClosingStream(stream), encoding);
		string text = null;
		string text2;
		while ((text2 = streamReader.ReadLine()) != null)
		{
			if (!string.IsNullOrWhiteSpace(text2))
			{
				if (s_sectionRegex.IsMatch(text2))
				{
					string value = s_sectionRegex.Match(text2).Groups[1].Value;
					text = value;
					iniFile.GetSection(value);
				}
				else if (s_valueRegex.IsMatch(text2) && text != null)
				{
					Match match = s_valueRegex.Match(text2);
					iniFile[text][match.Groups[1].Value] = match.Groups[2].Value;
				}
			}
		}
		return iniFile;
	}

	public void Save(Stream stream, Encoding encoding = null, bool sort = false)
	{
		StringBuilder stringBuilder = new StringBuilder();
		if (sort)
		{
			List<string> list = Keys.ToList();
			list.Sort();
			foreach (string item in list)
			{
				stringBuilder.AppendLine("[" + item + "]");
				List<string> list2 = this[item].Keys.ToList();
				list2.Sort();
				foreach (string item2 in list2)
				{
					stringBuilder.AppendLine(item2 + "=" + this[item][item2]);
				}
				stringBuilder.AppendLine();
			}
		}
		else
		{
			using IEnumerator<KeyValuePair<string, IniSection>> enumerator3 = GetEnumerator();
			while (enumerator3.MoveNext())
			{
				KeyValuePair<string, IniSection> current3 = enumerator3.Current;
				stringBuilder.AppendLine("[" + current3.Key + "]");
				foreach (KeyValuePair<string, IniValue> item3 in current3.Value)
				{
					stringBuilder.AppendLine(item3.Key + "=" + item3.Value);
				}
				stringBuilder.AppendLine("");
			}
		}
		using StreamWriter streamWriter = new StreamWriter(stream, encoding ?? Encoding.Default, 1024, leaveOpen: true);
		streamWriter.Write(stringBuilder);
	}

	public void Save(string fileName, Encoding encoding = null, bool sort = false)
	{
		using FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
		Save(stream, encoding, sort);
	}

	public void Save(Encoding encoding = null, bool sort = false)
	{
		if (!string.IsNullOrWhiteSpace(_filePath))
		{
			Save(_filePath, encoding, sort);
		}
	}

	public IniSection GetSection(string key)
	{
		if (!_dictionary.TryGetValue(key, out var value))
		{
			value = new IniSection(key);
			_dictionary.TryAdd(key, value);
		}
		return value;
	}

	public bool ContainsKey(string key)
	{
		return _dictionary.ContainsKey(key);
	}

	public bool TryGetValue(string key, out IniSection value)
	{
		return _dictionary.TryGetValue(key, out value);
	}

	public override bool TryGetMember(GetMemberBinder binder, out object result)
	{
		result = GetSection(binder.Name);
		return true;
	}

	public IEnumerator<KeyValuePair<string, IniSection>> GetEnumerator()
	{
		return _dictionary.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
