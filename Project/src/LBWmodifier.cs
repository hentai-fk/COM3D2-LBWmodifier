using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HarmonyLib.Public.Patching;
using MaidStatus;
using PrivateMaidMode;
using Schedule;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static FacilityDataTable;
using static ScenarioData;
using Teikokusou;
using static LBWmodifier.SongLyrics;

namespace LBWmodifier
{
    public class LBWmodifier : BaseUnityPlugin
    {
        public static ManualLogSource logger;
        public static readonly string cachePath = Path.Combine(Paths.CachePath, "LBWmodifier");

        private static Queue<Action> fixedUpdateQueue = new Queue<Action>();


        public virtual void Awake()
        {
            logger = Logger;
            // 创建默认的缓存文件夹
            Directory.CreateDirectory(cachePath);
        }

        public virtual void FixedUpdate()
        {
            lock (fixedUpdateQueue)
            {
                while (fixedUpdateQueue.Count > 0)
                {
                    fixedUpdateQueue.Dequeue()();
                }
            }
        }

        public static void DoMainAction(Action action)
        {
            lock (fixedUpdateQueue)
            {
                fixedUpdateQueue.Enqueue(action);
            }
        }
    }

    public static class LBWextention
    {
        public static void Assert(bool failed, string error)
        {
            if (failed)
            {
                LBWmodifier.logger.LogError(error);
            }
        }
    }
}
