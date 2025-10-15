using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using UnityEngine.UI;
using UnityEngine;
using System.Diagnostics;
using System.ComponentModel;
using HarmonyLib;
using Schedule;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using LBWmodifier.Loader;

namespace LBWmodifier
{
    public class Subtitle : MonoBehaviour
    {
        public static readonly bool IsVrMode = Environment.GetCommandLineArgs().Any(s => s.ToLower().Contains("/vr"));

        private static GameObject subtitleObject;
        private static Subtitle instance;
        private Outline subtitleOutline;
        private Text subtitleText;
        private CanvasScaler canvasScaler;
        private RectTransform rectTransform;

        private Vector2 nowScreenScale = Vector2.zero;
        private Vector2 nowScreenYDistance = Vector2.zero;
        private Vector2 nowScreenXDistance = Vector2.zero;

        // <mainName, voiceText, voiceName, voiceMgr>
        private List<Tuple<string, string, string, AudioSourceMgr>> showList =
            new List<Tuple<string, string, string, AudioSourceMgr>>();

        private bool updateSound;
        private bool isSongLyrics;

        public void Awake()
        {
            if (IsVrMode)
                initVrGui();
            else
                initGui();
        }

        private void initGui()
        {
            // 创建 Canvas
            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.parent = transform;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.matchWidthOrHeight = 0.3f;
            canvasScaler.referenceResolution = nowScreenScale;

            // 创建 Text 显示文本
            var textGO = new GameObject("TextBox");
            textGO.transform.SetParent(canvasObject.transform);

            // 设置 RectTransform 用于调整大小和位置
            rectTransform = textGO.AddComponent<RectTransform>();
            // 设置锚点到屏幕顶部居中
            rectTransform.anchorMin = new Vector2(0.5f, 1);
            rectTransform.anchorMax = new Vector2(0.5f, 1);
            // 设置 pivot 为顶部居中
            rectTransform.pivot = new Vector2(0.5f, 1);
            // 设置偏移量，确保文本框稍微离顶部有一些距离
            rectTransform.anchoredPosition = nowScreenYDistance;
            // 设置宽高
            rectTransform.sizeDelta = nowScreenXDistance;

            // 添加文字阴影
            subtitleOutline = textGO.AddComponent<Outline>();

            // 添加 Text 组件显示文字
            subtitleText = textGO.AddComponent<Text>();
            subtitleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); // 设置字体
            subtitleText.fontStyle = FontStyle.Bold;
            if (!isSongLyrics)
                SubtitleSetStyle1();
            else
                SubtitleSetStyle2();
        }

        private void initVrGui()
        {
            var uiRoot = GameObject.Find("SystemUI Root");
            LBWextention.Assert(uiRoot == null, "SystemUI Root == null");
            // 创建 Canvas
            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(uiRoot.transform);
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one;
            canvasObject.layer = uiRoot.layer;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.referenceResolution = nowScreenScale;

            rectTransform = canvasObject.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = nowScreenYDistance;
            rectTransform.sizeDelta = nowScreenXDistance;

            subtitleOutline = canvasObject.AddComponent<Outline>();

            subtitleText = canvasObject.AddComponent<Text>();
            subtitleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (!isSongLyrics)
                SubtitleSetStyle1();
            else
                SubtitleSetStyle2();
        }
        
        private void SubtitleSetStyle1()
        {
            subtitleText.fontSize = 24 * Translator.字幕文字大小设置1.Value / 100;
            subtitleText.fontStyle = Translator.文字使用粗体1.Value ? FontStyle.Bold : FontStyle.Normal;

            var effectDistance = 0.8f * Translator.字幕文字大小设置1.Value / 100;
            subtitleOutline.effectDistance = new Vector2(effectDistance, effectDistance);

            subtitleText.color = ConverColor(Translator.字幕文字颜色1.Value);
            subtitleOutline.effectColor = ConverColor(Translator.字幕文字阴影颜色1.Value);

            subtitleText.alignment = ConverAlignment(Translator.字幕位置对齐1.Value);

            nowScreenScale.x = 1920;
            nowScreenScale.y = 1080;
            nowScreenYDistance.x = 0;
            nowScreenYDistance.y = Translator.字幕位置竖向距离1.Value * (subtitleText.alignment == TextAnchor.UpperCenter ||
                                                                  subtitleText.alignment == TextAnchor.UpperLeft
                ? -1
                : 1);
            nowScreenXDistance.x = nowScreenScale.x - Translator.字幕位置横向距离1.Value;
            nowScreenXDistance.y = nowScreenScale.y;

            if (canvasScaler.referenceResolution.x != nowScreenScale.x ||
                canvasScaler.referenceResolution.y != nowScreenScale.y)
                canvasScaler.referenceResolution = nowScreenScale;
            if (rectTransform.anchoredPosition.x != nowScreenYDistance.x ||
                rectTransform.anchoredPosition.y != nowScreenYDistance.y)
                rectTransform.anchoredPosition = nowScreenYDistance;
            if (rectTransform.sizeDelta.x != nowScreenXDistance.x || rectTransform.sizeDelta.y != nowScreenXDistance.y)
                rectTransform.sizeDelta = nowScreenXDistance;
        }

