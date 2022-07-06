using System;
using System.Collections;
using UnityEngine;

namespace Assets.Plugins.Remote
{
    internal class Reporter : MonoBehaviour
    {
        public string ServerHost;
        private Collector Collector;
        private Sender Sender;

        private void Awake()
        {
            Collector = new Collector();
            Collector.Initialize();
            Sender = new Sender();
        }

        private void Start()
        {
            StartCoroutine("Test");
        }

        private void Update()
        {
            Collector.Update();
        }

        [Serializable]
        private class test
        {
            //public Collector.Log[] logs;
            public object[] logs;
        }

        public IEnumerator Test()
        {
            var json = "";
            var t = new test();
            var logs = new Collector.Log[]
            {
                new Collector.Log(){ logString = "logString1", stackTrace = "stackTrace1", type = "type1"},
                new Collector.Log(){ logString = "logString2", stackTrace = "stackTrace2", type = "type2"},
                new Collector.Log(){ logString = "logString3", stackTrace = "stackTrace3", type = "type3"},
            };
            json = Sender.ArgsToJson(logs, "3333", "f&f\"ff", 666);
            print(json);
            return Sender.SendJsonToAPI("test", json, Debug.Log, (a, b) =>
            {
                Debug.LogError(a);
                Debug.Log(b);
            });
        }
    }
}