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
        private bool writeOverflowed = false;

        public long ReadPosition => readPosition;
        public long WritePosition => writePosition;

        public long LeftToWrite
        {
            get
            {
                if (writePosition == readPosition)
                    return buffer.Length - writePosition;
                if (writePosition < readPosition)
                    return readPosition - writePosition;
                return buffer.Length - writePosition + readPosition;
            }
        }

        public long LeftToRead
        {
            get
            {
                if (readPosition < writePosition)
                {
                    return writePosition - readPosition;
                }
                return buffer.Length - readPosition + writePosition;
            }
        }

        public override void Write(byte[] buf, int offset, int count)
        {
            if (count - offset > LeftToWrite)
                throw new ArgumentOutOfRangeException(nameof(count));
            var buff = this.buffer;
            long maxCount;
            if (readPosition <= writePosition)
            {
                maxCount = Math.Min(buffer.Length - writePosition, count - offset);
                Array.Copy(buf, offset, buff, writePosition, maxCount);
                offset += (int)maxCount;
                writePosition += maxCount;
                if (writePosition == buffer.Length)
                {
                    writePosition = 0;
                    writeOverflowed = true;
                }
                if (count - offset == 0)
                    return;
                writePosition = 0;
                maxCount = Math.Min(readPosition, count - offset);
                Array.Copy(buf, offset, buff, writePosition, maxCount);
                writePosition += maxCount;
                return;
            }
            Array.Copy(buf, offset, buff, writePosition, count);
            writePosition += count;

        }

        public override int Read(byte[] buf, int offset, int count)
        {
            if (LeftToRead == 0)
                return 0;
            long maxLen;
            if (readPosition < writePosition)
            {
                maxLen = Math.Min(writePosition-readPosition, count);
                Array.Copy(this.buffer, readPosition, buf, offset, maxLen);
                readPosition += maxLen;
                return (int)maxLen;
            }
            if (readPosition == writePosition && !writeOverflowed)
                return 0;
            maxLen = Math.Min(buffer.Length - readPosition, count);
            Array.Copy(this.buffer, readPosition, buf, offset, maxLen);
            if (readPosition == writePosition)
                writeOverflowed = false;
            offset += (int)maxLen;
            readPosition += maxLen;
            if (readPosition == buffer.Length)
                readPosition = 0;
            if (count - offset == 0)
                return count;
            maxLen = Math.Min(writePosition, count - offset);
            Array.Copy(this.buffer, readPosition, buf, offset, maxLen);
            readPosition += maxLen;
            return (int) maxLen;
        }

        public override long Length => LeftToRead;
        public override long Position { get { return readPosition; } set { readPosition = value; } }
        public override bool CanSeek => true;
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override int Capacity => buffer.Length;

        public void Clear()
        {
            writePosition = 0;
            readPosition = 0;
        }
    }
}