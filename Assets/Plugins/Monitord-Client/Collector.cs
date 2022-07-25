using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wynne.MonitordClient
{
    internal interface ICollectorContext
    {
        bool CollectingLog { get; set; }
        bool CollectingFps { get; set; }
        bool CollectingMem { get; set; }
        List<string> IgnoredCustomTags { get; set; }
    }

    public class Collector
    {
        [Serializable]
        public class Log
        {
            public double time;
            public string msg;
            public string trace;
            public string level;
        }

        [Serializable]
        public class Stat
        {
            public double time;
            public string key;
            public string value;
        }

        public class Stream
        {
            [Serializable]
            public class Pack
            {
                public Log[] logs;
                public Stat[] stats;
            }

            private int currentLogIndex = 0;
            private int currentStatIndex = 0;
            private Collector collector;

            public Stream(Collector collector)
            {
                if (collector == null)
                    throw new ArgumentNullException(nameof(collector));
                this.collector = collector;
            }

            public Pack ReadAll(bool clear)
            {
                var pack = new Pack();
                currentLogIndex = collector.CollectedLogs.Count;
                pack.logs = new Log[currentLogIndex];
                collector.CollectedLogs.CopyTo(pack.logs);
                currentStatIndex = collector.CollectedStats.Count;
                pack.stats = new Stat[currentStatIndex];
                collector.CollectedStats.CopyTo(pack.stats);
                if (clear)
                {
                    collector.CollectedLogs.Clear();
                    collector.CollectedStats.Clear();
                    currentLogIndex = 0;
                    currentStatIndex = 0;
                }
                return pack;
            }

            public Pack ReadRest(bool clear)
            {
                var pack = new Pack();
                pack.logs = new Log[collector.CollectedLogs.Count - currentLogIndex];
                collector.CollectedLogs.CopyTo(currentLogIndex, pack.logs, 0, pack.logs.Length);
                currentLogIndex = collector.CollectedLogs.Count;
                pack.stats = new Stat[collector.CollectedStats.Count - currentStatIndex];
                collector.CollectedStats.CopyTo(currentStatIndex, pack.stats, 0, pack.stats.Length);
                currentStatIndex = collector.CollectedStats.Count;
                if (clear)
                {
                    collector.CollectedLogs.Clear();
                    collector.CollectedStats.Clear();
                    currentLogIndex = 0;
                    currentStatIndex = 0;
                }
                return pack;
            }
        }

        [NonSerialized] public List<Log> CollectedLogs = new List<Log>();
        [NonSerialized] public List<Stat> CollectedStats = new List<Stat>();
        [NonSerialized] public double currentTime = 0;
        [NonSerialized] public float currentMem = 0;
        [NonSerialized] public float currentFps = 0;

        private List<Log> _threadedLogs = new List<Log>();
        private ICollectorContext context;

        private double unixTimeOffset = 0;

        public bool CollectingLog => context?.CollectingLog ?? false;
        public bool CollectingFps => context?.CollectingFps ?? false;
        public bool CollectingMem => context?.CollectingMem ?? false;
        public List<string> IgnoredCustomTags => context?.IgnoredCustomTags;

        #region Logs

        private void HandleThreadedLog(string logString, string stackTrace, LogType type)
        {
            if (!CollectingLog) return;
            Log log = new Log() { time = currentTime, msg = logString, trace = stackTrace, level = type.ToString() };
            lock (_threadedLogs)
            {
                _threadedLogs.Add(log);
            }
        }

        private void UpdateLogs()
        {
            if (!CollectingLog) return;
            if (_threadedLogs.Count > 0)
            {
                lock (_threadedLogs)
                {
                    for (int i = 0; i < _threadedLogs.Count; ++i)
                        CollectedLogs.Add(_threadedLogs[i]);
                    _threadedLogs.Clear();
                }
            }
        }

        public List<Log> GetLogs(bool swapOut = false)
        {
            if (swapOut)
            {
                var ret = CollectedLogs;
                CollectedLogs = new List<Log>();
                return ret;
            }
            return CollectedLogs;
        }

        #endregion Logs

        #region Stat

        private void UpdateFps()
        {
            if (!CollectingFps) return;
            var fps = 1f / Time.deltaTime;
            currentFps = fps;
        }

        private void UpdateMem()
        {
            if (!CollectingMem) return;
            var gcmem = ((float)System.GC.GetTotalMemory(false)) / 1024 / 1024;
            currentMem = gcmem;
        }

        private void UpdateTime()
        {
            var time = Time.realtimeSinceStartup + unixTimeOffset;
            currentTime = time;
        }

        public void AddStat(string key, string value)
        {
            if (IgnoredCustomTags != null && IgnoredCustomTags.Contains(key)) return;
            CollectedStats.Add(new Stat { time = currentTime, key = key, value = value });
        }

        public void AddStat(string key, bool value)
        {
            if (IgnoredCustomTags != null && IgnoredCustomTags.Contains(key)) return;
            if (value)
                CollectedStats.Add(new Stat { time = currentTime, key = key, value = "true" });
            else
                CollectedStats.Add(new Stat { time = currentTime, key = key, value = "false" });
        }

        public void AddStat<T>(string key, T value) where T : struct
        {
            if (IgnoredCustomTags != null && IgnoredCustomTags.Contains(key)) return;
            CollectedStats.Add(new Stat { time = currentTime, key = key, value = value.ToString() });
        }

        public List<Stat> GetStats(bool swapOut = false)
        {
            if (swapOut)
            {
                var ret = CollectedStats;
                CollectedStats = new List<Stat>();
                return ret;
            }
            return CollectedStats;
        }

        #endregion Stat

        internal Collector(ICollectorContext context)
        {
            this.context = context;
        }

        public event Action Updating;

        public void OnEnable()
        {
            OnDisable(); // to avoid multi-add event
            unixTimeOffset = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds - Time.realtimeSinceStartup;
            Application.logMessageReceivedThreaded += HandleThreadedLog;
            Updating += UpdateTime;
            Updating += UpdateLogs;
            Updating += UpdateFps;
            Updating += UpdateMem;
        }

        public void OnDisable()
        {
            Application.logMessageReceivedThreaded -= HandleThreadedLog;
            Updating -= UpdateTime;
            Updating -= UpdateLogs;
            Updating -= UpdateFps;
            Updating -= UpdateMem;
        }

        private float _lastUpdateTime = 0;
        private float _updateTime = 0.1f;

        public void Update()
        {
            Updating();
            if (Time.time - _lastUpdateTime > _updateTime)
            {
                _lastUpdateTime = Time.time;
            }
            else
            {
                return;
            }
            if (CollectingMem)
                AddStat("#mem", currentMem);
            if (CollectingFps)
                AddStat("#fps", currentFps);
        }
    }
}
