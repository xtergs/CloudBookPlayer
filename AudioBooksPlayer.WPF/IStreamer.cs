using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using AudioBooksPlayer.WPF.Streaming;

namespace AudioBooksPlayer.WPF
{
	public delegate Task ReceiveCallback(StreamFrame frame, IProgress<ReceivmentProgress> rp);
	public struct StreamFrame
	{
		public long Order;
		public int Length;
		public Guid id;
		public int Offsset;
		public byte[] buffer;
	}
	public interface IStreamer
	{
		bool CanBeCached { get; }
		StreamingStatus Status { get; }
		int transferePerMs { get; set; }
		byte VersionProtocol { get; set; }

		event EventHandler<StreamStatus> StreamStatusChanged;

		void Cancel();
		string GetConnectionInfo();
		bool Init(string connectionInfo);
		void Pause();
		bool Resume();
		Task StartSendStream(Stream stream, BookStreamer.CommandData connectionInfo, IPEndPoint endpoint, IProgress<StreamProgress> repoerter);

		Task StartReceiveStream(string connectionInfo, IPEndPoint endpoint,
			IProgress<ReceivmentProgress> reporter, ReceiveCallback receivmentAction);
	}
}