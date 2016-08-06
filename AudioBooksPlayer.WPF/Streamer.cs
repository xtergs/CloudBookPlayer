using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Streaming;
using System.Linq;

namespace AudioBooksPlayer.WPF
{
	
	abstract class BaseStreamer : IStreamer
	{
		private volatile StreamingStatus _status;
		public bool CanBeCached { get; } = true;
		public int transferePerMs { get; set; } = 50;
		public byte VersionProtocol { get; set; } = 1;
		public int HeaderLength { get; } = 100;
		public virtual int BufferLength { get; set; } = 1024*4*4;

		protected static void FillHeader(byte[] buffer, int version, long order, Guid id, int len)
		{
			if (version == 1)
			{
				var byteorder = BitConverter.GetBytes(order);
				for (int i = 0; i < byteorder.Length; i++)
					buffer[i + 1] = byteorder[i];
				var byteGuid = id.ToByteArray();
				for (int i = 0; i < byteGuid.Length; i++)
					buffer[i + 9] = byteGuid[i];
				byteorder = BitConverter.GetBytes(len);
				for (int i = 0; i < byteorder.Length; i++)
					buffer[i + 25] = byteorder[i];
			}
		}

		protected static StreamFrame FromeHeader(byte[] buffer)
		{
			var result = new StreamFrame();
			if (buffer[0] == 1)
			{
				result.Order = BitConverter.ToInt64(buffer, 1);
				result.id = new Guid(buffer.Skip(9).Take(16).ToArray());
				result.Length = BitConverter.ToInt32(buffer, 25);
				result.Offsset = 100;
			}
			result.buffer = buffer;
			return result;
		}

		public StreamingStatus Status
		{
			get { return _status; }
			private set { _status = value; }
		}

		public void Pause()
		{
			Status = StreamingStatus.Pause;
		}

		public void Cancel()
		{
			Status = StreamingStatus.Cancel;
		}

		public abstract string GetConnectionInfo();

		public bool Resume()
		{
			if (Status != StreamingStatus.Cancel)
			{
				Status = StreamingStatus.Stream;
				return true;
			}
			return false;
		}

		public abstract Task StartSendStream(Stream stream, string connectionInfo, IPEndPoint endpoint, IProgress<StreamProgress> repoerter);

		public abstract Task StartReceiveStream(string connectionInfo, IPEndPoint endpoint,
			IProgress<ReceivmentProgress> reporter, ReceiveCallback receivmentAction);

		public bool Init(string connectionInfo)
		{

			return true;
		}

		public event EventHandler<StreamStatus> StreamStatusChanged;
		protected virtual void OnStreamStatusChanged(StreamStatus e)
		{
			StreamStatusChanged?.Invoke(this, e);
		}
	}


	class PipeStreamer : BaseStreamer
	{
		private readonly NamedPipeServerStream pipe;
		private readonly string name;
		public PipeStreamer()
		{
			name = Guid.NewGuid().ToString();
			pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, transmissionMode: PipeTransmissionMode.Message);
		}
		public override string GetConnectionInfo()
		{
			return name;
		}
		public override async Task StartSendStream(Stream stream, string connectionInfo, IPEndPoint endpoint, IProgress<StreamProgress> repoerter)
		{
			await pipe.WaitForConnectionAsync().ConfigureAwait(false);
			Guid operationId = Guid.NewGuid();
			OnStreamStatusChanged(new StreamStatus() { operationId = operationId, Status = StreamingStatus.Stream });
			try
			{
				byte[] sendBuffrer = new byte[BufferLength + HeaderLength];
				sendBuffrer[0] = VersionProtocol;
				var Id = Guid.NewGuid();

				int order = 0;
				var prs = new StreamProgress() { OperationId = operationId };

				string bytes;
				//byte[] b;
				prs.Length = stream.Length;
				while (true)
				{
					while (stream.CanRead && stream.Position < stream.Length)
					{
						var readed = stream.Read(sendBuffrer, 100, BufferLength);
						FillHeader(sendBuffrer, VersionProtocol, order, Id, readed);
						await pipe.WriteAsync(sendBuffrer, 0, readed + HeaderLength);
						pipe.WaitForPipeDrain();
						await Task.Delay(transferePerMs);

						while (Status == StreamingStatus.Pause)
						await Task.Delay(new TimeSpan(0, 0, 0, 1));
						if (Status == StreamingStatus.Cancel)
							return;
						prs.Position = stream.Position;
						repoerter.Report(prs);
						order++;
					}
					break;
				}
				FillHeader(sendBuffrer, VersionProtocol, order, Id, 0);
				await pipe.WriteAsync(sendBuffrer, 0, HeaderLength).ConfigureAwait(false);
			}
			catch
			{
				pipe.Dispose();
				throw;
			}
			finally
			{
				OnStreamStatusChanged(new StreamStatus() { operationId = operationId, Status = StreamingStatus.Cancel });
			}
		}

