using System;
using System.Reflection;
using System.Reflection.Emit;

namespace OpenS4L.Blub.Serialization;

internal static class TypeBuilderFactory
{
	private static readonly AssemblyBuilder s_assemblyBuilder;

	private static readonly ModuleBuilder s_moduleBuilder;

	static TypeBuilderFactory()
	{
		s_assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("OpenS4L.Blub.Serialization.SerializerAssembly"), AssemblyBuilderAccess.Run);
		s_moduleBuilder = s_assemblyBuilder.DefineDynamicModule(string.Format("{0}.dll", "OpenS4L.Blub.Serialization.SerializerAssembly"));
		AppDomain.CurrentDomain.AssemblyResolve += (object s, ResolveEventArgs e) => (!e.Name.StartsWith("OpenS4L.Blub.Serialization.SerializerAssembly", StringComparison.Ordinal)) ? null : s_assemblyBuilder;
	}

	public static TypeBuilder Create(string name)
	{
		return s_moduleBuilder.DefineType($"{name}-{Guid.NewGuid()}", TypeAttributes.Public);
	}
}
