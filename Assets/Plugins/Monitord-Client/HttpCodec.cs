using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Wynne.MoniterdClient
{
    internal static class HttpCodec
    {
        public static string EscapeJsonString(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        // unescape json string
        public static string UnEscapeJsonString(string str)
        {
            return str.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        public static string EscapeURL(string str)
        {
            return UnityWebRequest.EscapeURL(str, Encoding.UTF8);
        }

        public static string UnEscapeURL(string str)
        {
            return UnityWebRequest.UnEscapeURL(str, Encoding.UTF8);
        }

        public static string ToJson(object obj)
        {
            if (obj == null)
            {
                return "null";
            }
            else if (obj is string)
            {
                return "\"" + EscapeJsonString((string)obj) + "\"";
            }
            else if (obj is bool)
            {
                return (bool)obj ? "true" : "false";
            }
            else if (obj.GetType().IsPrimitive)
            {
                return obj.ToString();
            }
            else if (obj is IDictionary)
            {
                var sb = new StringBuilder();
                sb.Append("{");
                var dic = (IDictionary)obj;
                var i = 0;
                foreach (var key in dic.Keys)
                {
                    if (i > 0)
                    {
                        sb.Append(",");
                    }
                    sb.Append("\"" + EscapeJsonString(key.ToString()) + "\":");
                    sb.Append(ToJson(dic[key]));
                    i++;
                }
                sb.Append("}");
                return sb.ToString();
            }
            else if (obj is IList)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                var i = 0;
                foreach (var item in (IEnumerable)obj)
                {
                    if (i > 0)
                    {
                        sb.Append(",");
                    }
                    sb.Append(ToJson(item));
                    i++;
                }
                sb.Append("]");
                return sb.ToString();
            }
            else
            {
                return JsonUtility.ToJson(obj);
            }
        }

        public static object FromJson(string json, Type type)
        {
            json = json.Trim();
            if (json == "null")
            {
                return null;
            }
            else if (type == typeof(string))
            {
                if (json.StartsWith("\"") && json.EndsWith("\""))
                {
                    return UnEscapeJsonString(json.Substring(1, json.Length - 2));
                }
                else
                {
                    return UnEscapeJsonString(json);
                }
            }
            else if (type == typeof(bool))
            {
                return json == "true";
            }
            else if (type.IsPrimitive)
            {
                return Convert.ChangeType(json, type);
            }
            else if (type.IsGenericType && typeof(IDictionary).IsAssignableFrom(type) && type.GetGenericArguments()[0] == typeof(string))
            {
                throw new NotImplementedException("IDictionary<string, T> is not supported yet");
            }
            else if ((type.IsGenericType || type.IsArray) && typeof(IList).IsAssignableFrom(type))
            {
                Type gtype = null;
                IList list = null;
                if (type.IsArray)
                {
                    gtype = type.GetElementType();
                    list = (IList)typeof(List<>).MakeGenericType(gtype).GetConstructor(Type.EmptyTypes).Invoke(null);
                }
                else
                {
                    gtype = type.GetGenericArguments()[0];
                    list = (IList)Activator.CreateInstance(type);
                }
                int index = 0, newindex;
                if (findNextIndex(json, '[', index, out index))
                {
                    while (true)
                    {
                        if (findNextIndex(json, ',', ++index, out newindex) || findNextIndex(json, ']', index, out newindex))
                        {
                            list.Add(FromJson(json.Substring(index, newindex - index), gtype));
                            index = newindex;
                            continue;
                        }
                        break;
                    }
                }
                if (type.IsArray)
                {
                    var array = Array.CreateInstance(gtype, list.Count);
                    for (int i = 0; i < list.Count; i++)
                    {
                        array.SetValue(list[i], i);
                    }
                    return array;
                }
                else
                {
                    return list;
                }
                // throw new NotImplementedException("IList<T> is not supported yet");
            }
            else
            {
                return JsonUtility.FromJson(json, type);
            }
        }

        public static string ArgvToJson(params object[] argv)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            var length = argv == null ? 0 : argv.Length;
            sb.Append("\"an\":" + length);
            for (int i = 0; i < length; ++i)
            {
                sb.Append(",");
                var arg = argv[i];
                sb.Append("\"a" + (i + 1) + "\":");
                sb.Append(ToJson(arg));
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static Dictionary<char, char> jsonQuoteMap = new Dictionary<char, char>()
        {
            { '"', '"' },
            { '{', '}' },
            { '[', ']' },
        };

        private static bool findNextIndex(string json, char charToFind, int index, out int newindex)
        {
            newindex = -1;
            bool escape = false;
            List<char> quotes = new List<char>();
            char quote = '\0';
            for (int i = index; i < json.Length; ++i)
            {
                var c = json[i];
                if (c == '\\')
                {
                    escape = !escape;
                }
                else if (escape)
                {
                    escape = false;
                }
                else if (c == charToFind && quote == '\0')
                {
                    newindex = i;
                    return true;
                }
                else if (c == quote)
                {
                    if (quotes.Count == 0)
                    {
                        quote = '\0';
                    }
                    else
                    {
                        quote = quotes[quotes.Count - 1];
                        quotes.RemoveAt(quotes.Count - 1);
                    }
                }
                else if (jsonQuoteMap.ContainsKey(c))
                {
                    quotes.Add(quote);
                    quote = jsonQuoteMap[c];
                }
            }
            return false;
        }

        public static object[] ArgvFromJson(string json, params Type[] types)
        {
            Dictionary<string, string> jsonmap = new Dictionary<string, string>();
            int index = 0, newindex;
            if (findNextIndex(json, '{', index, out index))
            {
                while (true)
                {
                    if (findNextIndex(json, '"', ++index, out index))
                    {
                        if (findNextIndex(json, '"', ++index, out newindex))
                        {
                            var key = json.Substring(index, newindex - index);
                            if (findNextIndex(json, ':', ++newindex, out index))
                            {
                                if (findNextIndex(json, ',', ++index, out newindex) || findNextIndex(json, '}', index, out newindex))
                                {
                                    jsonmap[key] = json.Substring(index, newindex - index);
                                    index = newindex;
                                    continue;
                                }
                            }
                        }
                    }
                    break;
                }
            }
            int n = 1;
            if (jsonmap.TryGetValue("an", out var anstr))
            {
                n = int.Parse(anstr);
            }
            var argv = new object[n];
            for (int i = 0; i < n; ++i)
            {
                var key = "a" + (i + 1);
                if (jsonmap.TryGetValue(key, out var jsonstr))
                {
                    argv[i] = FromJson(jsonstr, types == null ? null : types[i]);
                }
            }
            return argv;
        }
    }
}