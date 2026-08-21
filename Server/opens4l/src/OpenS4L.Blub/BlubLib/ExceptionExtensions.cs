using System;
using System.Runtime.ExceptionServices;

namespace OpenS4L.Blub;

public static class ExceptionExtensions
{
	public static Exception Rethrow(this Exception @this)
	{
		ExceptionDispatchInfo.Capture(@this).Throw();
		return null;
	}
}
