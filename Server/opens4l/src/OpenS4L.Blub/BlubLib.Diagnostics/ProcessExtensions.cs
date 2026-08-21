using System.Diagnostics;
using System.Threading.Tasks;
using OpenS4L.Blub.Threading.Tasks;

namespace OpenS4L.Blub.Diagnostics;

public static class ProcessExtensions
{
	public static Task WaitForExitAsync(this Process @this)
	{
		@this.EnableRaisingEvents = true;
		System.Threading.Tasks.TaskCompletionSource tcs = new System.Threading.Tasks.TaskCompletionSource();
		@this.Exited += delegate
		{
			tcs.TrySetResult();
		};
		return tcs.Task;
	}
}
