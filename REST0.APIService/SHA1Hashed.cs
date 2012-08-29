using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.APIService
{
    public sealed class SHA1Hashed<T>
    {
        readonly T _value;
        readonly byte[] _hash;

        public SHA1Hashed(T value, byte[] hash)
        {
            if (hash.Length != 20) throw new Exception("SHA1Hashed constructor requires a hash array length of 20 bytes");
            _value = value;
            _hash = hash;
        }

        public static implicit operator T(SHA1Hashed<T> hashed) { return hashed._value; }

        public T Value { get { return _value; } }
        public byte[] Hash { get { return _hash; } }
        public string HashHexString { get { return _hash.ToHexString(); } }
    }
}
