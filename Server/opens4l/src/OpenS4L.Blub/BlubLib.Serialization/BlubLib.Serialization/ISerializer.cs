using System;
using System.IO;

namespace OpenS4L.Blub.Serialization;

public interface ISerializer
{
	bool CanHandle(Type type);
}
public interface ISerializer<T> : ISerializer
{
	void Serialize(BlubSerializer blubSerializer, BinaryWriter writer, T value);

	T Deserialize(BlubSerializer blubSerializer, BinaryReader reader);
}
