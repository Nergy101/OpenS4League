using System.Reflection.Emit;

namespace OpenS4L.Blub.Serialization;

internal class GeneratedField
{
	public FieldBuilder FieldBuilder { get; }

	public ISerializer Serializer { get; }

	public GeneratedField(FieldBuilder fieldBuilder, ISerializer serializer)
	{
		FieldBuilder = fieldBuilder;
		Serializer = serializer;
	}
}
