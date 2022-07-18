using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Plugins.Remote
{
    public class Reporter : MonoBehaviour, ISenderContext, ICollectorContext
    {
        [Header("Sender")]
        [SerializeField] private bool defaultHttpSender = true;

        [SerializeField] private string serverHost = "localhost";
        [SerializeField] private string appKey = "unknown";
        [SerializeField] private string deviceKey = ("Test" + new System.Random().Next(0, 10000));

        [Header("Collector")]
        [SerializeField] private bool collectingLog = false;

        [SerializeField] private bool collectingFps = false;
        [SerializeField] private bool collectingMem = false;
        [SerializeField] private List<string> ignoredCustomTags = new List<string>();

        private Collector Collector;
        private Sender Sender;

        bool ISenderContext.Enabled { get => defaultHttpSender; }
        public string ServerHost { get => serverHost; set => serverHost = value; }
        public string AppKey { get => appKey; set => appKey = value; }
        public string DeviceKey { get => deviceKey; set => deviceKey = value; }
        public bool CollectingLog { get => collectingLog; set => collectingLog = value; }
        public bool CollectingFps { get => collectingFps; set => collectingFps = value; }
        public bool CollectingMem { get => collectingMem; set => collectingMem = value; }
        public List<string> IgnoredCustomTags { get => ignoredCustomTags; set => ignoredCustomTags = value; }

        private void Awake()
        {
            Collector = new Collector(this);
            Sender = new Sender(this);
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

        public IEnumerator SyncCollections(float interval = 3f)
        {
            var stream = new Collector.Stream(Collector);
            return Sender.PostStream("syncollec", () => stream.ReadRest(false), interval);
        }
    }
}