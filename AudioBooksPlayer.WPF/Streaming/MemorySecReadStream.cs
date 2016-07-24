using System;
using System.IO;

namespace RemoteAudioBooksPlayer.WPF.ViewModel
{
    public class MemorySecReadStream : MemoryStream
    {
        private byte[] buffer;

        public MemorySecReadStream(byte[] buf)
        {
            buffer = buf;
        }
        private long readPosition = 0;
        private long writePosition = 0;


        public override void Write(byte[] buf, int offset, int count)
        {
            var buff = this.buffer;
            Array.Copy(buf, offset, buff, writePosition, count);
            writePosition += count;
        }

        public override int Read(byte[] buf, int offset, int count)
        {
            var maxLen =  Math.Min(writePosition, buffer.Length - 1);
            long toCopy = Math.Min(count, maxLen - readPosition);
            Array.Copy(this.buffer, readPosition, buf, offset, toCopy);
            readPosition += toCopy;
            return (int)toCopy;
        }
    }
}