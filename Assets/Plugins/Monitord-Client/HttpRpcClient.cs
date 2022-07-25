using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Wynne.MonitordClient
{
    internal interface IRpcClient
    {
        void Connect(string host, int? port);

        void Certificate(string appkey, string token, string showname);

        void Disconnect();

        void Reconnect();

        void Send(string method, params object[] args);

        event Action<string, object[]> OnReceived;

        bool IsConnected();
        bool IsConnecting();
    }

    public class HttpRpcClient : IRpcClient
    {
        private string UrlRoot;
        private string AppKey;
        private string Token;
        private string ShowName;
        private List<UnityWebRequest> requestToRetry;
        public bool IsConnected() { return requestToRetry != null && requestToRetry.Count == 0; }
        public bool IsConnecting() { return false; }

        public event Action<string, object[]> OnReceived;

        public void Certificate(string appkey, string token, string showname)
        {
            AppKey = appkey;
            Token = token;
            ShowName = showname;
        }

        public void Connect(string host, int? port)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            UrlRoot = host;
            if (port != null)
                UrlRoot += ":" + port;
            requestToRetry = new List<UnityWebRequest>();
        }

        public void Reconnect()
        {
            if (requestToRetry == null)
                throw new InvalidOperationException("Not connected");
            foreach (var uwr in requestToRetry)
            {
                SendWebRequest(uwr, true);
            }
            requestToRetry.Clear();
        }

        public void Send(string method, params object[] args)
        {
            if (!IsConnected())
                Reconnect();
            Post(method, args);
        }

        public void Disconnect()
        {
        }

        private void SetHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("C-App-Key", AppKey);
            request.SetRequestHeader("C-Token", Token);
            request.SetRequestHeader("C-Show-Name", ShowName);
            request.SetRequestHeader("C-Mark", "client");
        }

        public async Task<string> SendWebRequestAsync(UnityWebRequest uwr, bool retry = false)
        {
            var tcs = new TaskCompletionSource<object>();
            uwr.SendWebRequest().completed += op => tcs.SetResult(null);
            await tcs.Task;
            if (uwr.isNetworkError)
            {
                // cannot connect to server
                if (retry)
                {
                    uwr.disposeUploadHandlerOnDispose = false;
                    uwr.disposeDownloadHandlerOnDispose = false;
                    UnityWebRequest ruwr = new UnityWebRequest(uwr.url, uwr.method);
                    SetHeaders(ruwr);
                    ruwr.uploadHandler = uwr.uploadHandler;
                    ruwr.downloadHandler = uwr.downloadHandler;
                    requestToRetry.Add(ruwr);
                    return null;
                }
                else
                    throw new Exception(uwr.error);
            }
            else if (uwr.isHttpError)
            {
                // server error
                var data = Encoding.UTF8.GetString(uwr.downloadHandler.data);
                Debug.LogError($"HttpError: {uwr.responseCode} - {data}");
                throw new Exception(uwr.error);
            }
            else
            {
                // success
                var data = Encoding.UTF8.GetString(uwr.downloadHandler.data);
                return data;
            }
        }

        public void SendWebRequest(UnityWebRequest uwr, bool retry = false)
        {
            uwr.SendWebRequest().completed += op =>
            {
                if (uwr.isNetworkError)
                {
                    // cannot connect to server
                    if (retry)
                    {
                        uwr.disposeUploadHandlerOnDispose = false;
                        uwr.disposeDownloadHandlerOnDispose = false;
                        UnityWebRequest ruwr = new UnityWebRequest(uwr.url, uwr.method);
                        SetHeaders(ruwr);
                        ruwr.uploadHandler = uwr.uploadHandler;
                        ruwr.downloadHandler = uwr.downloadHandler;
                        requestToRetry.Add(ruwr);
                    }
                }
                else if (uwr.isHttpError)
                {
                    // server error
                    var data = Encoding.UTF8.GetString(uwr.downloadHandler.data);
                    Debug.LogError($"HttpError: {uwr.responseCode} - {data}");
                }
                else
                {
                    // success
                }
                uwr.Dispose();
            };
        }

        public void Post(string method, params object[] args)
        {
            var url = UrlRoot + "/api/" + method;
            UnityWebRequest uwr = new UnityWebRequest(url, "POST");
            uwr.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(HttpCodec.ArgvToJson(args)));
            uwr.downloadHandler = new DownloadHandlerBuffer();
            SetHeaders(uwr);
            SendWebRequest(uwr, true);
        }

        public async Task<T> Get<T>(string method, params object[] args)
        {
            var url = UrlRoot + "/api/" + method;
            UnityWebRequest uwr = new UnityWebRequest(url, "POST");
            uwr.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(HttpCodec.ArgvToJson(args)));
            uwr.downloadHandler = new DownloadHandlerBuffer();
            SetHeaders(uwr);
            string data = await SendWebRequestAsync(uwr, false);
            if (data == null)
                return default(T);
            return (T)HttpCodec.FromJson(data, typeof(T));
        }
    }
}
