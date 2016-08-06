using System;

namespace AudioBooksPlayer.WPF.Streaming
{
    public struct CommandFrame
    {
        public Guid IdCommand { get; set; }
        public CommandEnum Type { get; set; }
        public byte[] FromIp { get; set; }
        public int FromIpPort { get; set; }
        public byte[] ToIp { get; set; }
        public int ToIpPort { get; set; }
        public string Book { get; set; }
		public string Command { get; set; }
    }
}