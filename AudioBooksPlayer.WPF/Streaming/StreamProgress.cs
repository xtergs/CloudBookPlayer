using System;

namespace AudioBooksPlayer.WPF.Streaming
{
    public struct StreamProgress
    {
        public Guid OperationId { get; set; }
        public int Minimum { get; set; }
        public long Position { get; set; }
        public long Length { get; set; }
    }
}