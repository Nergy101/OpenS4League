using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenS4L.Blub.Threading.Tasks;

namespace OpenS4L.Blub.Net.Sockets;

public static class SocketExtensions
{
	public static Task ConnectTaskAsync(this Socket @this, IPEndPoint endPoint)
	{
		return Task.Factory.FromAsync(@this.BeginConnect, @this.EndConnect, endPoint, null);
	}

	public static Task<Socket> AcceptTaskAsync(this Socket @this)
	{
		return Task<Socket>.Factory.FromAsync(@this.BeginAccept, @this.EndAccept, null);
	}

	public static Task<int> SendTaskAsync(this Socket @this, byte[] buffer, int offset, int size, SocketFlags socketFlags)
	{
		return Task<int>.Factory.FromAsync(@this.BeginSend, @this.EndSend, buffer, offset, size, socketFlags);
	}

	public static Task<int> SendTaskAsync(this Socket @this, byte[] buffer, int size, SocketFlags socketFlags)
	{
		return @this.SendTaskAsync(buffer, 0, size, socketFlags);
	}

	public static Task<int> SendTaskAsync(this Socket @this, byte[] buffer, SocketFlags socketFlags)
	{
		return @this.SendTaskAsync(buffer, 0, buffer.Length, socketFlags);
	}

	public static Task<int> SendTaskAsync(this Socket @this, byte[] buffer)
	{
		return @this.SendTaskAsync(buffer, 0, buffer.Length, SocketFlags.None);
	}

	public static Task<int> ReceiveTaskAsync(this Socket @this, byte[] buffer, int offset, int size, SocketFlags socketFlags)
	{
		return Task<int>.Factory.FromAsync(@this.BeginReceive, @this.EndReceive, buffer, offset, size, socketFlags);
	}

	public static Task<int> ReceiveTaskAsync(this Socket @this, byte[] buffer, int size, SocketFlags socketFlags)
	{
		return @this.ReceiveTaskAsync(buffer, 0, size, socketFlags);
	}

	public static Task<int> ReceiveTaskAsync(this Socket @this, byte[] buffer, SocketFlags socketFlags)
	{
		return @this.ReceiveTaskAsync(buffer, 0, buffer.Length, socketFlags);
	}

	public static Task<int> ReceiveTaskAsync(this Socket @this, byte[] buffer)
	{
		return @this.ReceiveTaskAsync(buffer, 0, buffer.Length, SocketFlags.None);
	}

	public static Task<int> SendToTaskAsync(this Socket @this, byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP)
	{
		return Task<int>.Factory.FromAsync(@this.BeginSendTo, @this.EndSendTo, buffer, offset, size, socketFlags, remoteEP);
	}

	public static Task<int> SendToTaskAsync(this Socket @this, byte[] buffer, int size, SocketFlags socketFlags, EndPoint remoteEP)
	{
		return @this.SendToTaskAsync(buffer, 0, size, socketFlags, remoteEP);
	}

	public static Task<int> SendToTaskAsync(this Socket @this, byte[] buffer, SocketFlags socketFlags, EndPoint remoteEP)
	{
		return @this.SendToTaskAsync(buffer, 0, buffer.Length, socketFlags, remoteEP);
	}

	public static Task<int> SendToTaskAsync(this Socket @this, byte[] buffer, EndPoint remoteEP)
	{
		return @this.SendToTaskAsync(buffer, 0, buffer.Length, SocketFlags.None, remoteEP);
	}

	public static Task<UdpReceiveResult> ReceiveFromTaskAsync(this Socket @this, byte[] buffer, int offset, int size, SocketFlags socketFlags)
	{
		EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
		TaskCompletionSource<UdpReceiveResult> taskCompletionSource = new TaskCompletionSource<UdpReceiveResult>(new
		{
			Socket = @this,
			Buffer = buffer,
			EndPoint = remoteEP
		});
		@this.BeginReceiveFrom(buffer, offset, size, socketFlags, ref remoteEP, delegate(IAsyncResult a)
		{
			TaskCompletionSource<UdpReceiveResult> taskCompletionSource2 = (TaskCompletionSource<UdpReceiveResult>)a.AsyncState;
			dynamic asyncState = taskCompletionSource2.Task.AsyncState;
			Socket socket = asyncState.Socket;
			byte[] sourceArray = asyncState.Buffer;
			EndPoint endPoint = asyncState.EndPoint;
			try
			{
				int num = socket.EndReceiveFrom(a, ref endPoint);
				byte[] array = new byte[num];
				Array.Copy(sourceArray, 0, array, 0, num);
				taskCompletionSource2.TrySetResult(new UdpReceiveResult(array, (IPEndPoint)endPoint));
			}
			catch (Exception exception)
			{
				taskCompletionSource2.TrySetException(exception);
			}
		}, taskCompletionSource);
		return taskCompletionSource.Task;
	}

	public static Task<UdpReceiveResult> ReceiveFromTaskAsync(this Socket @this, byte[] buffer, int size, SocketFlags socketFlags)
	{
		return @this.ReceiveFromTaskAsync(buffer, 0, size, socketFlags);
	}

	public static Task<UdpReceiveResult> ReceiveFromTaskAsync(this Socket @this, byte[] buffer, SocketFlags socketFlags)
	{
		return @this.ReceiveFromTaskAsync(buffer, 0, buffer.Length, socketFlags);
	}

	public static Task<UdpReceiveResult> ReceiveFromTaskAsync(this Socket @this, byte[] buffer)
	{
		return @this.ReceiveFromTaskAsync(buffer, 0, buffer.Length, SocketFlags.None);
	}
}
