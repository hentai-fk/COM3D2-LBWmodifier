using HarmonyLib;
using MaidStatus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using LBWmodifier.Loader;

namespace LBWmodifier
{
    internal class SongLyrics
    {
        private static string songSubtitleDir = "LBWtranslation\\DanceSubtitle";
        private static string fixSongSubtitleFile = "LBWtranslation\\DanceSubtitle\\MusicNameToSubtitleFolder.txt";
        private static Dictionary<string, string[][]> subtitleNeiType = new Dictionary<string, string[][]>();

        private static void initialize()
        {
            Directory.CreateDirectory(songSubtitleDir);
            if (!File.Exists(fixSongSubtitleFile))
            {
                setMusicFile2CsvFolder(new Dictionary<string, Tuple<string, string, string>>());
            }
            foreach (var file in Directory.GetFiles(songSubtitleDir, "*", SearchOption.AllDirectories))
            {
                var filename = Path.GetFileName(file);
                bool isNei;
                if ((isNei = filename == "dance_subtitle.nei") || filename == "dance_subtitle.csv")
                {
                    var csvFolder = Path.GetFileName(Path.GetDirectoryName(file)).ToUpper();
                    if (subtitleNeiType.ContainsKey(csvFolder))
                        continue;
                    subtitleNeiType[csvFolder] = isNei ? CSVUtils.LoadFromFileNei(file) : CSVUtils.LoadFromCSVFile(file);
                    // 不存在 csv 的话就导出字幕歌词
                    if (isNei && !File.Exists(csvFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file)), "dance_subtitle.csv")))
                        CSVUtils.SaveArray2CSVFile(csvFolder, subtitleNeiType[csvFolder]);
                }
            }
        }

        public static AudioSource getPlayingAudioSource()
        {
            if (RhythmAction_Mgr.Instance == null)
                return null;
            var m_DanceMain = Traverse.Create(RhythmAction_Mgr.Instance).Field("m_DanceMain").GetValue() as DanceMain;
            if (m_DanceMain == null)
                return null;
            return Traverse.Create(m_DanceMain).Field("m_audioNew").GetValue() as AudioSource;
        }

        public static string getMusicSubtitleFolder(string song)
        {
            if (song != null && getMusicFile2CsvFolder().TryGetValue(song.ToUpper(), out var csv))
                return csv.Item2;
            return Traverse.Create(RhythmAction_Mgr.Instance).Field("m_UseMusicName").GetValue() as string;
        }

        public static Dictionary<string, Tuple<string, string, string>> getMusicFile2CsvFolder()
        {
            var result = new Dictionary<string, Tuple<string, string, string>>();
            foreach (var line in File.ReadAllLines(fixSongSubtitleFile))
            {
                var textParts = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (textParts.Length < 2) 
                    continue;
                if (textParts[0] == "[歌曲音频名字]")
                    continue;
                result[textParts[0].ToUpper()] = new Tuple<string, string, string>(textParts[0].Unescape(), textParts[1].Unescape(), textParts.Length < 3 ? null : textParts[2].Unescape());
            }
            return result;
        }

        public static void setMusicFile2CsvFolder(Dictionary<string, Tuple<string, string, string>> keyLine)
        {
            using (var output = new StreamWriter(fixSongSubtitleFile))
            {
                output.WriteLine("[歌曲音频名字]\t\t\t[歌曲字幕所处文件夹名字]\t\t\t[备注]");
                keyLine.Remove("[歌曲音频名字]");
                foreach (var item in keyLine)
                {
                    output.Write($"{item.Value.Item1.Escape()}\t\t\t{item.Value.Item2.Escape()}");
                    if (item.Value.Item3 != null)
                        output.Write($"\t\t\t{item.Value.Item3.Escape()}");
                    output.WriteLine();
                }
            }
        }

        private static List<Tuple<float, float, string>> turnSubtitleFromFullCsvArray(string[][] array)
        {
            var list = new List<Tuple<float, float, string>>();
            if (array.Length == 0 || array[0].Length < 3)
            {
                LBWmodifier.logger.LogError("failed to parse the header of dance_subtitle array");
                return null;
            }
            for (int i = 1; i < array.Length; i++)
            {
                if (!float.TryParse(array[i][0], out var val1))
                {
                    LBWmodifier.logger.LogError("failed to parse float begin value of dance_subtitle.csv: " + array[i][0]);
                    break;
                }
                if (!float.TryParse(array[i][1], out var val2))
                {
                    LBWmodifier.logger.LogError("failed to parse float end value of dance_subtitle.csv: " + array[i][1]);
                    break;
                }
                list.Add(new Tuple<float, float, string>(val1, val2, array[i][2]));
            }
            return list.Count == 0 ? null : list;
        }

        public static List<Tuple<float, float, string>> getSubtitleTimeLine(string csvMusicFolder)
        {
            if (csvMusicFolder == null)
                return null;
            // 首先从缓存里查找
            if (subtitleNeiType.TryGetValue(csvMusicFolder.ToUpper(), out var title))
            {
                return turnSubtitleFromFullCsvArray(title);
            }
            // 然后从当前游戏资源查找
            using (var wd = FileTool.OpenFileCompatible("csv_rhythm_action/" + csvMusicFolder + "/dance_subtitle.nei"))
            {
                return wd == null ? null : turnSubtitleFromFullCsvArray(CSVUtils.LoadFromFileNei(wd.file));
            }
        }

        private static void fixTimeLine(List<Tuple<float, float, string>> list)
        {
            float appendTime = 1;
            float extandTime = 1;
            for (int i = 0; i < list.Count; i++)
            {
                list[i] = new Tuple<float, float, string>(list[i].Item1 + appendTime, list[i].Item2 + appendTime, list[i].Item3);
            }

            list[0] = new Tuple<float, float, string>(list[0].Item1 - extandTime, list[0].Item2, list[0].Item3);
            if (list[0].Item1 < 0)
                list[0] = new Tuple<float, float, string>(0, list[0].Item2, list[0].Item3);
            list[list.Count - 1] = new Tuple<float, float, string>(list[list.Count - 1].Item1, list[list.Count - 1].Item2 + extandTime, list[list.Count - 1].Item3);
            if (list[list.Count - 1].Item1 < 0)
                list[list.Count - 1] = new Tuple<float, float, string>(0, list[list.Count - 1].Item2, list[list.Count - 1].Item3);

            for (int i = 0; i < list.Count - 1; i++)
            {
                if (list[i].Item2 + extandTime >= list[i + 1].Item1 - extandTime)
                {
                    list[i] = new Tuple<float, float, string>(list[i].Item1, (list[i].Item2 + list[i + 1].Item1) / 2, list[i].Item3);
                    list[i + 1] = new Tuple<float, float, string>(list[i].Item2, list[i + 1].Item2, list[i + 1].Item3);
                }
                else
                {
                    list[i] = new Tuple<float, float, string>(list[i].Item1, list[i].Item2 + extandTime, list[i].Item3);
                    list[i + 1] = new Tuple<float, float, string>(list[i + 1].Item1 - extandTime, list[i + 1].Item2, list[i + 1].Item3);
                }
            }
        }

        public static void callbackSongLyrics(AudioSourceMgr audioSourceMgr)
        {
            if (DanceMainHooker.hookerThread.Contains(Thread.CurrentThread.ManagedThreadId))
                DanceMainHooker.sourceMgr.Add(audioSourceMgr);
        }


        [HarmonyPatch(typeof(DanceMain), "Update")]
        public class DanceMainHooker
        {
            public static HashSet<int> hookerThread = new HashSet<int>();
            public static HashSet<AudioSourceMgr> sourceMgr = new HashSet<AudioSourceMgr>();
            private static Harmony harmony;

            public static void initialize()
            {
                SongLyrics.initialize();
                harmony = Harmony.CreateAndPatchAll(typeof(DanceMainHooker));
            }

            public static void unload()
            {
                harmony.UnpatchSelf();
            }

            public static void Prefix(DanceMain __instance, out int __state)
            {
                if (!Translator.开启歌词.Value)
                {
                    __state = 0;
                    return;
                }
                __state = (int) Traverse.Create(__instance).Field("m_eMode").GetValue();
                hookerThread.Add(Thread.CurrentThread.ManagedThreadId);
            }

            public static void Postfix(DanceMain __instance, int __state)
            {
                hookerThread.Remove(Thread.CurrentThread.ManagedThreadId);
                if (!Translator.开启歌词.Value)
                    return;
                if (__state != 1 || GameMain.Instance?.MainCamera?.gameObject?.activeSelf != true)
                    return;
                var audioNew = getPlayingAudioSource();
                foreach (var source in sourceMgr)
                {
                    if (source.audiosource == audioNew)
                    {
                        var audioFile = Path.GetFileNameWithoutExtension(source.FileName);
                        var folder = getMusicSubtitleFolder(audioFile);
                        var list = getSubtitleTimeLine(folder);
                        string musicName = null;

                        if (folder != null)
                        {
                            // 本地尝试记录没有的歌曲的字幕类型
                            var music = getMusicFile2CsvFolder();
                            if (!music.TryGetValue(audioFile.ToUpper(), out var musicRecord))
                            {
                                musicName = DanceMain.SelectDanceData?.title;
                                if (musicName == null)
                                {
                                    if (DanceMain.KaraokeMode)
                                        musicName = "卡拉OK模式";
                                    else
                                        musicName = "舞蹈模式";
                                }
                                else
                                {
                                    // 对歌曲进行翻译
                                    if (Translation.TranslateText(musicName, out var translation) != Translation.RESULT.FAIL)
                                        musicName = translation;
                                }
                                if (list == null)
                                    musicName += "(未找到歌曲字幕)";
                                music[audioFile.ToUpper()] = new Tuple<string, string, string>(audioFile, folder, musicName);
                                setMusicFile2CsvFolder(music);
                            }
                            else
                            {
                                musicName = musicRecord.Item3;
                            }
                        }

                        if (list == null)
                        {
                            LBWmodifier.logger.LogWarning("failed to find this kind of dance_subtitle.nei: " + folder);
                            break;
                        }

                        fixTimeLine(list);

                        LBWmodifier.logger.LogWarning("play dance bgm: " + audioFile + ", dance subtitle: " + folder);

                        __instance.StartCoroutine(tryUpdateLyric(list, audioFile, musicName, source));
                        break;
                    }
                }
                sourceMgr.Clear();
            }
        }

        public static IEnumerator tryUpdateLyric(List<Tuple<float, float, string>> list, string audioFile, string musicName, AudioSourceMgr audioSourceMgr)
        {
            int lastIndex = -1;
            while (true)
            {
                if (RhythmAction_Mgr.Instance == null)
                    yield break;
                float danceTimer = RhythmAction_Mgr.Instance.DanceTimer;
                if (danceTimer >= list[list.Count - 1].Item2)
                    yield break;
                bool isBetweenTimer = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Item1 <= danceTimer && danceTimer < list[i].Item2)
                    {
                        isBetweenTimer = true;
                        if (lastIndex != i)
                        {
                            lastIndex = i;
                            // 这里尝试翻译歌词
                            if (Translation.TranslateText(list[i].Item3, out var translation) == Translation.RESULT.FAIL)
                                translation = list[i].Item3;
                            Subtitle.Play(musicName, translation, audioFile, audioSourceMgr, true);
                        }
                        break;
                    }
                }
                if (!isBetweenTimer && lastIndex != -1)
                {
                    lastIndex = -1;
                    Subtitle.Play(musicName, "", audioFile, audioSourceMgr, true);
                }
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}
