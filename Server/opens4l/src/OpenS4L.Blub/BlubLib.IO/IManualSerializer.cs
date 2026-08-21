using System.IO;

namespace OpenS4L.Blub.IO;

public interface IManualSerializer
{
	void Serialize(Stream stream);

	void Deserialize(Stream stream);
}
