using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace REST0
{
    public sealed class SHA1InputStream : Stream
    {
        private long position;
        private SHA1 sha1;
        private Stream input;

        public SHA1InputStream(Stream input)
        {
            this.input = input;

            this.sha1 = SHA1.Create();
            this.position = 0L;
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return false; } }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { return input.Length; }
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
            int nr = input.Read(buffer, offset, count);
            if (nr == 0) return 0;

            sha1.TransformBlock(buffer, offset, nr, null, 0);
            position += nr;

            return nr;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
