using Sigil.NonGeneric;

namespace OpenS4L.Blub.Serialization;

public class CompilerContext
{
	public BlubSerializer BlubSerializer { get; }

	public Emit Emit { get; }

	internal SerializerGenerator Generator { get; }

	internal CompilerContext(BlubSerializer blubSerializer, Emit emit, SerializerGenerator generator)
	{
		BlubSerializer = blubSerializer;
		Emit = emit;
		Generator = generator;
	}
}