        private void SubtitleSetStyle2()
        {
            subtitleText.fontSize = 24 * Translator.字幕文字大小设置2.Value / 100;
            subtitleText.fontStyle = Translator.文字使用粗体2.Value ? FontStyle.Bold : FontStyle.Normal;

            var effectDistance = 0.8f * Translator.字幕文字大小设置2.Value / 100;
            subtitleOutline.effectDistance = new Vector2(effectDistance, effectDistance);

            subtitleText.color = ConverColor(Translator.字幕文字颜色2.Value);
            subtitleOutline.effectColor = ConverColor(Translator.字幕文字阴影颜色2.Value);

            subtitleText.alignment = ConverAlignment(Translator.字幕位置对齐2.Value);

            nowScreenScale.x = 1920;
            nowScreenScale.y = 1080;
            nowScreenYDistance.x = 0;
            nowScreenYDistance.y = Translator.字幕位置竖向距离2.Value * (subtitleText.alignment == TextAnchor.UpperCenter ||
                                                                 subtitleText.alignment == TextAnchor.UpperLeft
                ? -1
                : 1);
            nowScreenXDistance.x = nowScreenScale.x - Translator.字幕位置横向距离2.Value;
            nowScreenXDistance.y = nowScreenScale.y;

            if (canvasScaler.referenceResolution.x != nowScreenScale.x ||
                canvasScaler.referenceResolution.y != nowScreenScale.y)
                canvasScaler.referenceResolution = nowScreenScale;
            if (rectTransform.anchoredPosition.x != nowScreenYDistance.x ||
                rectTransform.anchoredPosition.y != nowScreenYDistance.y)
                rectTransform.anchoredPosition = nowScreenYDistance;
            if (rectTransform.sizeDelta.x != nowScreenXDistance.x || rectTransform.sizeDelta.y != nowScreenXDistance.y)
                rectTransform.sizeDelta = nowScreenXDistance;
        }

        private static Color ConverColor(SubtitleColor color)
        {
            switch (color)
            {
                case SubtitleColor.白色: return Color.white;
                case SubtitleColor.深蓝色: return Color.blue;
                case SubtitleColor.淡蓝色: return Color.cyan;
                case SubtitleColor.绿色: return Color.green;
                case SubtitleColor.红色: return Color.red;
                case SubtitleColor.黄色: return Color.yellow;
                case SubtitleColor.粉红色: return Color.magenta;
                case SubtitleColor.灰色: return Color.gray;
                case SubtitleColor.黑色: return Color.black;
                default: return Color.clear;
            }
        }

        private static TextAnchor ConverAlignment(SubtitleAlignment alignment)
        {
            switch (alignment)
            {
                case SubtitleAlignment.顶部居中: return TextAnchor.UpperCenter;
                case SubtitleAlignment.顶部左端: return TextAnchor.UpperLeft;
                case SubtitleAlignment.底部居中: return TextAnchor.LowerCenter;
                case SubtitleAlignment.底部左端: return TextAnchor.LowerLeft;
                default: return TextAnchor.UpperCenter;
            }
        }

        public void Update()
        {
            if (!isSongLyrics)
                SubtitleSetStyle1();
            else
                SubtitleSetStyle2();
            UpdateShowingText();
            if (!AudioAndTextTrace.AvaiableShowSubtitle(!isSongLyrics))
                Hide();
        }

