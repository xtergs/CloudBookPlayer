using System;

namespace AudioBooksPlayer.WPF.Streaming
{
    struct UdpFrame
    {
        public Guid Id { get; set; }
        public int Order { get; set; }
        public byte[] Data { get; set; }
        public int Length { get; set; }
    }

	
}