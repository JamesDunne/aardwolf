using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace REST0.Definition
{
    public sealed class TeeTextReader : TextReader
    {
        readonly TextReader input;
        readonly TextWriter[] outputs;
        readonly bool bufferToNewline;
        readonly StringBuilder buffer;
        readonly int bufferSize;

        public TeeTextReader(TextReader input, TextWriter[] outputs, bool bufferToNewline = true, int bufferSize = 8192)
            : base()
        {
            this.input = input;
            this.outputs = outputs;

            this.bufferToNewline = bufferToNewline;
            this.bufferSize = bufferSize;
            if (bufferToNewline)
                this.buffer = new StringBuilder(bufferSize);
        }

        public TeeTextReader(TextReader input, TextWriter output, bool bufferToNewline = true, int bufferSize = 8192)
            : this(input, new TextWriter[1] { output }, bufferToNewline, bufferSize)
        {
        }

        public override int Peek()
        {
            return input.Peek();
        }

        void writeBuffer()
        {
            foreach (var output in outputs)
                output.Write(buffer.ToString());
            buffer.Clear();
        }

        public override int Read()
        {
            int c = input.Read();
            if (bufferToNewline)
            {
                if (c == -1 || c == '\n')
                {
                    // Both EOF and '\n' cause a newline append:
                    if (c == '\n') buffer.Append('\n');
                    writeBuffer();
                }
                else
                {
                    buffer.Append((char)c);
                    if (buffer.Length >= bufferSize)
                    {
                        writeBuffer();
                    }
                }
            }
            else
            {
                if (c != -1)
                {
                    foreach (var output in outputs)
                        output.Write((char)c);
                }
            }
            return c;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            int nr = base.Read(buffer, index, count);
            if (bufferToNewline)
            {
                writeBuffer();
            }
            if (nr > 0)
            {
                foreach (var output in outputs)
                    output.Write(buffer, index, nr);
            }
            return nr;
        }
    }
}
