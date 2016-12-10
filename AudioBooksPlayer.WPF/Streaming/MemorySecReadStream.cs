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
        private volatile int readPosition = 0;
        private volatile int writePosition = 0;
        private bool writeOverflowed = false;

        public long ReadPosition => readPosition;
        public long WritePosition => writePosition;

        public bool IsClosed { get; private set; } = false;

        public void CloseStream()
        {
            IsClosed = true;
        }

        public long LeftToWrite
        {
            get
            {
                if (writePosition == readPosition)
                    return buffer.Length - writePosition;
                if (writePosition < readPosition)
                    return readPosition - writePosition;
                return buffer.Length - (writePosition - readPosition);
            }
        }

        public long LeftToRead
        {
            get
            {
	            if (readPosition == writePosition && !writeOverflowed)
	            {
		            return 0;
	            }
                if (readPosition < writePosition)
                {
                    return writePosition - readPosition;
                }
                return buffer.Length - (readPosition - writePosition);
            }
        }

        public override void Write(byte[] fromBuff, int offset, int count)
        {
            if (count > LeftToWrite)
                throw new ArgumentOutOfRangeException(nameof(count));
            var buff = this.buffer;
            int maxCount;
	        var backReadPosition = readPosition;
            if (backReadPosition <= writePosition)
            {
                maxCount = Math.Min(buffer.Length - writePosition, count);
                Array.Copy(fromBuff, offset, buff, writePosition, maxCount);
	            count -= (int)maxCount;
				offset += (int)maxCount;
                writePosition += maxCount;
	            if (writePosition == buffer.Length)
		            writePosition = 0;
                if (count > 0)
                {
                    writeOverflowed = true;
					maxCount = Math.Min(backReadPosition, count);
					Array.Copy(fromBuff, offset, buff, writePosition, maxCount);
					writePosition += maxCount;
                }
                return;
            }
            Array.Copy(fromBuff, offset, buff, writePosition, count);
            writePosition += count;

        }

        public override int Read(byte[] buf, int offset, int count)
        {
            if (IsClosed && LeftToRead == 0 )
            {
                return -1;
            }
            if (LeftToRead == 0)
                return 0;
            int maxLen;
	        var backWritePositon = writePosition;
	        if (readPosition > backWritePositon)
	        {
				//if (readPosition == backWritePositon && !writeOverflowed)
				//	return 0;
				maxLen = Math.Min(buffer.Length - readPosition, count);
				Array.Copy(this.buffer, readPosition, buf, offset, maxLen);
				readPosition += maxLen;
				offset += (int)maxLen;
		        count -= maxLen;
		        if (readPosition == buffer.Length)
		        {
			        readPosition = 0;
			        writeOverflowed = false;
		        }
		        if (count == 0)
			        return maxLen;
			}
			if (readPosition < backWritePositon)
            {
                maxLen = Math.Min(backWritePositon - readPosition, count);
                Array.Copy(this.buffer, readPosition, buf, offset, maxLen);
                readPosition += maxLen;
                return (int)maxLen;
            }
            
            return (int) 0;
        }

        public override long Length => LeftToRead;
        public override long Position { get { return readPosition; } set { readPosition = (int)value; } }
        public override bool CanSeek => true;
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override int Capacity => buffer.Length;

        public void Clear()
        {
            writePosition = 0;
            readPosition = 0;
        }

        public override void Flush()
        {
            IsClosed = true;
        }
    }
}