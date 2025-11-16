using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using static LBWmodifier.SongLyrics;

namespace LBWmodifier.Loader
{
#if TRANSLATOR
    [BepInPlugin("lbwnb.translation", "LBWmodifier-Translator", "4.1")]
#endif
    public class Translator : LBWmodifier
    {
        private static Stopwatch turnOffTs = Stopwatch.StartNew();

        private static Harmony harmony0;

        public static ConfigEntry<bool> 开启字幕;
        public static ConfigEntry<bool> 开启歌词;
        public static ConfigEntry<bool> 开启文字翻译;

        public static ConfigEntry<int> 字幕文字大小设置1;
        public static ConfigEntry<bool> 文字使用粗体1;
        public static ConfigEntry<SubtitleColor> 字幕文字颜色1;
        public static ConfigEntry<SubtitleColor> 字幕文字阴影颜色1;
        public static ConfigEntry<bool> 显示女仆名字1;
        public static ConfigEntry<SubtitleColor> 字幕女仆名字颜色1;
        public static ConfigEntry<SubtitleAlignment> 字幕位置对齐1;
        public static ConfigEntry<int> 字幕位置竖向距离1;
        public static ConfigEntry<int> 字幕位置横向距离1;

        public static ConfigEntry<int> 字幕文字大小设置2;
        public static ConfigEntry<bool> 歌词显示原文;
        public static ConfigEntry<bool> 文字使用粗体2;
        public static ConfigEntry<SubtitleColor> 字幕文字颜色2;
        public static ConfigEntry<SubtitleColor> 字幕文字阴影颜色2;
        public static ConfigEntry<bool> 显示歌词名称2;
        public static ConfigEntry<SubtitleColor> 字幕歌词名称颜色2;
        public static ConfigEntry<SubtitleAlignment> 字幕位置对齐2;
        public static ConfigEntry<int> 字幕位置竖向距离2;
        public static ConfigEntry<int> 字幕位置横向距离2;