        private void UpdateShowingText()
        {
            lock (this)
            {
                var lastShowListCount = showList.Count;
                showList.RemoveAll(s => string.IsNullOrEmpty(s.Item2) || s.Item4 == null || !s.Item4.isPlay());
                if (updateSound || lastShowListCount != showList.Count)
                {
                    string content = "";
                    foreach (var item in showList)
                    {
                        if (string.IsNullOrEmpty(item.Item2))
                            continue;
                        if (!isSongLyrics)
                        {
                            if (Translator.显示女仆名字1.Value && !string.IsNullOrEmpty(item.Item1))
                                content +=
                                    $"<color=#{ColorUtility.ToHtmlStringRGBA(ConverColor(Translator.字幕女仆名字颜色1.Value))}>{item.Item1}</color>\u00A0{item.Item2}\n";
                            else
                                content += $"{item.Item2}\n";
                        }
                        else
                        {
                            if (Translator.显示歌词名称2.Value && !string.IsNullOrEmpty(item.Item1))
                                content +=
                                    $"<color=#{ColorUtility.ToHtmlStringRGBA(ConverColor(Translator.字幕歌词名称颜色2.Value))}>{item.Item1}</color>\u00A0{item.Item2}\n";
                            else
                                content += $"{item.Item2}\n";
                            if (Translator.歌词显示原文.Value)
                            {
                                if (Translator.显示歌词名称2.Value && !string.IsNullOrEmpty(item.Item1))
                                    content +=
                                        $"<color=#{ColorUtility.ToHtmlStringRGBA(ConverColor(Translator.字幕歌词名称颜色2.Value))}>" +
                                        $"{Translation.getOriginalFromTranslated(item.Item1)}</color>\u00A0{Translation.getOriginalFromTranslated(item.Item2)}\n";
                                else
                                    content += $"{Translation.getOriginalFromTranslated(item.Item2)}\n";
                            }
                        }
                    }

                    if (content.EndsWith("\n"))
                        content = content.Substring(0, content.Length - 1);
                    // 无条件设置为已翻译，防止AutoTranslator重复翻译
                    Translation.markTranslated(content, true);
                    subtitleText.text = content;
                }

                updateSound = false;
            }
        }

        public static void Show()
        {
            lock (typeof(Subtitle))
            {
                Hide();
                subtitleObject = new GameObject("Subtitle");
                instance = subtitleObject.AddComponent<Subtitle>();
                GameObject.DontDestroyOnLoad(subtitleObject);
            }
        }

        public static void Hide()
        {
            lock (typeof(Subtitle))
            {
                if (subtitleObject != null)
                    GameObject.Destroy(subtitleObject);
                subtitleObject = null;
                instance = null;
            }
        }

