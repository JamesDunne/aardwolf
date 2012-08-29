using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.Definition
{
    public sealed class ConfigurationDictionary
    {
        readonly Dictionary<string, List<string>> _values;

        internal ConfigurationDictionary(Dictionary<string, List<string>> values)
        {
            _values = values;
        }

        public static ConfigurationDictionary Parse(IEnumerable<string> args)
        {
            var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var arg in args)
            {
                // Split at first '=':
                int eqidx;
                if ((eqidx = arg.IndexOf('=')) == -1) continue;

                string key, value;
                key = arg.Substring(0, eqidx);
                value = arg.Substring(eqidx + 1);

                // Create the list of values for the key if necessary:
                List<string> list;
                if (!values.TryGetValue(key, out list))
                {
                    list = new List<string>();
                    values.Add(key, list);
                }

                // Add the value to the list:
                list.Add(value);
            }

            return new ConfigurationDictionary(values);
        }

        public string SingleValue(string key)
        {
            List<string> list;
            if (!_values.TryGetValue(key, out list) || list.Count == 0)
                throw new Exception(String.Format("Configuration key '{0}' is required", key));
            if (list.Count > 1)
                throw new Exception(String.Format("Configuration key '{0}' has more than one value", key));
            return list[0];
        }

        public string SingleValueOrDefault(string key, string defaultValue)
        {
            List<string> list;
            if (!_values.TryGetValue(key, out list) || list.Count == 0)
                return defaultValue;
            if (list.Count > 1)
                throw new Exception(String.Format("Configuration key '{0}' has more than one value", key));
            return list[0];
        }

        public List<string> Values(string key)
        {
            List<string> list;
            if (!_values.TryGetValue(key, out list))
                throw new Exception(String.Format("Configuration key '{0}' is required", key));
            return list;
        }

        public bool TryGetValue(string key, out List<string> values)
        {
            return _values.TryGetValue(key, out values);
        }
    }
}
