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
        readonly Action<string>[] outputs;
        readonly StringBuilder buffer;
        readonly int bufferSize;

        public TeeTextReader(TextReader input, Action<string>[] outputs, int bufferSize = 8192)
            : base()
        {
            this.input = input;
            this.outputs = outputs;

            this.bufferSize = bufferSize;
            this.buffer = new StringBuilder(bufferSize);
        }

        public TeeTextReader(TextReader input, Action<string> output, int bufferSize = 8192)
            : this(input, new Action<string>[1] { output }, bufferSize)
        {
        }

        public override int Peek()
        {
            return input.Peek();
        }

        void writeBuffer()
        {
            foreach (var output in outputs)
                output(buffer.ToString());
            buffer.Clear();
        }

        public override int Read()
        {
            int c = input.Read();
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
            return c;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            int nr = base.Read(buffer, index, count);
            if (nr > 0)
            {
                for (int i = index; i < index + nr; ++i)
                {
                    this.buffer.Append(buffer[i]);
                    if ((buffer.Length >= bufferSize) || (buffer[i] == '\n'))
                        writeBuffer();
                }
            }
            else writeBuffer();
            return nr;
        }
    }
}
