using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using static VRFaceShortcutConfig;
using static Yotogis.Skill.Old.Data.Command.Data;

namespace LBWmodifier.Loader
{
    public class HotLoaderInterface
    {
        private static GameObject gameObject;

        public static void Load()
        {
            gameObject = new GameObject();
#if MODIFIER
            gameObject.AddComponent<Modifier>();
#endif
#if TRANSLATOR
            gameObject.AddComponent<Translator>();
#endif
            GameObject.DontDestroyOnLoad(gameObject);
        }

        public static void Unload()
        {
            GameObject.Destroy(gameObject);
        }
    }
}