        /// <param name="text">=null or empty时，原来显示的字幕的句子将会被清除</param>
        /// <param file="text">播放的音频的名称</param>
        public static void Play(string name, string text, string file, AudioSourceMgr audio, bool isSong)
        {
            if ((!isSong ? !Translator.开启字幕.Value : !Translator.开启歌词.Value) || audio == null)
                return;
            lock (typeof(Subtitle))
            {
                if (instance == null)
                    Show();
                if (instance != null)
                {
                    lock (instance)
                    {
                        instance.updateSound = true;
                        if (isSong != instance.isSongLyrics)
                            instance.showList.Clear();
                        instance.isSongLyrics = isSong;
                        
                        for (int i = 0; i < instance.showList.Count; ++i)
                        {
                            if (instance.showList[i].Item3 == file)
                            {
                                instance.showList[i] =
                                    new Tuple<string, string, string, AudioSourceMgr>(name, text, file, audio);
                                return;
                            }
                        }

                        instance.showList.RemoveAll(item => item.Item4 == audio);
                        instance.showList.Add(
                            new Tuple<string, string, string, AudioSourceMgr>(name, text, file, audio));
                    }
                }
            }
        }
    }

    public enum SubtitleColor
    {
        白色,
        深蓝色,
        淡蓝色,
        绿色,
        红色,
        黄色,
        粉红色,
        灰色,
        黑色,
        透明
    }

    public enum SubtitleAlignment
    {
        顶部居中,
        底部居中,
        顶部左端,
        底部左端
    }

    public class AudioAndTextTrace
    {
        private static string lastPlayingTalkVoiceFile;

        // <file, audio>，储存未翻译的音频
        private static Dictionary<string, AudioSourceMgr> voicePlay = new Dictionary<string, AudioSourceMgr>();

        // <file, text>，储存已经翻译的音频文本
        private static Dictionary<string, string> voiceText = new Dictionary<string, string>();

        // 保存已经加载过的ks脚本
        private static HashSet<KeyValuePair<string, AFileSystemBase>> ksManager =
            new HashSet<KeyValuePair<string, AFileSystemBase>>();


        private static string GetMaidNameByAudio(AudioSourceMgr audio)
        {
            for (int i = 0; i < GameMain.Instance?.CharacterMgr?.GetMaidCount(); ++i)
            {
                var maid = GameMain.Instance.CharacterMgr.GetMaid(i);
                if (maid?.AudioMan == audio)
                    return maid.status.callName;
            }

            var maids = GameMain.Instance?.CharacterMgr?.GetStockMaidList();
            if (maids != null)
            {
                foreach (var maid in maids)
                {
                    if (maid?.AudioMan == audio)
                        return maid.status.callName;
                }
            }

            if (GameMain.Instance?.CharacterMgr != null)
            {
                maids = Traverse.Create(GameMain.Instance.CharacterMgr).Field("m_listStockNpcMaid").GetValue() as List<Maid>;
                foreach (var maid in maids)
                {
                    if (maid?.AudioMan == audio)
                        return maid.status.callName;
                }
            }

            return null;
        }

        public static bool AvaiableShowSubtitle(bool isVoice) => 
            (isVoice ? Translator.开启字幕.Value : Translator.开启歌词.Value)
            && GameMain.Instance?.MsgWnd != null 
            && (Traverse.Create(GameMain.Instance.MsgWnd).Field("m_goMessageWindowPanel").GetValue() as GameObject)?.activeSelf == false;

        [HarmonyPatch(typeof(YotogiKagManager), nameof(YotogiKagManager.TagTalk))]
        [HarmonyPatch(typeof(YotogiKagManager), nameof(YotogiKagManager.TagTalkAddFt))]
        [HarmonyPatch(typeof(YotogiKagManager), nameof(YotogiKagManager.TagTalkRepeat))]
        [HarmonyPatch(typeof(YotogiKagManager), nameof(YotogiKagManager.TagTalkRepeatAdd))]
        [HarmonyPrefix]
        private static void YotogiPlayTalkVoice(YotogiKagManager __instance, KagTagSupport tag_data)
        {
            if (AvaiableShowSubtitle(true) && tag_data.IsValid("voice"))
            {
                var file = tag_data.GetTagProperty("voice").AsString().ToUpper();
                if (!string.IsNullOrEmpty(file))
                    lastPlayingTalkVoiceFile = file;
            }
        }

        [HarmonyPatch(typeof(YotogiKagManager), nameof(YotogiKagManager.TagHitRet))]
        [HarmonyPostfix]
        private static void YotogiPlayHitRet(YotogiKagManager __instance, KagTagSupport tag_data)
        {
            // 音频文本不存在或者还没有被翻译
            if (AvaiableShowSubtitle(true) && lastPlayingTalkVoiceFile != null &&
                (!voiceText.TryGetValue(lastPlayingTalkVoiceFile, out var voiceMessage) ||
                 Translation.isNeedTranslated(voiceMessage, true)))
            {
                // GetText返回值已经被hook翻译
                var text = __instance.kag.GetText();
                // 文本已经翻译
                if (!string.IsNullOrEmpty(text) && !Translation.isNeedTranslated(text, true))
                {
                    // 存在待更新的文本的话就更新显示的字幕
                    if (voicePlay.TryGetValue(lastPlayingTalkVoiceFile, out var audio))
                        Subtitle.Play(GetMaidNameByAudio(audio), text, lastPlayingTalkVoiceFile, audio, false);
                    // 添加音频文本的翻译信息
                    voiceText[lastPlayingTalkVoiceFile] = text;
                }

                // 移除上一次的Talk音频记录和待更新的音频字幕记录
                voicePlay.Remove(lastPlayingTalkVoiceFile);
                lastPlayingTalkVoiceFile = null;
            }
        }

        [HarmonyPatch(typeof(AudioSourceMgr), nameof(AudioSourceMgr.Play))]
        [HarmonyPatch(typeof(AudioSourceMgr), nameof(AudioSourceMgr.PlayOneShot))]
        [HarmonyPrefix]
        private static void PlayGlobalVoice(AudioSourceMgr __instance)
        {
            if (!AvaiableShowSubtitle(true))
                return;
            if (__instance.SoundType == AudioSourceMgr.Type.Bgm)
            {
                // 可能是舞蹈歌曲
                SongLyrics.callbackSongLyrics(__instance);
                return;
            }

            if (__instance.SoundType < AudioSourceMgr.Type.Voice || GameMain.Instance?.MsgWnd == null ||
                (Traverse.Create(GameMain.Instance.MsgWnd).Field("m_goMessageWindowPanel").GetValue() as GameObject)
                .activeSelf)
                return;
            // 根据文件找到对应的文本，还没有被翻译或者没有找到且没有音频翻译就添加到待更新列表
            var file = Path.GetFileNameWithoutExtension(__instance.FileName).ToUpper();
            var hasVoiceText = voiceText.TryGetValue(file, out var text);
            if (hasVoiceText && Translation.isNeedTranslated(text, true) || !hasVoiceText &&
                Translation.TranslateVoice(file, out text) == Translation.RESULT.FAIL)
            {
                voicePlay[file] = __instance;
            }

            // 文本不为空时播放音频，不管是否已经翻译
            if (!string.IsNullOrEmpty(text))
                Subtitle.Play(GetMaidNameByAudio(__instance), text, file, __instance, false);
        }

        [HarmonyPatch(typeof(KagScript), nameof(KagScript.CallTag))]
        [HarmonyPrefix]
        private static void KagScriptCallTag(KagScript __instance)
        {
            if (!AvaiableShowSubtitle(true))
                return;
            var name = __instance.GetCurrentFileName();
            if (!string.IsNullOrEmpty(name))
            {
                // 反射调用hook
                using (FileTool.OpenFileCompatible(name)) { }
            }
        }

        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.FileOpen))]
        [HarmonyPatch(typeof(FileSystemWindows), nameof(FileSystemWindows.FileOpen))]
        [HarmonyPostfix]
        private static void FileOpenKagScript(AFileSystemBase __instance, AFileBase __result, string file_name)
        {
            if (!AvaiableShowSubtitle(true) || __result == null || Path.GetExtension(file_name) != ".ks" ||
                !ksManager.Add(new KeyValuePair<string, AFileSystemBase>(file_name, __instance)))
                return;
            var bytes = __result.ReadAll();
            if (bytes == null)
                return;
            // 解析ks脚本
            foreach (var text in AnalyzeKagScript.SplitVoiceAndText(NUty.SjisToUnicode(bytes)))
            {
                // 尝试翻译原文
                if (Translation.TranslateText(text.Value, out var translate) != Translation.RESULT.FAIL)
                    voiceText[text.Key.ToUpper()] = translate;
                else if (Translation.TranslateVoice(text.Key, out translate) != Translation.RESULT.FAIL)
                    voiceText[text.Key.ToUpper()] = translate;
                else
                    voiceText[text.Key.ToUpper()] = text.Value;
            }
        }
    }

    public static class AnalyzeKagScript
    {
        /// <link cref="Translation.WhitespaceChars">
        public static readonly char[] WhitespaceChars = new char[]
        {
            '\t', '\n', '\v', '\f', '\r', ' ', '\u0085', '\u00a0', '\u1680', '\u2000',
            '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200a',
            '\u200b', '\u2028', '\u2029', '\u3000', '\ufeff', /*增加两个字符*/ ';', '@'
        };

        private static readonly Regex voiceTag =
            new Regex(
                @"\b(talk|talkaddft|talkRepeat|talkRepeatAdd|talkRepeatRepeat|rc_talk|PlayVoice)\b.*\bvoice=(?<voice>[\w-]+)",
                RegexOptions.IgnoreCase);

        private static readonly Regex hitretTag =
            new Regex(@"(?<header>.*?)(@|\b(hitret|return|VoiceWait)\b)", RegexOptions.IgnoreCase);

        private static string GetVoiceName(string line)
        {
            var m = voiceTag.Match(line);
            if (m.Success)
                return Path.GetFileName(m.Groups["voice"].Value);
            return null;
        }

        private static string GetHitRetTagHeader(string line)
        {
            var m = hitretTag.Match(line);
            if (m.Success)
                return m.Groups["header"].Value;
            return null;
        }

        private static string GetReadableText(string text)
        {
            var result = text.Trim(WhitespaceChars);
            // *开头的是标签
            if (result.StartsWith("*") && result.Length > 1)
                return "";
            return result;
        }

        /// <returns>Dictionary is not null and each key and value is not null</returns>
        public static Dictionary<string, string> SplitVoiceAndText(string whole)
        {
            var result = new Dictionary<string, string>();
            var lines = whole.Split(new[] {"\r\n", "\n"}, StringSplitOptions.RemoveEmptyEntries);
            int begin = -1;
            string voice = null;
            for (int i = 0; i < lines.Length; ++i)
            {
                var name = GetVoiceName(lines[i]);
                if (name != null)
                {
                    begin = i;
                    voice = name;
                    continue;
                }

                if (begin == -1 || voice == null)
                    continue;
                var header = GetHitRetTagHeader(lines[i]);
                if (header != null)
                {
                    string text = "";
                    for (int j = begin + 1; j < i; ++j)
                        text += GetReadableText(lines[j]) + '\n';
                    text = GetReadableText(text + GetReadableText(header));
                    if (!string.IsNullOrEmpty(text))
                        result[voice] = text;
                    begin = -1;
                    voice = null;
                    continue;
                }

                var line = GetReadableText(lines[i]);
                if (!string.IsNullOrEmpty(line))
                {
                    result[voice] = line;
                    begin = -1;
                    voice = null;
                }
            }

            return result;
        }
    }
}