        public override void Awake()
        {
            base.Awake();

            开启文字翻译 = Config.Bind<bool>("1. 场景", "开启文字翻译", true, new ConfigDescription("开启后可能会和其它翻译插件冲突", null, new ConfigurationManagerAttributes { Order = 30 }));
            开启字幕 = Config.Bind<bool>("1. 场景", "开启字幕", true, new ConfigDescription("文字会显示在屏幕顶部居中位置", null, new ConfigurationManagerAttributes { Order = 20 }));
            开启歌词 = Config.Bind<bool>("1. 场景", "开启歌词", true, new ConfigDescription("文字会显示在屏幕顶部居中位置", null, new ConfigurationManagerAttributes { Order = 10 }));

            字幕文字大小设置1 = Config.Bind<int>("2.1. 字幕设置", "设置字幕大小", 100, new ConfigDescription("100就是原始字幕大小，改成80就是原来80%的大小，也可以改大", null, new ConfigurationManagerAttributes { Order = 90 }));
            文字使用粗体1 = Config.Bind<bool>("2.1. 字幕设置", "文字使用粗体", false, new ConfigDescription("文字变粗", null, new ConfigurationManagerAttributes { Order = 80 }));
            字幕文字颜色1 = Config.Bind<SubtitleColor>("2.1. 字幕设置", "字幕文字颜色", SubtitleColor.白色, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 70 }));
            字幕文字阴影颜色1 = Config.Bind<SubtitleColor>("2.1. 字幕设置", "字幕文字阴影颜色", SubtitleColor.黑色, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 60 }));
            显示女仆名字1 = Config.Bind<bool>("2.1. 字幕设置", "字幕显示女仆名字", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 50 }));
            字幕女仆名字颜色1 = Config.Bind<SubtitleColor>("2.1. 字幕设置", "字幕的女仆名字颜色", SubtitleColor.淡蓝色, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 40 }));
            字幕位置对齐1 = Config.Bind<SubtitleAlignment>("2.1. 字幕设置", "字幕位置对齐", SubtitleAlignment.顶部居中, new ConfigDescription("字幕显示的位置", null, new ConfigurationManagerAttributes { Order = 30 }));
            字幕位置竖向距离1 = Config.Bind<int>("2.1. 字幕设置", "字幕位置竖向距离", 8, new ConfigDescription("如果对齐方式是顶部居中，那么就是文本顶部距离屏幕顶部的距离", null, new ConfigurationManagerAttributes { Order = 20 }));
            字幕位置横向距离1 = Config.Bind<int>("2.1. 字幕设置", "字幕位置横向距离", 100, new ConfigDescription("如果对齐方式是顶部左端，那么就是文本左端距离屏幕左端的距离", null, new ConfigurationManagerAttributes { Order = 10 }));

            字幕文字大小设置2 = Config.Bind<int>("2.2. 歌词设置", "设置歌词大小", 100, new ConfigDescription("100就是原始歌词大小，改成80就是原来80%的大小，也可以改大", null, new ConfigurationManagerAttributes { Order = 100 }));
            歌词显示原文 = Config.Bind<bool>("2.2. 歌词设置", "歌词显示原文", false, new ConfigDescription("歌词显示原文", null, new ConfigurationManagerAttributes { Order = 90 }));
            文字使用粗体2 = Config.Bind<bool>("2.2. 歌词设置", "歌词文字使用粗体", false, new ConfigDescription("歌词文字变粗", null, new ConfigurationManagerAttributes { Order = 80 }));
            字幕文字颜色2 = Config.Bind<SubtitleColor>("2.2. 歌词设置", "歌词文字颜色", SubtitleColor.白色, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 70 }));
            字幕文字阴影颜色2 = Config.Bind<SubtitleColor>("2.2. 歌词设置", "歌词文字阴影颜色", SubtitleColor.黑色, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 60 }));
            显示歌词名称2 = Config.Bind<bool>("2.2. 歌词设置", "歌词显示歌词名称", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 50 }));
            字幕歌词名称颜色2 = Config.Bind<SubtitleColor>("2.2. 歌词设置", "歌词的歌词名称颜色", SubtitleColor.淡蓝色, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 40 }));
            字幕位置对齐2 = Config.Bind<SubtitleAlignment>("2.2. 歌词设置", "歌词位置对齐", SubtitleAlignment.顶部居中, new ConfigDescription("歌词显示的位置", null, new ConfigurationManagerAttributes { Order = 30 }));
            字幕位置竖向距离2 = Config.Bind<int>("2.2. 歌词设置", "歌词位置竖向距离", 8, new ConfigDescription("如果对齐方式是顶部居中，那么就是文本顶部距离屏幕顶部的距离", null, new ConfigurationManagerAttributes { Order = 20 }));
            字幕位置横向距离2 = Config.Bind<int>("2.2. 歌词设置", "歌词位置横向距离", 100, new ConfigDescription("如果对齐方式是顶部左端，那么就是文本左端距离屏幕左端的距离", null, new ConfigurationManagerAttributes { Order = 10 }));

            if (开启文字翻译.Value)
            {
                Translation.initialize();
            }
            开启文字翻译.SettingChanged += (o, e) =>
            {
                if (开启文字翻译.Value)
                    Translation.initialize();
                else
                    Translation.unload();
            };

            harmony0 = Harmony.CreateAndPatchAll(typeof(AudioAndTextTrace));

            DanceMainHooker.initialize();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (turnOffTs.Elapsed.TotalSeconds >= 60)
            {
                turnOffTs.Reset();
                var xuatAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "XUnity.AutoTranslator.Plugin.Core");
                if (xuatAssembly != null)
                {
                    var LanguageHelper = xuatAssembly.GetType("XUnity.AutoTranslator.Plugin.Core.Utilities.LanguageHelper");
                    LBWextention.Assert(LanguageHelper == null, "LanguageHelper == null");
                    var IsTranslatable = LanguageHelper.GetMethod("IsTranslatable");
                    LBWextention.Assert(IsTranslatable == null, "IsTranslatable == null");

                    var harmony = new Harmony("LBWmodifier.Patch.AutoTranslator");
                    harmony.Patch(IsTranslatable, new HarmonyMethod(typeof(Translator).GetMethod("AutoTranslator_FixTranslate_Prefix", BindingFlags.Static | BindingFlags.NonPublic)));
                }
            }
        }

        public void OnDestroy()
        {
            harmony0.UnpatchSelf();
            Translation.unload();
            Subtitle.Hide();
            DanceMainHooker.unload();
        }

        private static bool AutoTranslator_FixTranslate_Prefix(string text, ref bool __result)
        {
            if (开启文字翻译.Value && !Translation.isNeedTranslated(text, true))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
