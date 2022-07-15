using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Plugins.Remote
{
    internal class Collector
    {
        [Serializable]
        public class Log
        {
            public float time;
            public string logString;
            public string stackTrace;
            public string type;
        }

        [Serializable]
        public class Stat
        {
            public float time;
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
                    collector.CollectedLogs.RemoveRange(0, currentLogIndex);
                    collector.CollectedStats.RemoveRange(0, currentStatIndex);
                }
                return pack;
            }
        }

        [NonSerialized] public List<Log> CollectedLogs = new List<Log>();
        [NonSerialized] public List<Stat> CollectedStats = new List<Stat>();
        private float currentTime = 0;
        private float currentMem = 0;
        private float currentFps = 0;

        private List<Log> _threadedLogs = new List<Log>();
        private bool _collectingLog = false;
        private bool _collectingMem = false;
        private bool _collectingFps = false;

        public bool CollectingLog
        {
            get
            {
                return _collectingLog;
            }
            set
            {
                if (_collectingLog != value)
                {
                    _collectingLog = value;
                    if (value)
                    {
                        Application.logMessageReceivedThreaded += HandleThreadedLog;
                        Updating += UpdateLogs;
                    }
                    else
                    {
                        Application.logMessageReceivedThreaded -= HandleThreadedLog;
                        Updating -= UpdateLogs;
                    }
                }
            }
        }

        public bool CollectingMem
        {
            get
            {
                return _collectingMem;
            }
            set
            {
                if (_collectingMem != value)
                {
                    _collectingMem = value;
                    if (value)
                    {
                        Updating += UpdateMem;
                    }
                    else
                    {
                        Updating -= UpdateMem;
                    }
                }
            }
        }

        public bool CollectingFps
        {
            get
            {
                return _collectingFps;
            }
            set
            {
                if (_collectingFps != value)
                {
                    _collectingFps = value;
                    if (value)
                    {
                        Updating += UpdateFps;
                    }
                    else
                    {
                        Updating -= UpdateFps;
                    }
                }
            }
        }

        #region Logs

        private void HandleThreadedLog(string logString, string stackTrace, LogType type)
        {
            Log log = new Log() { time = currentTime, logString = logString, stackTrace = stackTrace, type = type.ToString() };
            lock (_threadedLogs)
            {
                _threadedLogs.Add(log);
            }
        }

        private void UpdateLogs()
        {
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
            var fps = 1f / Time.deltaTime;
            currentFps = fps;
        }

        private void UpdateMem()
        {
            var gcmem = ((float)System.GC.GetTotalMemory(false)) / 1024 / 1024;
            currentMem = gcmem;
        }

        private void UpdateTime()
        {
            var time = Time.realtimeSinceStartup;
            currentTime = time;
        }

        public void AddStat(string key, string value)
        {
            CollectedStats.Add(new Stat { time = currentTime, key = key, value = value });
        }

        public void AddStat(string key, bool value)
        {
            if (value)
                AddStat(key, "true");
            else
                AddStat(key, "false");
        }

        public void AddStat<T>(string key, T value) where T : struct
        {
            AddStat(key, value.ToString());
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

        public event Action Updating;

        public void Awake()
        {
            Updating += UpdateTime;
        }

        public void Update()
        {
            Updating();
            if (CollectingMem)
                AddStat("mem", currentMem);
            if (CollectingFps)
                AddStat("fps", currentFps);
        }
    }
}