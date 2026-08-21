using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;

namespace OpenS4L.Blub.Configuration;

public sealed class IniSection : DynamicObject, IReadOnlyDictionary<string, IniValue>, IEnumerable<KeyValuePair<string, IniValue>>, IEnumerable, IReadOnlyCollection<KeyValuePair<string, IniValue>>
{
	private readonly ConcurrentDictionary<string, IniValue> _dictionary = new ConcurrentDictionary<string, IniValue>();

	public string Name { get; }

	public int Count => _dictionary.Count;

	public IEnumerable<string> Keys => _dictionary.Keys;

	public IEnumerable<IniValue> Values => _dictionary.Values;

	public IniValue this[string key]
	{
		get
		{
			return GetValue(key);
		}
		set
		{
			SetValue(key, value);
		}
	}

	public IniSection(string name)
	{
		Name = name;
	}

	public IniValue GetValue(string key)
	{
		if (!_dictionary.TryGetValue(key, out var value))
		{
			value = new IniValue();
			_dictionary.TryAdd(key, value);
		}
		return value;
	}

	public void SetValue(string key, IniValue value)
	{
		_dictionary.AddOrUpdate(key, value, (string k, IniValue o) => value);
	}

	public bool ContainsKey(string key)
	{
		return _dictionary.ContainsKey(key);
	}

	public bool TryGetValue(string key, out IniValue value)
	{
		return _dictionary.TryGetValue(key, out value);
	}

	public override bool TryGetMember(GetMemberBinder binder, out object result)
	{
		result = GetValue(binder.Name);
		return true;
	}

	public override bool TrySetMember(SetMemberBinder binder, object value)
	{
		IniValue iniValue = value as IniValue;
		SetValue(binder.Name, iniValue ?? new IniValue(value.ToString()));
		return true;
	}

	public IEnumerator<KeyValuePair<string, IniValue>> GetEnumerator()
	{
		return _dictionary.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public override string ToString()
	{
		return Name;
	}
}
