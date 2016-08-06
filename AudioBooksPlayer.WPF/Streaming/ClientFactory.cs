using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AudioBooksPlayer.WPF.Streaming
{
	class ClientFactory
	{
		ConcurrentBag<UDPStreamer> clients = new ConcurrentBag<UDPStreamer>();
		private ConcurrentBag<PipeStreamer> pipeClients = new ConcurrentBag<PipeStreamer>(); 

		public UDPStreamer GetUdpClient()
		{
			UDPStreamer client;
			if (clients.TryTake(out client))
				return client;
			return new UDPStreamer();
		}

		public void Return(UDPStreamer client)
		{
			clients.Add(client);
		}

		public void Return(PipeStreamer client)
		{
			pipeClients.Add(client);
		}

		public PipeStreamer GetPipeClient()
		{
			PipeStreamer client;
			if (pipeClients.TryTake(out client))
				return client;
			return new PipeStreamer();
		}
	}
}
