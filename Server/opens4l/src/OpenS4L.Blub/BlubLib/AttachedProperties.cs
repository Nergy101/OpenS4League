using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using OpenS4L.Blub.Collections.Concurrent;

namespace OpenS4L.Blub;

public class AttachedProperties : DynamicObject, IDictionary<string, object>, ICollection<KeyValuePair<string, object>>, IEnumerable<KeyValuePair<string, object>>, IEnumerable
{
	private readonly ConcurrentDictionary<string, object> _properties = new ConcurrentDictionary<string, object>();

	public int Count => _properties.Count;

	bool ICollection<KeyValuePair<string, object>>.IsReadOnly => false;

	public ICollection<string> Keys => _properties.Keys;

	public ICollection<object> Values => _properties.Values;

	public object this[string key]
	{
		get
		{
			return _properties[key];
		}
		set
		{
			_properties[key] = value;
		}
	}

	public override bool TryGetMember(GetMemberBinder binder, out object result)
	{
		return _properties.TryGetValue(binder.Name, out result);
	}

	public override bool TrySetMember(SetMemberBinder binder, object value)
	{
		_properties[binder.Name] = value;
		return true;
	}

	public void Add(KeyValuePair<string, object> item)
	{
		((ICollection<KeyValuePair<string, object>>)_properties).Add(item);
	}

	public void Clear()
	{
		_properties.Clear();
	}

	public bool Contains(KeyValuePair<string, object> item)
	{
		return ((ICollection<KeyValuePair<string, object>>)_properties).Contains(item);
	}

	public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
	{
		((ICollection<KeyValuePair<string, object>>)_properties).CopyTo(array, arrayIndex);
	}

	public bool Remove(KeyValuePair<string, object> item)
	{
		return ((ICollection<KeyValuePair<string, object>>)_properties).Remove(item);
	}

	public bool ContainsKey(string key)
	{
		return _properties.ContainsKey(key);
	}

	public void Add(string key, object value)
	{
		((IDictionary<string, object>)_properties).Add(key, value);
	}

	public bool Remove(string key)
	{
		return _properties.Remove(key);
	}

	public bool TryGetValue(string key, out object value)
	{
		return _properties.TryGetValue(key, out value);
	}

	public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
	{
		return _properties.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
