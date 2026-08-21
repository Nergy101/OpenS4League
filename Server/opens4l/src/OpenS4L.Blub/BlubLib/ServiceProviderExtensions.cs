using System;

namespace OpenS4L.Blub;

public static class ServiceProviderExtensions
{
	public static T GetService<T>(this IServiceProvider @this)
	{
		return (T)@this.GetService(typeof(T));
	}
}
