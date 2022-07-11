using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Plugins.Remote
{
    internal class Sender
    {
        public string ServerHost;
        public string Identification;
        private static string Mark = "client";

        public Sender(string whereToSend = null, string whoAmI = null)
        {
            ServerHost = whereToSend ?? "localhost";
            Identification = whoAmI ?? ("Test" + new System.Random().Next(0, 10000));
        }

        public IEnumerator PostStream<T1, T2>(string api, Func<T2, T1> next, float interval = 1, T2 currentStatus = default)
        {
            T1 nextData;
            var url = ServerHost + "/api/" + api;
            while ((nextData = next(currentStatus)) != null)
            {
                var jsonStream = EncodeJson(nextData);
                using (var uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonStream)))
                using (var downloadHandler = new DownloadHandlerBuffer())
                {
                    while (true)
                    {
                        using (UnityWebRequest uwr = new UnityWebRequest(url, "POST"))
                        {
                            uwr.SetRequestHeader("Accept", "application/json");
                            uwr.SetRequestHeader("Content-Type", "application/json");
                            uwr.SetRequestHeader("Identification", Identification);
                            uwr.SetRequestHeader("Mark", Mark);
                            uwr.SetRequestHeader("Json-Packing", "true");
                            uwr.uploadHandler = uploadHandler;
                            uwr.downloadHandler = downloadHandler;
                            uwr.disposeUploadHandlerOnDispose = false;
                            uwr.disposeDownloadHandlerOnDispose = false;
                            yield return uwr.SendWebRequest();
                            var data = Encoding.UTF8.GetString(uwr.downloadHandler.data);
                            if (uwr.isDone)
                            {
                                if (uwr.isNetworkError)
                                {
                                    yield return new WaitForSeconds(interval);
                                }
                                else if (uwr.isHttpError)
                                {
                                    Debug.LogError($"HttpError: {uwr.responseCode} - {data}");
                                    yield break;
                                }
                                else
                                {
                                    // success
                                    currentStatus = (T2)DecodeJson(data, typeof(T2));
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public IEnumerator SendJsonToAPI(string api, string json, Action<string> onRsp, Action<string, string> onErr)
        {
            var url = ServerHost + "/api/" + api;
            using (UnityWebRequest uwr = new UnityWebRequest(url, "POST"))
            {
                uwr.SetRequestHeader("Accept", "application/json");
                uwr.SetRequestHeader("Content-Type", "application/json");
                uwr.SetRequestHeader("Identification", Identification);
                uwr.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                uwr.downloadHandler = new DownloadHandlerBuffer();
                yield return uwr.SendWebRequest();
                // if (uwr.isDone && uwr.result == UnityWebRequest.Result.Success)  // in a newer version of unity, use this
                if (uwr.isDone && (!uwr.isNetworkError && !uwr.isHttpError))
                {
                    if (onRsp != null)
                    {
                        var data = Encoding.UTF8.GetString(uwr.downloadHandler.data);
                        onRsp(data);
                    }
                }
                else
                {
                    if (onErr != null)
                    {
                        var data = Encoding.UTF8.GetString(uwr.downloadHandler.data);
                        onErr(data, uwr.error);
                    }
                }
            }
        }

        // escape json string
        public static string EscapeJsonString(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        // unescape json string
        public static string UnescapeJsonString(string str)
        {
            return str.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        public static string UrlEscape(string str)
        {
            return UnityWebRequest.EscapeURL(str, Encoding.UTF8);
        }

        public static string UrlUnescape(string str)
        {
            return UnityWebRequest.UnEscapeURL(str, Encoding.UTF8);
        }

        public static string EncodeJson(object obj)
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
            else if (obj is DateTime)
            {
                return "\"" + UrlEscape(((DateTime)obj).ToString("yyyy-MM-dd HH:mm:ss.fff")) + "\"";
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
                    sb.Append("\"" + UrlEscape(key.ToString()) + "\":");
                    sb.Append(EncodeJson(dic[key]));
                    i++;
                }
                sb.Append("}");
                return sb.ToString();
            }
            else if (obj is IEnumerable)
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
                    sb.Append(EncodeJson(item));
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

        public static object DecodeJson(string json, Type type)
        {
            // ! generate by Copilot, has bugs
            if (json == "null")
            {
                return null;
            }
            else if (type == typeof(string))
            {
                if (json.StartsWith("\"") && json.EndsWith("\""))
                {
                    return UnescapeJsonString(json.Substring(1, json.Length - 2));
                }
                else
                {
                    return json;
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
            else if (type == typeof(DateTime))
            {
                return DateTime.Parse(UrlUnescape(json));
            }
            else if (type == typeof(IDictionary))
            {
                var dic = new Dictionary<string, object>();
                var obj = JsonUtility.FromJson(json, typeof(object));
                var dicObj = (IDictionary)obj;
                foreach (var key in dicObj.Keys)
                {
                    dic.Add(key.ToString(), DecodeJson(dicObj[key].ToString(), typeof(object)));
                }
                return dic;
            }
            else if (type == typeof(IEnumerable))
            {
                var list = new List<object>();
                var obj = JsonUtility.FromJson(json, typeof(object));
                var listObj = (IEnumerable)obj;
                foreach (var item in listObj)
                {
                    list.Add(DecodeJson(item.ToString(), typeof(object)));
                }
                return list;
            }
            else
            {
                return JsonUtility.FromJson(json, type);
            }
        }

        public static string ArgsToJson(params object[] args)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            var length = args == null ? 0 : args.Length;
            sb.Append("\"an\":" + length);
            for (int i = 0; i < length; ++i)
            {
                sb.Append(",");
                var arg = args[i];
                sb.Append("\"a" + (i + 1) + "\":");
                sb.Append(EncodeJson(arg));
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
}