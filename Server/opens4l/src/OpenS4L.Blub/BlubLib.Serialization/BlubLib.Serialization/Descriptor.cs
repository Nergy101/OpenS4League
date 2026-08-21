using System;
using System.Collections.Generic;
using System.Reflection;

namespace OpenS4L.Blub.Serialization;

internal class Descriptor
{
	private readonly object _mutex = new object();

	private Queue<Descriptor> _stack;

	public Type Type { get; set; }

	public Descriptor Parent { get; set; }

	public SortedList<uint, MemberDescriptor> Members { get; set; }

	public IList<MethodInfo> BeforeSerializeMethods { get; set; }

	public IList<MethodInfo> AfterSerializeMethods { get; set; }

	public IList<MethodInfo> BeforeDeserializeMethods { get; set; }

	public IList<MethodInfo> AfterDeserializeMethods { get; set; }

	public ISerializer Serializer { get; set; }

	public ISerializerCompiler Compiler { get; set; }

	public IEnumerable<Descriptor> GetTree()
	{
		lock (_mutex)
		{
			if (_stack == null)
			{
				_stack = new Queue<Descriptor>();
				AddRecursive(this);
			}
			return _stack;
		}
	}

	private void AddRecursive(Descriptor descriptor)
	{
		if (descriptor.Parent != null)
		{
			AddRecursive(descriptor.Parent);
		}
		_stack.Enqueue(descriptor);
	}
}
