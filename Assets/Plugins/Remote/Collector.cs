using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Plugins.Remote
{
    internal class Collector
    {
        #region Logs

        [Serializable]
        public class Log
        {
            public string logString;
            public string stackTrace;
            public string type;
        }

        private List<Log> CollectedLogs = new List<Log>();

        private void Init_Logs()
        {
            Application.logMessageReceivedThreaded += HandleThreadedLog;
        }

        private List<Log> threadedLogs = new List<Log>();

        private void HandleThreadedLog(string logString, string stackTrace, LogType type)
        {
            Log log = new Log() { logString = logString, stackTrace = stackTrace, type = type.ToString() };
            lock (threadedLogs)
            {
                threadedLogs.Add(log);
            }
        }

        private void Update_Logs()
        {
            if (threadedLogs.Count > 0)
            {
                lock (threadedLogs)
                {
                    for (int i = 0; i < threadedLogs.Count; ++i)
                    {
                        AddLog(threadedLogs[i]);
                    }
                    threadedLogs.Clear();
                }
            }
        }

        private void AddLog(Log log)
        {
            CollectedLogs.Add(log);
        }

        public List<Log> GetLogs()
        {
            return CollectedLogs;
        }

        #endregion Logs

        #region Profiles

        public class Profile
        {
            public float fps { get; set; }
            public float mem { get; set; }
        }

        private List<Profile> CollectedProfiles = new List<Profile>();

        [SerializeField] private float currentFPS = 0;

        private void Update_CalcFPS()
        {
            var fps = 1f / Time.deltaTime;
            currentFPS = fps;
        }

        [SerializeField] private float currentMEM = 0;

        private void Update_CalcMEM()
        {
            var gcmem = ((float)System.GC.GetTotalMemory(false)) / 1024 / 1024;
            currentMEM = gcmem;
        }

        [SerializeField] private float currentTIME = 0;

        private void Update_CalcTIME()
        {
            var time = Time.realtimeSinceStartup;
            currentTIME = time;
        }

        private void Update_Profiles()
        {
            Update_CalcFPS();
            Update_CalcMEM();
            Update_CalcTIME();
            var profile = new Profile { fps = currentFPS, mem = currentMEM };
            CollectedProfiles.Add(profile);
        }

        public List<Profile> GetProfiles()
        {
            return CollectedProfiles;
        }

        #endregion Profiles

        public void Initialize()
        {
            //Application.targetFrameRate = -1;
            Init_Logs();
        }

        private float lastUpdateTime = 0;

        public void Update()
        {
            Update_Logs();
            Update_Profiles();
            lastUpdateTime = Time.realtimeSinceStartup;
        }
    }
}