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
        private Collector Collector;
        private Sender Sender;

        private void Awake()
        {
            Collector = new Collector();
            Collector.Initialize();
            Sender = new Sender(AppKey, ServerHost);
        }

        private void Start()
        {
            //InvokeRepeating("PrintTime", 1, 1);
            //StartCoroutine("SyncCollections", 3f);
            Invoke("Test", 0f);
        }

        private void Update()
        {
            Collector.Update();
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