using HarmonyLib;
using System;
using System.Collections.Generic;

namespace LBWmodifier.FileSystem
{
    internal class FileSystemHook
    {
        public static Dictionary<AFileBase, string> neiFileHandles = new Dictionary<AFileBase, string>();
        public static Dictionary<AFileBase, string[][]> appendNeiArray = new Dictionary<AFileBase, string[][]>();

        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.FileOpen))]
        [HarmonyPatch(typeof(FileSystemWindows), nameof(FileSystemWindows.FileOpen))]
        [HarmonyPostfix]
        private static void FileOpen(AFileBase __result, string file_name)
        {
            if (file_name?.ToLower()?.EndsWith(".nei") == true)
            {
                lock (neiFileHandles)
                {
                    neiFileHandles.Add(__result, file_name);
                }
            }
        }


        [HarmonyPatch(typeof(WfFile), nameof(WfFile.Dispose), new Type[] { typeof(bool) })]
        [HarmonyPrefix]
        private static void Dispose(WfFile __instance)
        {
            lock (neiFileHandles)
            {
                neiFileHandles.Remove(__instance);
            }
        }

        //[HarmonyPatch(typeof(CsvParser), nameof(CsvParser.Open))]
        //[HarmonyPrefix]
        //private static void CsvParser_Open(AFileBase file)
        //{
        //    lock (neiFileHandles)
        //    {
        //        if (neiFileHandles.TryGetValue(file, out var csv))
        //        lock (appendNeiArray)
        //        {

        //        }
        //    }
        //}
    }
}
