using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Wynne.MoniterdClient
{
    public class Reporter : MonoBehaviour, ICollectorContext
    {
        [Header("Sender")]
        [SerializeField] private bool defaultHttpSender = false;

        [Tooltip("server host to upload data, default \"https://dbg.ihuman.cc\"")]
        [SerializeField] private string serverHost = "https://dbg.ihuman.cc";

        [Tooltip("your app name, default \"unknown\"")]
        [SerializeField] private string appKey;

        [NonSerialized] private string deviceKey;

        [Tooltip("your device name, default to read from your device")]
        [SerializeField, FormerlySerializedAs("showName")] private string deviceShowName;

        [Header("Collector")]
        [SerializeField] private bool collectingLog = false;

        [SerializeField] private bool collectingFps = false;
        [SerializeField] private bool collectingMem = false;
        [SerializeField] private List<string> ignoredCustomTags = new List<string>();

        public Collector Collector { get; private set; }
        public string ServerHost { get => serverHost; set => serverHost = value; }
        public string AppKey { get => appKey; set => appKey = value; }
        public string DeviceKey { get => deviceKey; private set => deviceKey = value; }
        public string DeviceShowName { get => deviceShowName; set => deviceShowName = value; }
        public bool CollectingLog { get => collectingLog; set => collectingLog = value; }
        public bool CollectingFps { get => collectingFps; set => collectingFps = value; }
        public bool CollectingMem { get => collectingMem; set => collectingMem = value; }
        public List<string> IgnoredCustomTags { get => ignoredCustomTags; set => ignoredCustomTags = value; }

        public bool ShowDebugInfo { get; set; } = false;

        private void Awake()
        {
            if (string.IsNullOrEmpty(deviceKey))
            {
                deviceKey = SystemInfo.deviceUniqueIdentifier + "+" + DateTime.Now.Ticks;
            }
            if (string.IsNullOrEmpty(deviceShowName))
            {
                deviceShowName = SystemInfo.deviceName + "[" + SystemInfo.deviceModel + "]";
            }
            if (string.IsNullOrEmpty(serverHost))
            {
                serverHost = "https://dbg.ihuman.cc";
            }
            if (string.IsNullOrEmpty(appKey))
            {
                appKey = "unknown";
            }
            Collector = new Collector(this);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            StartCoroutine("SyncCollections", 3f);
        }

        private void OnEnable()
        {
            Collector.OnEnable();
        }

        private void OnDisable()
        {
            Collector.OnDisable();
        }

        private void Update()
        {
            Collector.Update();
        }

        private void OnGUI()
        {
            if (!ShowDebugInfo) return;
            string s = "";
            s += "serverHost:" + serverHost + "\n";
            s += "appKey:" + appKey + "\n";
            s += "deviceKey:" + deviceKey + "\n";
            s += "deviceShowName:" + deviceShowName + "\n";
            s += "collectingLog:" + collectingLog.ToString() + "\n";
            s += "collectingFps:" + collectingFps.ToString() + "\n";
            s += "collectingMem:" + collectingMem.ToString() + "\n";
            s += "ignoredCustomTags:" + string.Join(",", ignoredCustomTags.ToArray()) + "\n";
            GUI.Label(new Rect(10, 50, 500, 500), s);
        }

        public IEnumerator SyncCollections(float interval = 3f)
        {
            if (defaultHttpSender)
            {
                IRpcClient client = new HttpRpcClient();
                client.Connect(serverHost, null);
                client.Certificate(AppKey, DeviceKey, DeviceShowName);
                var stream = new Collector.Stream(Collector);
                while (true)
                {
                    if (client.IsConnected)
                    {
                        var data = stream.ReadRest(true);
                        client.Send("syncollec", data);
                    }
                    else
                    {
                        client.Reconnect();
                    }
                    yield return new WaitForSeconds(interval);
                }
            }
        }
    }
}