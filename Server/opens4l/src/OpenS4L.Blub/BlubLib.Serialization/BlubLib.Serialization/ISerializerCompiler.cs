using System;
using Sigil;

namespace OpenS4L.Blub.Serialization;

public interface ISerializerCompiler
{
	bool CanHandle(Type type);

	void EmitSerialize(CompilerContext ctx, Local value);

	void EmitDeserialize(CompilerContext ctx, Local value);
}
