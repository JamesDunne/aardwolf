using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace Aardwolf
{
    public sealed class SHA1OutputStream : Stream
    {
        private long position;
        private SHA1 sha1;
        private Stream output;

        public SHA1OutputStream(Stream output)
        {
            this.output = output;

            this.sha1 = SHA1.Create();
            this.position = 0L;
        }

        public override bool CanRead { get { return false; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return true; } }

        public override void Flush()
        {
            output.Flush();
        }

        public override long Length
        {
            get { return output.Length; }
        }

        public override long Position
        {
            get
            {
                return this.position;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            output.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            sha1.TransformBlock(buffer, offset, count, null, 0);
            output.Write(buffer, offset, count);
            position += count;
        }

        private readonly static byte[] dum = new byte[0];
        private bool isFinal = false;

        public byte[] GetHash()
        {
            if (!isFinal)
            {
                sha1.TransformFinalBlock(dum, 0, 0);
                isFinal = true;
            }

            return sha1.Hash;
        }
    }
}
