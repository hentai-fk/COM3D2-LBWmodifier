using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LBWmodifier
{
    public class Translation
    {
        public static readonly char[] WhitespaceChars = new char[]
        {
            '\t', '\n', '\v', '\f', '\r', ' ', '\u0085', '\u00a0', '\u1680', '\u2000',
            '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200a',
            '\u200b', '\u2028', '\u2029', '\u3000', '\ufeff'
        };

        // <pattern, translation>
        private static Dictionary<string, string> loadedStringText = new Dictionary<string, string>();

        // <translation>
        private static Dictionary<string, string> translatedStringText = new Dictionary<string, string>();

        // <pattern>
        private static HashSet<string> untranslatedStringText = new HashSet<string>();

        // <pattern, translation>
        private static Dictionary<string, string> globalStringText = new Dictionary<string, string>();

        // <regex, replace_with_translation>
        private static Dictionary<Regex, KeyValuePair<string, string>> globalStringRegex = new Dictionary<Regex, KeyValuePair<string, string>>();

        private static bool hasWriteUntranslatedText;
        private static bool hasWriteUntranslatedVoice;

        private static Harmony harmony_1;
        private static Harmony harmony_2;


        public static void initialize()
        {
            if (harmony_1 != null)
                return;

            // 初始化资源替换路径
            Directory.CreateDirectory("LBWtranslation\\Script");
            Directory.CreateDirectory("LBWtranslation\\Texture");
            try
            {
                File.Delete("LBWtranslation\\__DUMP__\\UntranslatedText.txt");
                File.Delete("LBWtranslation\\__DUMP__\\UntranslatedVoice.txt");
            }
            catch { }

            // 开始加载翻译文件
            LBWmodifier.logger.LogInfo("start to load translation text...");

            var fileList = Directory.GetFiles("LBWtranslation\\Script", "*.txt", SearchOption.AllDirectories);
            if (!tryToLoadTranslationFromCache(fileList, false))
            {
                foreach (var file in fileList)
                {
                    LBWmodifier.logger.LogInfo("initializing translation file " + file);
                    loadFile(file);
                }
                tryToLoadTranslationFromCache(fileList, true);
            }

            LBWmodifier.logger.LogInfo("load translation text finished");
            
            TextureReplaceManager.initialize();

            harmony_1 = Harmony.CreateAndPatchAll(typeof(Translation));
            harmony_2 = Harmony.CreateAndPatchAll(typeof(TextureReplace));

            var tryMatchNGUITextMethod = AccessTools.Method("NGUIText:WrapText",
                    new Type[] { typeof(string), typeof(string).MakeByRefType(), typeof(bool) });
            if (tryMatchNGUITextMethod != null)
            {
                harmony_1.Patch(tryMatchNGUITextMethod, new HarmonyMethod(typeof(Translation).GetMethod("NGUITextWrapText")));
            }

            MaidCafeTranslation.load();
        }

        public static void unload()
        {
            harmony_1?.UnpatchSelf();
            harmony_2?.UnpatchSelf();
            harmony_1 = null;
            harmony_2 = null;
            MaidCafeTranslation.unload();

            loadedStringText.Clear();
            translatedStringText.Clear();
            untranslatedStringText.Clear();
            globalStringText.Clear();
            globalStringRegex.Clear();
        }

        public static bool isInited() => harmony_1 != null && harmony_2 != null;

        private static bool tryToLoadTranslationFromCache(string[] fileSaveList, bool isSave)
        {
            var cacheM = Path.Combine(LBWmodifier.cachePath, "translation_cache.md5");
            var cacheText = Path.Combine(LBWmodifier.cachePath, "translation_cache_text.bin");
            var cacheRegex = Path.Combine(LBWmodifier.cachePath, "translation_cache_regex.bin");

            // 计算所有翻译文件的md5值，判断是否需要缓存
            var fileListMD5 = "";
            foreach (var file in fileSaveList)
            {
                fileListMD5 = FileTool.getTextMD5(fileListMD5 + "|" + FileTool.getFileMD5(file));
            }

            if (isSave)
            {
                using (var stream = new FileStream(cacheText, FileMode.Create))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                    {
                        foreach (var item in globalStringText)
                        {
                            writer.Write(item.Key);
                            writer.Write(item.Value);
                        }
                    }
                }
                using (var stream = new FileStream(cacheRegex, FileMode.Create))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                    {
                        foreach (var item in globalStringRegex)
                        {
                            writer.Write(item.Value.Key);
                            writer.Write(item.Value.Value);
                        }
                    }
                }
                File.WriteAllText(cacheM, fileListMD5 + "|" + FileTool.getFileMD5(cacheText) + "|" + FileTool.getFileMD5(cacheRegex));
                return true;
            }
            if (!File.Exists(cacheM) || !File.Exists(cacheText) || !File.Exists(cacheRegex))
                return false;
            var md5 = File.ReadAllText(cacheM).Split(new char[] { '|' });
            if (md5.Length != 3 || md5[0] != fileListMD5 || md5[1] != FileTool.getFileMD5(cacheText) || md5[2] != FileTool.getFileMD5(cacheRegex))
                return false;
            using (var stream = new FileStream(cacheText, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    try
                    {
                        while (true)
                        {
                            var key = reader.ReadString();
                            globalStringText.Add(key, reader.ReadString());
                        }
                    }
                    catch { }
                }
            }
            using (var stream = new FileStream(cacheRegex, FileMode.Open))
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    try
                    {
                        while (true)
                        {
                            var key = reader.ReadString();
                            globalStringRegex.Add(new Regex(key, RegexOptions.Compiled), new KeyValuePair<string, string>(key, reader.ReadString()));
                        }
                    }
                    catch { }
                }
            }
            return globalStringText.Count > 0 || globalStringRegex.Count > 0;
        }

        private static string ConvertOriginalStringPattern(string text) => text.Replace("\r", "").Replace("\n", "")
            .Replace("\t", "").Trim(WhitespaceChars).ToUpper();

        private static void loadFile(string file)
        {
            string[] translationLines;
            try
            {
                translationLines = File.ReadAllLines(file);
            }
            catch (Exception e)
            {
                LBWmodifier.logger.LogWarning($"Failed to load {file} because {e.Message}. Skipping file...");
                return;
            }

            foreach (string translationLine in translationLines)
            {
                var textParts = translationLine.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (textParts.Length < 2)
                    continue;

                string original = textParts[0].Unescape().Trim(WhitespaceChars);
                string translation = textParts[1].Unescape().Trim(WhitespaceChars);
                if (string.IsNullOrEmpty(original))
                    continue;
                if (string.IsNullOrEmpty(translation))
                    continue;

                if (original.StartsWith("$", StringComparison.CurrentCulture))
                    globalStringRegex[new Regex(original.Substring(1), RegexOptions.Singleline | RegexOptions.Compiled)] = new KeyValuePair<string, string>(original.Substring(1), translation);
                else
                    globalStringText[ConvertOriginalStringPattern(original)] = translation;
            }
        }


        [HarmonyPatch(typeof(KagScript), nameof(KagScript.GetText))]
        [HarmonyPostfix]
        private static void KagScriptGetText(ref string __result)
        {
            if (TranslateText(__result, out var translate) == RESULT.SUCCESS)
                __result = translate;
        }

        [HarmonyPatch(typeof(Graphic), nameof(Graphic.SetVerticesDirty))]
        [HarmonyPrefix]
        private static void UITextSetText(object __instance)
        {
            if (__instance is Text)
            {
                var field = Traverse.Create(__instance).Field("m_Text");
                if (TranslateText(field.GetValue() as string, out var translate) == RESULT.SUCCESS)
                    field.SetValue(translate);
            }
        }

        public static void NGUITextWrapText(ref string text)
        {
            if (TranslateText(text, out var translate) == RESULT.SUCCESS)
                text = translate;
        }

        [HarmonyPatch(typeof(ScriptManager), nameof(ScriptManager.ReplaceCharaName), new Type[] { typeof(string) })]
        [HarmonyPrefix]
        private static void ReplaceCharaNameBefore(ref string text)
        {
            if (TranslateText(text, out var translate) == RESULT.SUCCESS)
                text = translate;
        }

        [HarmonyPatch(typeof(LanguageSource), nameof(LanguageSource.TryGetTranslation))]
        [HarmonyPrefix]
        private static void TryGetTranslation(ref string Term)
        {
            if (TranslateText(Term, out var translate) == RESULT.SUCCESS)
                Term = translate;
        }

        [HarmonyPatch(typeof(SceneEdit.SMenuItem), nameof(SceneEdit.SMenuItem.CountryReplace))]
        [HarmonyPostfix]
        private static void SMenuItem_CountryReplace(ref string __result)
        {
            if (TranslateText(__result, out var translate) == RESULT.SUCCESS)
                __result = translate;
        }

        public static void markTranslated(string text, bool translated)
        {
            if (translated)
                translatedStringText[text] = text;
            else
                untranslatedStringText.Remove(text);
        }

        public static string getOriginalFromTranslated(string translated)
        {
            return translatedStringText.TryGetValue(translated, out var result) ? result : translated;
        }

        public static bool isNeedTranslated(string text, bool skipNoJp)
        {
            if (string.IsNullOrEmpty(text) || translatedStringText.ContainsKey(text))
                return false;
            var pattern = ConvertOriginalStringPattern(text);
            if (untranslatedStringText.Contains(text))
                return true;
            return !loadedStringText.ContainsKey(pattern) && !globalStringText.ContainsKey(pattern) &&
                   (!skipNoJp || ContainsJapanese(pattern));
        }

        private static RESULT TranslateTextOrVoice(string original, out string text, bool isText)
        {
            text = null;
            if (string.IsNullOrEmpty(original) || !isInited())
                return RESULT.FAIL;
            // 是否已经被标记为已翻译，已翻译表示original已经是翻译以后的文本
            if (translatedStringText.ContainsKey(original))
            {
                text = original;
                return RESULT.TRANSLATED;
            }

            var pattern = ConvertOriginalStringPattern(original);

            // 从缓存中检查没有翻译成功的句子
            if (untranslatedStringText.Contains(pattern))
                return RESULT.FAIL;

            // 从缓存中加载已经翻译的句子
            if (loadedStringText.TryGetValue(pattern, out text))
                return RESULT.SUCCESS;

            if (TryTranslate(original, pattern, out text) || (isText && !ContainsJapanese(original)))
            {
                text = text ?? original;
                loadedStringText[pattern] = text;
                translatedStringText[text] = original;
                return RESULT.SUCCESS;
            }
            if (untranslatedStringText.Add(pattern))
            {
                Directory.CreateDirectory("LBWtranslation\\__DUMP__");
                if (isText)
                {
                    if (!hasWriteUntranslatedText)
                    {
                        hasWriteUntranslatedText = true;
                        File.AppendAllText("LBWtranslation\\__DUMP__\\UntranslatedText.txt", "原文\t待匹配");
                    }
                    File.AppendAllText("LBWtranslation\\__DUMP__\\UntranslatedText.txt",
                        $"\n{original.Escape()}\t{pattern.Escape()}");
                }
                else
                {
                    if (!hasWriteUntranslatedVoice)
                    {
                        hasWriteUntranslatedVoice = true;
                        File.AppendAllText("LBWtranslation\\__DUMP__\\UntranslatedVoice.txt", "音频名称(无后缀)");
                    }
                    File.AppendAllText("LBWtranslation\\__DUMP__\\UntranslatedVoice.txt",
                        $"\n{pattern.Escape()}");
                }
            }

            return RESULT.FAIL;
        }

        public static RESULT TranslateText(string original, out string text)
        {
            return TranslateTextOrVoice(original, out text, true);
        }

        public static RESULT TranslateVoice(string voiceName, out string text)
        {
            return TranslateTextOrVoice(voiceName, out text, false);
        }

        private static bool TryTranslate(string original, string pattern, out string result)
        {
            if (globalStringText.TryGetValue(pattern, out result))
                return true;

            foreach (var regexTranslation in globalStringRegex)
            {
                var m = regexTranslation.Key.Match(original);
                if (!m.Success)
                {
                    if (!(m = regexTranslation.Key.Match(pattern)).Success)
                        continue;
                }

                result = regexTranslation.Value.Value.Template(s =>
                {
                    string capturedString;
                    if (int.TryParse(s, out int index) && index < m.Groups.Count)
                        capturedString = m.Groups[index].Value;
                    else
                        capturedString = m.Groups[s].Value;
                    return globalStringText.TryGetValue(capturedString, out string groupTranslation)
                        ? groupTranslation
                        : capturedString;
                });
                return true;
            }

            return false;
        }

        private static bool ContainsJapanese(string text)
        {
            foreach (var c in text)
            {
                if (('\u3041' <= c && c <= '\u3096') || ('\u3099' <= c && c <= '\u309f') ||
                    ('\u30a1' <= c && c <= '\u30fa') || ('\u30fc' <= c && c <= '\u30ff') ||
                    ('\u31f0' <= c && c <= '\u31ff'))
                    return true;
            }

            return false;
        }

        public enum RESULT
        {
            SUCCESS,
            FAIL,
            TRANSLATED
        }
    }


    public class TextureReplace
    {
        [HarmonyPatch(typeof(FileSystemArchive), nameof(FileSystemArchive.IsExistentFile))]
        [HarmonyPatch(typeof(FileSystemWindows), nameof(FileSystemWindows.IsExistentFile))]
        [HarmonyPostfix]
        private static void IsExistentFileCheck(ref bool __result, string file_name)
        {
            if (file_name == null ||
                (!Path.GetExtension(file_name)?.Equals(".tex", StringComparison.InvariantCultureIgnoreCase) ?? true))
                return;

            if (!string.IsNullOrEmpty(file_name) &&
                TextureReplaceManager.ReplacementExists(Path.GetFileNameWithoutExtension(file_name)))
                __result = true;
        }

        [HarmonyPatch(typeof(ImportCM), nameof(ImportCM.LoadTexture))]
        [HarmonyPrefix]
        private static bool LoadTexture(ref TextureResource __result, AFileSystemBase f_fileSystem,
            string f_strFileName, bool usePoolBuffer)
        {
            var fileName = Path.GetFileNameWithoutExtension(f_strFileName);
            if (string.IsNullOrEmpty(fileName))
                return true;

            var newTex = TextureReplaceManager.GetReplacementTextureBytes(fileName, "tex");

            if (newTex == null)
                return true;

            __result = new TextureResource(1, 1, TextureFormat.ARGB32, __result?.uvRects, newTex);

            return false;
        }

        [HarmonyPatch(typeof(UIWidget), nameof(UIWidget.mainTexture), MethodType.Getter)]
        [HarmonyPatch(typeof(UI2DSprite), nameof(UI2DSprite.mainTexture), MethodType.Getter)]
        [HarmonyPostfix]
        private static void GetMainTexturePost(UIWidget __instance, ref Texture __result)
        {
            Texture tex = __result;

            if (tex == null || string.IsNullOrEmpty(tex.name) || tex.name.StartsWith("translation_") ||
                tex.name == "Font Texture")
                return;

            var newData = TextureReplaceManager.GetReplacementTextureBytes(tex.name, __instance.GetType().Name);

            if (newData == null)
                return;

            if (tex is Texture2D tex2d)
            {
#if DATA4
                __result = tex2d = duplicateTexture(tex2d);
                ImageConversion.LoadImage(tex2d, newData);
                switch (__instance)
                {
                    case UI2DSprite sprite:
                        sprite.sprite2D = Sprite.Create(tex2d, new Rect(0, 0, tex2d.width, tex2d.height), new Vector2(0.5f, 0.5f));
                        break;
                    default:
                        __instance.material.mainTexture = tex2d;
                        break;
                }
#else
                tex2d.LoadImage(new byte[0]);
                tex2d.LoadImage(newData);
#endif
                tex2d.name = $"translation_{tex2d}";
            }
        }

        [HarmonyPatch(typeof(UITexture), nameof(UITexture.mainTexture), MethodType.Getter)]
        [HarmonyPostfix]
        private static void GetMainTexturePostTex(UITexture __instance, ref Texture __result, ref Texture ___mTexture)
        {
            var tex = ___mTexture ?? __instance.material?.mainTexture;

            if (tex == null || string.IsNullOrEmpty(tex.name) || tex.name.StartsWith("translation_"))
                return;

            var newData = TextureReplaceManager.GetReplacementTextureBytes(tex.name, "UITexture");

            if (newData == null)
                return;

            if (tex is Texture2D tex2d)
            {
#if DATA4
                __result = tex2d = duplicateTexture(tex2d);
                ImageConversion.LoadImage(tex2d, newData);
                if (___mTexture != null)
                    ___mTexture = tex2d;
                else
                    __instance.material.mainTexture = tex2d;
#else
                tex2d.LoadImage(new byte[0]);
                tex2d.LoadImage(newData);
#endif
                tex2d.name = $"translation_{tex2d}";
            }
        }

        [HarmonyPatch(typeof(Image), nameof(Image.sprite), MethodType.Setter)]
        [HarmonyPrefix]
        private static void SetSprite(ref Sprite value)
        {
            if (value == null || value.texture == null || string.IsNullOrEmpty(value.texture.name) ||
                value.texture.name.StartsWith("translation_"))
                return;

            var newData = TextureReplaceManager.GetReplacementTextureBytes(value.texture.name, "Image");

            if (newData == null)
                return;

#if DATA4
            ImageConversion.LoadImage(value.texture, newData);
#else
            value.texture.LoadImage(new byte[0]);
            value.texture.LoadImage(newData);
#endif
            value.texture.name = $"translation_{value.texture.name}";
        }

        [HarmonyPatch(typeof(MaskableGraphic), "OnEnable")]
        [HarmonyPrefix]
        private static void OnMaskableGraphicEnable(MaskableGraphic __instance)
        {
            // Force replacement of Images
            if (!(__instance is Image img) || img.sprite == null)
                return;
            var tmp = img.sprite;
            img.sprite = tmp;
        }

        private static Texture2D duplicateTexture(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.Default,
                        RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }
    }

    public static class Extensions
    {
        public static string Template(this string template, Func<string, string> templateFunc)
        {
            var sb = new StringBuilder(template.Length);
            var sbTemplate = new StringBuilder();

            bool insideTemplate = false;
            bool bracedTemplate = false;
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                switch (c)
                {
                    case '\\':
                        if (i + 1 < template.Length && template[i + 1] == '$')
                        {
                            sb.Append('$');
                            i++;
                            continue;
                        }

                        break;
                    case '$':
                        if (insideTemplate)
                        {
                            sb.Append(templateFunc(sbTemplate.ToString()));
                            sbTemplate.Length = 0;
                        }

                        insideTemplate = true;
                        continue;
                    case '{':
                        if (insideTemplate)
                        {
                            bracedTemplate = true;
                            continue;
                        }

                        break;
                    case '}':
                        if (insideTemplate && sbTemplate.Length > 0)
                        {
                            sb.Append(templateFunc(sbTemplate.ToString()));
                            sbTemplate.Length = 0;
                            insideTemplate = false;
                            bracedTemplate = false;
                            continue;
                        }

                        break;
                }

                if (insideTemplate && !bracedTemplate && !char.IsDigit(c))
                {
                    sb.Append(templateFunc(sbTemplate.ToString()));
                    sbTemplate.Length = 0;
                    insideTemplate = false;
                }

                if (insideTemplate)
                    sbTemplate.Append(c);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        public static string Escape(this string txt)
        {
            var stringBuilder = new StringBuilder(txt.Length + 2);
            foreach (char c in txt)
                switch (c)
                {
                    case '\0':
                        stringBuilder.Append(@"\0");
                        break;
                    case '\a':
                        stringBuilder.Append(@"\a");
                        break;
                    case '\b':
                        stringBuilder.Append(@"\b");
                        break;
                    case '\t':
                        stringBuilder.Append(@"\t");
                        break;
                    case '\n':
                        stringBuilder.Append(@"\n");
                        break;
                    case '\v':
                        stringBuilder.Append(@"\v");
                        break;
                    case '\f':
                        stringBuilder.Append(@"\f");
                        break;
                    case '\r':
                        stringBuilder.Append(@"\r");
                        break;
                    case '\'':
                        stringBuilder.Append(@"\'");
                        break;
                    case '\\':
                        stringBuilder.Append(@"\");
                        break;
                    case '\"':
                        stringBuilder.Append(@"\""");
                        break;
                    default:
                        stringBuilder.Append(c);
                        break;
                }

            return stringBuilder.ToString();
        }

        public static string Unescape(this string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return txt;
            var stringBuilder = new StringBuilder(txt.Length);
            for (int i = 0; i < txt.Length;)
            {
                int num = txt.IndexOf('\\', i);
                if (num < 0 || num == txt.Length - 1)
                    num = txt.Length;
                stringBuilder.Append(txt, i, num - i);
                if (num >= txt.Length)
                    break;
                char c = txt[num + 1];
                switch (c)
                {
                    case '0':
                        stringBuilder.Append('\0');
                        break;
                    case 'a':
                        stringBuilder.Append('\a');
                        break;
                    case 'b':
                        stringBuilder.Append('\b');
                        break;
                    case 't':
                        stringBuilder.Append('\t');
                        break;
                    case 'n':
                        stringBuilder.Append('\n');
                        break;
                    case 'v':
                        stringBuilder.Append('\v');
                        break;
                    case 'f':
                        stringBuilder.Append('\f');
                        break;
                    case 'r':
                        stringBuilder.Append('\r');
                        break;
                    case '\'':
                        stringBuilder.Append('\'');
                        break;
                    case '\"':
                        stringBuilder.Append('\"');
                        break;
                    case '\\':
                        stringBuilder.Append('\\');
                        break;
                    default:
                        stringBuilder.Append('\\').Append(c);
                        break;
                }

                i = num + 2;
            }

            return stringBuilder.ToString();
        }

        public static ulong KnuthHash(this string read)
        {
            var hashedValue = 3074457345618258791ul;
            foreach (var t in read)
            {
                hashedValue += t;
                hashedValue *= 3074457345618258799ul;
            }

            return hashedValue;
        }
    }

    public class TextureReplacement
    {
        public TextureReplacement(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
        }

        public string Name { get; }

        public string FullPath { get; }

        public byte[] Data { get; set; }

        public void Load()
        {
            using (var s = (!File.Exists(FullPath) ? null : File.OpenRead(FullPath)))
            {
                Data = new byte[s.Length];
                s.Read(Data, 0, Data.Length);
            }
        }
    }

    public static class TextureReplaceManager
    {
        private static readonly HashSet<string> dumpedItems = new HashSet<string>();
        private static readonly HashSet<string> missingTextures = new HashSet<string>();

        private static readonly LinkedList<TextureReplacement> texReplacementCache =
            new LinkedList<TextureReplacement>();

        private static readonly Dictionary<string, LinkedListNode<TextureReplacement>> texReplacementLookup =
            new Dictionary<string, LinkedListNode<TextureReplacement>>(StringComparer.InvariantCultureIgnoreCase);

        private static readonly Dictionary<string, string> textureReplacements =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);


        public static void initialize()
        {
            foreach (var file in Directory.GetFiles("LBWtranslation\\Texture", "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);

                if (textureReplacements.ContainsKey(name))
                {
                    LBWmodifier.logger.LogWarning(
                        $"Found duplicate replacements for texture \"{name}\". Please name all your textures uniquely. If there are name collisions, name them by hash.");
                    continue;
                }

                textureReplacements[name] = file;
            }
        }

        public static bool ReplacementExists(string texName)
        {
            return textureReplacements.ContainsKey(texName);
        }

        public static byte[] GetReplacementTextureBytes(string texName, string tag = null)
        {
            return GetReplacement(texName, tag)?.Data;
        }

        private static TextureReplacement GetReplacement(string texName, string tag = null)
        {
            var hash = $"{texName}:{tag}".KnuthHash().ToString("X16");
            string[] lookupNames =
            {
                texName,
                hash,
                $"{texName}@{SceneManager.GetActiveScene().buildIndex}",
                $"{hash}@{SceneManager.GetActiveScene().buildIndex}"
            };

            foreach (var lookupName in lookupNames)
            {
                if (!textureReplacements.ContainsKey(lookupName))
                    continue;
                return LoadReplacement(lookupName);
            }

            return null;
        }

        private static TextureReplacement LoadReplacement(string name)
        {
            if (texReplacementLookup.TryGetValue(name, out var node))
            {
                texReplacementCache.Remove(node);
                texReplacementCache.AddFirst(node);
                return node.Value;
            }

            if (texReplacementLookup.Count >= 100)
            {
                node = texReplacementCache.Last;
                texReplacementCache.RemoveLast();
                texReplacementLookup.Remove(node.Value.Name);
            }

            try
            {
                var newNode = new TextureReplacement(name, textureReplacements[name]);
                newNode.Load();
                node = texReplacementCache.AddFirst(newNode);
                texReplacementLookup.Add(name, node);
                return newNode;
            }
            catch (Exception e)
            {
                LBWmodifier.logger.LogError($"Failed to load texture \"{name}\" because: {e.Message}");
                textureReplacements.Remove(name);
                return null;
            }
        }
    }
}