		public override async Task StartReceiveStream(string connectionInfo, IPEndPoint endpoint, IProgress<ReceivmentProgress> reporter, ReceiveCallback receivmentAction)
		{
			NamedPipeClientStream client = new NamedPipeClientStream(".",connectionInfo);
			await client.ConnectAsync(5000);
			byte[] buffer = new byte[BufferLength + HeaderLength];
			while (client.CanRead)
			{
				await client.ReadAsync(buffer, 0, buffer.Length);
				var frame = FromeHeader(buffer);
				await receivmentAction(frame, reporter);
				if (frame.Length == 0)
					return;
			}
		}
	}



	class UDPStreamer : BaseStreamer, IDisposable
	{
		
		private readonly UdpClient client;

		public UDPStreamer()
		{
			client = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
		}

		public override string GetConnectionInfo()
		{
			return $"{((IPEndPoint) client.Client.LocalEndPoint).Address}:{((IPEndPoint) client.Client.LocalEndPoint).Port}";
		}

		public override Task StartSendStream(Stream stream, string connectionInfo, IPEndPoint endpoin, IProgress<StreamProgress> repoerter)
		{
			//var parts = connectionInfo.Split(':');
			//IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
			return StartSendStream(client, stream, endpoin, repoerter);
		}

		public override async Task StartReceiveStream(string connectionInfo, IPEndPoint endpoint, IProgress<ReceivmentProgress> reporter, ReceiveCallback receivmentAction)
		{
			UdpClient cl = new UdpClient();
			cl.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			cl.Client.ReceiveTimeout = 5000;
			cl.Client.ReceiveBufferSize = int.MaxValue;
			while (true)
			{
				var res = await cl.ReceiveAsync().ConfigureAwait(false);
				var frame = FromeHeader(res.Buffer);
				await receivmentAction(frame, reporter);
				if (frame.Length == 0)
					return;
			}
		}

		private async Task StartSendStream(UdpClient client, Stream stream, IPEndPoint endpoint, IProgress<StreamProgress> reporter)
		{
			Guid operationId = Guid.NewGuid();
			OnStreamStatusChanged(new StreamStatus() { operationId = operationId, Status = StreamingStatus.Stream });
			try
			{

				byte[] sendBuffrer = new byte[BufferLength + HeaderLength];
				sendBuffrer[0] = VersionProtocol;
				var id = Guid.NewGuid();

				int order = 0;
				var prs = new StreamProgress() { OperationId = operationId };
				client.EnableBroadcast = true;
				prs.Length = stream.Length;
				while (true)
				{
					while (stream.CanRead && stream.Position < stream.Length)
					{
						var readed = stream.Read(sendBuffrer, HeaderLength, BufferLength);
						FillHeader(sendBuffrer, VersionProtocol, order, id, readed);
						await client.SendAsync(sendBuffrer, readed + HeaderLength, endpoint);
						await Task.Delay(transferePerMs);

						while (Status == StreamingStatus.Pause)
							await Task.Delay(new TimeSpan(0, 0, 0, 1));
						if (Status == StreamingStatus.Cancel)
							return;
						prs.Position = stream.Position;
						reporter.Report(prs);
						order++;
					}
					break;
				}
				FillHeader(sendBuffrer, VersionProtocol, order, id, 0);
				await client.SendAsync(sendBuffrer, HeaderLength, endpoint);
			}
			catch
			{
				client.Dispose();
				throw;
			}
			finally
			{
				OnStreamStatusChanged(new StreamStatus() { operationId = operationId, Status = StreamingStatus.Cancel });
			}
		}
		public Task StartSendStream(Stream stream, IPEndPoint endpoint, IProgress<StreamProgress> reporter,
			int transferePerMs = 50)
		{
			UdpClient client = new UdpClient();
			using (client)
			{
				client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				return StartSendStream(client, stream, endpoint, reporter);
			}
		}

		public void Dispose()
		{
			client.Close();
			client.Dispose();
		}
	}
}
