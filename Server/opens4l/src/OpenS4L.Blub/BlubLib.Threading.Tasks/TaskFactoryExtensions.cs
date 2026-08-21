using System;
using System.Threading.Tasks;

namespace OpenS4L.Blub.Threading.Tasks;

public static class TaskFactoryExtensions
{
	public static Task<TResult> FromAsync<TArg1, TArg2, TArg3, TArg4, TResult>(this TaskFactory<TResult> @this, Func<TArg1, TArg2, TArg3, TArg4, AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, object state)
	{
		TaskCompletionSource<TResult> taskCompletionSource = new TaskCompletionSource<TResult>();
		beginMethod(arg1, arg2, arg3, arg4, EndCallback(endMethod, taskCompletionSource), state);
		return taskCompletionSource.Task;
	}

	public static Task<TResult> FromAsync<TArg1, TArg2, TArg3, TArg4, TResult>(this TaskFactory<TResult> @this, Func<TArg1, TArg2, TArg3, TArg4, AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
	{
		TaskCompletionSource<TResult> taskCompletionSource = new TaskCompletionSource<TResult>();
		beginMethod(arg1, arg2, arg3, arg4, EndCallback(endMethod, taskCompletionSource), null);
		return taskCompletionSource.Task;
	}

	public static Task<TResult> FromAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TResult>(this TaskFactory<TResult> @this, Func<TArg1, TArg2, TArg3, TArg4, TArg5, AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, object state)
	{
		TaskCompletionSource<TResult> taskCompletionSource = new TaskCompletionSource<TResult>();
		beginMethod(arg1, arg2, arg3, arg4, arg5, EndCallback(endMethod, taskCompletionSource), state);
		return taskCompletionSource.Task;
	}

	public static Task<TResult> FromAsync<TArg1, TArg2, TArg3, TArg4, TArg5, TResult>(this TaskFactory<TResult> @this, Func<TArg1, TArg2, TArg3, TArg4, TArg5, AsyncCallback, object, IAsyncResult> beginMethod, Func<IAsyncResult, TResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
	{
		TaskCompletionSource<TResult> taskCompletionSource = new TaskCompletionSource<TResult>();
		beginMethod(arg1, arg2, arg3, arg4, arg5, EndCallback(endMethod, taskCompletionSource), null);
		return taskCompletionSource.Task;
	}

	public static Task FromAsync<TArg1, TArg2, TArg3, TArg4>(this TaskFactory @this, Func<TArg1, TArg2, TArg3, TArg4, AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, object state)
	{
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
		beginMethod(arg1, arg2, arg3, arg4, EndCallback(endMethod, taskCompletionSource), state);
		return taskCompletionSource.Task;
	}

	public static Task FromAsync<TArg1, TArg2, TArg3, TArg4>(this TaskFactory @this, Func<TArg1, TArg2, TArg3, TArg4, AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
	{
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
		beginMethod(arg1, arg2, arg3, arg4, EndCallback(endMethod, taskCompletionSource), null);
		return taskCompletionSource.Task;
	}

	public static Task FromAsync<TArg1, TArg2, TArg3, TArg4, TArg5>(this TaskFactory @this, Func<TArg1, TArg2, TArg3, TArg4, TArg5, AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, object state)
	{
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
		beginMethod(arg1, arg2, arg3, arg4, arg5, EndCallback(endMethod, taskCompletionSource), state);
		return taskCompletionSource.Task;
	}

	public static Task FromAsync<TArg1, TArg2, TArg3, TArg4, TArg5>(this TaskFactory @this, Func<TArg1, TArg2, TArg3, TArg4, TArg5, AsyncCallback, object, IAsyncResult> beginMethod, Action<IAsyncResult> endMethod, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
	{
		TaskCompletionSource taskCompletionSource = new TaskCompletionSource();
		beginMethod(arg1, arg2, arg3, arg4, arg5, EndCallback(endMethod, taskCompletionSource), null);
		return taskCompletionSource.Task;
	}

	private static AsyncCallback EndCallback<TResult>(Func<IAsyncResult, TResult> endMethod, TaskCompletionSource<TResult> tcs)
	{
		return delegate(IAsyncResult ar)
		{
			try
			{
				tcs.TrySetResult(endMethod(ar));
			}
			catch (OperationCanceledException)
			{
				tcs.TrySetCanceled();
			}
			catch (Exception exception)
			{
				tcs.TrySetException(exception);
			}
		};
	}

	private static AsyncCallback EndCallback(Action<IAsyncResult> endMethod, TaskCompletionSource tcs)
	{
		return delegate(IAsyncResult ar)
		{
			try
			{
				endMethod(ar);
				tcs.TrySetResult();
			}
			catch (OperationCanceledException)
			{
				tcs.TrySetCanceled();
			}
			catch (Exception exception)
			{
				tcs.TrySetException(exception);
			}
		};
	}
}
