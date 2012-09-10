using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    public static class Extensions
    {
        public static string F(this string format, params object[] args)
        {
            return String.Format(format, args);
        }

        /// <summary>
        /// Interpolates tokens of the form ${key} using the <paramref name="lookup"/> function provided. If the
        /// lookup function returns null, the token is left as-is, otherwise it is replaced with the value returned.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="lookup">A token key lookup function to produce a replacement value to interpolate. If the
        /// lookup function returns null, the token is left as-is, otherwise it is replaced with the value returned.</param>
        /// <returns></returns>
        public static string Interpolate(this string input, Func<string, string> lookup)
        {
            if (input == null) return null;

            var sbMsg = new StringBuilder(input.Length);

            int i = 0;
            while (i < input.Length)
            {
                if (input[i] != '$')
                {
                    sbMsg.Append(input[i]);
                    ++i;
                    continue;
                }

                // We have a '$':
                ++i;
                if (i >= input.Length)
                {
                    sbMsg.Append('$');
                    break;
                }

                if (input[i] != '{')
                {
                    // Just a regular old character to go straight through to the final text:
                    sbMsg.Append('$');
                    sbMsg.Append(input[i]);
                    ++i;
                    continue;
                }

                // We have a '{':

                ++i;
                int start = i;
                while (i < input.Length)
                {
                    if (input[i] == '}') break;
                    ++i;
                }

                // We hit the end?
                if (i >= input.Length)
                {
                    // FAIL.
                    i = start;
                    sbMsg.Append('$');
                    sbMsg.Append('{');
                    continue;
                }

                // Did we hit a real '}' character?
                if (input[i] != '}')
                {
                    // Wasn't a token like we thought, just output the '{' and keep going from there:
                    i = start;
                    sbMsg.Append('$');
                    sbMsg.Append('{');
                    continue;
                }

                // We have a token, sweet...
                string tokenName = input.Substring(start, i - start);

                ++i;

                // Look up the token name:
                string replText = lookup(tokenName);
                if (replText != null)
                {
                    // Insert the token's value:
                    sbMsg.Append(replText);
                }
                else
                {
                    // Token wasn't found, so just insert the text raw:
                    sbMsg.Append('$');
                    sbMsg.Append('{');
                    sbMsg.Append(tokenName);
                    sbMsg.Append('}');
                }
            }

            return sbMsg.ToString();
        }
    }
}
