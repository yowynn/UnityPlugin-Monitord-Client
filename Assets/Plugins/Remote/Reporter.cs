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
            //StartCoroutine("Test");
            //StartCoroutine("Report");
            InvokeRepeating("PrintTime", 1, 1);
            //StartCoroutine("TestResend");
            StartCoroutine("SyncCollections", 3f);
        }

        private void Update()
        {
            Collector.Update();
        }

        public IEnumerator Test()
        {
            var logs = new Collector.Log[]
            {
                new Collector.Log(){ logString = "logString1", stackTrace = "stackTrace1", type = "type1"},
                new Collector.Log(){ logString = "logString2", stackTrace = "stackTrace2", type = "type2"},
                new Collector.Log(){ logString = "logString3", stackTrace = "stackTrace3", type = "type3"},
            };
            var json = Sender.ArgsToJson(logs, "3333", "f&f\"ff", 666);
            print(json);
            return Sender.SendJsonToAPI("test", json, Debug.Log, (a, b) =>
            {
                Debug.LogError(a);
                Debug.Log(b);
            });
        }

        public IEnumerator SendCollections(float interval = 1f)
        {
            string json = null;
            Action<string> onSucc = _ =>
            {
                json = null;
                //print("Succ");
            };
            Action<string, string> onFail = (_1, _2) =>
            {
                //print("Fail");
            };
            while (true)
            {
                if (json == null)
                {
                    var logs = Collector.GetLogs(true);
                    var profiles = Collector.GetProfiles(true);
                    profiles = null; // test ignore
                    json = Sender.ArgsToJson(logs, profiles);
                    //print("New");
                }
                yield return Sender.SendJsonToAPI("syncollec", json, onSucc, onFail);
                yield return new WaitForSeconds(interval);
            }
        }

        public IEnumerator Report()
        {
            var json = Sender.ArgsToJson("client", Sender.Identification);
            yield return Sender.SendJsonToAPI("mark", json, null, null);
            //yield return 0;
            StartCoroutine("SyncCollections");
        }

        private void PrintTime()
        {
            print(Time.realtimeSinceStartup);
        }

        [Serializable]
        public class CollectionPackStatus
        {
            public int logIndex = 0;
            public int profileIndex = 0;
        }

        [Serializable]
        public class CollectionPack
        {
            public List<Collector.Log> logs;
            public List<Collector.Profile> profiles;
            public CollectionPackStatus lastStatus;
        }

        public IEnumerator SyncCollections(float interval = 3f)
        {
            Func<CollectionPackStatus, CollectionPack> next = (CollectionPackStatus status) =>
            {
                var logs = Collector.GetLogs(false);
                var profiles = Collector.GetProfiles(false);
                var logspan = logs.GetRange(status.logIndex, logs.Count - status.logIndex);
                var profilespan = profiles.GetRange(status.profileIndex, profiles.Count - status.profileIndex);
                return new CollectionPack()
                {
                    logs = logspan,
                    profiles = profilespan,
                    lastStatus = status,
                };
            };
            return Sender.PostStream("syncollec", next, interval, new CollectionPackStatus());
        }
    }
}