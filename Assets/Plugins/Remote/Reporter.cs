using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Plugins.Remote
{
    internal class Reporter : MonoBehaviour
    {
        public string ServerHost;
        public string AppKey;
        public bool CollectingLog;
        public bool CollectingFps;
        public bool CollectingMem;
        private Collector Collector;
        private Sender Sender;

        private void Awake()
        {
            Collector = new Collector();
            Collector.Awake();
            Sender = new Sender(AppKey, ServerHost);
            Collector.CollectingLog = CollectingLog;
            Collector.CollectingFps = CollectingFps;
            Collector.CollectingMem = CollectingMem;
        }

        private void Start()
        {
            InvokeRepeating("PrintTime", 1, 1);
            StartCoroutine("SyncCollections", 3f);
            //Invoke("Test", 0f);
        }

        private void Update()
        {
            Collector.Update();
        }

        private void PrintTime()
        {
            print(Time.realtimeSinceStartup);
        }

        public IEnumerator SyncCollections(float interval = 3f)
        {
            var stream = new Collector.Stream(Collector);
            return Sender.PostStream("syncollec", _ => stream.ReadRest(false), interval, new object());
        }

        public void Test()
        {
            var json = Sender.ArgvToJson("aa", "bb", new Collector.Log[]{
                     new Collector.Log{
                         logString = "logString1",
                         stackTrace = "stackTrace1",
                         type = "type1",
                     },
                     new Collector.Log{
                         logString = "logString2",
                         stackTrace = "stackTrace2",
                         type = "type2",
                     },
                 }, 3.14f, 5);
            var os = Sender.ArgvFromJson(json, typeof(string), typeof(string), typeof(Collector.Log[]), typeof(float), typeof(int));
            foreach (var item in os)
            {
                Debug.Log($"{(item ?? new object()).GetType()} -- {item}");
            }
            foreach (var item in (Collector.Log[])os[2])
            {
                Debug.Log($"{item.logString} -- {item.stackTrace} -- {item.type}");
            }

            //print(typeof(IList).IsAssignableFrom(typeof(int[])));
            //print(typeof(IList<int>).IsAssignableFrom(typeof(int[])));
        }
    }
}