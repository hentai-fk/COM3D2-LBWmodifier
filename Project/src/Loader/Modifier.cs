using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using MaidStatus;
using PrivateMaidMode;
using Schedule;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Teikokusou;
using UnityEngine;
using static FacilityDataTable;
using static LBWmodifier.SongLyrics;
using static ScenarioData;

namespace LBWmodifier.Loader
{
#if MODIFIER
    [BepInPlugin("lbwnb.modification", "LBWmodifier-Modifier", "4.1")]
#endif
    public class Modifier : LBWmodifier
    {
        private static Stopwatch turnOffSep = Stopwatch.StartNew();
        private static Stopwatch frameUpdate = Stopwatch.StartNew();

        private static Harmony harmony0;
        private static Harmony harmony1;
        private static Harmony harmony2;

        private static HashSet<int> privateModeEnter = new HashSet<int>();
        private static HashSet<int> lifeModeEnter = new HashSet<int>();
        private static HashSet<int> CheckScenarioEnter = new HashSet<int>();

        public static ConfigEntry<bool> 开启NTR事件;
        public static ConfigEntry<bool> 解锁日程表事件;
        public static ConfigEntry<bool> 强制允许CR身体;
        public static ConfigEntry<bool> 解锁所有回忆事件;
        public static ConfigEntry<bool> 画面帧率无限制;

        public static ConfigEntry<bool> 所有女仆私有化;
        public static ConfigEntry<bool> 使用2D声音;

        public static ConfigEntry<bool> 女仆语音随机播放;
        public static ConfigEntry<long> 语音娇喘等待时间;
        public static ConfigEntry<long> 多人语音播放时间;
        public static ConfigEntry<bool> 多人语音强制播放;


        public override void Awake()
        {
            base.Awake();

            所有女仆私有化 = Config.Bind<bool>("1. 女仆", "所有女仆私有化", true, new ConfigDescription("CR身体也可以在私人模式里选择了", null, new ConfigurationManagerAttributes { Order = 20 }));
            使用2D声音 = Config.Bind<bool>("1. 女仆", "使用2D声音", false, new ConfigDescription("原版的声音播放受空间影响，开启后将强制2D播放，双声道音频体验提升", null, new ConfigurationManagerAttributes { Order = 10 }));

            开启NTR事件 = Config.Bind<bool>("2. 场景", "开启NTR事件", false, new ConfigDescription("修改NTR属性，不确定存档后是否会导致存档也具有NTR属性。CheatMenu插件可以重新加上NTR锁", null, new ConfigurationManagerAttributes { Order = 50 }));
            解锁日程表事件 = Config.Bind<bool>("2. 场景", "解锁日程表事件", false, new ConfigDescription("日程表就是女仆每天的工作内容，里面可以选择H事件，解锁后就可以随便选择事件了", null, new ConfigurationManagerAttributes { Order = 40 }));
            强制允许CR身体 = Config.Bind<bool>("2. 场景", "强制允许CR身体", false, new ConfigDescription("日程表、活动事件有部分事件当女仆使用CR身体后将会无法显示事件，注意强制进行事件可能造成游戏无法继续运行", null, new ConfigurationManagerAttributes { Order = 30 }));
            解锁所有回忆事件 = Config.Bind<bool>("2. 场景", "解锁所有回忆事件", false, new ConfigDescription("解锁回忆事件，有些事件禁止CR身体，默认强制开启事件，如果有问题可能会无法正常游戏", null, new ConfigurationManagerAttributes { Order = 20 }));
            画面帧率无限制 = Config.Bind<bool>("2. 场景", "画面帧率无限制", false, new ConfigDescription("Unlock FPS，但是作用不大，实际还会受电脑性能限制，渲染人物稍微复杂一点就掉帧率", null, new ConfigurationManagerAttributes { Order = 10 }));

            女仆语音随机播放 = Config.Bind<bool>("3. 随机语音", "女仆语音随机播放", false, new ConfigDescription("部分后宫场景的语音播放受插入次数影响", null, new ConfigurationManagerAttributes { Order = 40 }));
            语音娇喘等待时间 = Config.Bind<long>("3. 随机语音", "语音娇喘等待时间", -1, new ConfigDescription("小于0表示自动等待一个娇喘循环时间，1000表示等待1秒，单位是毫秒", null, new ConfigurationManagerAttributes { Order = 30 }));
            多人语音播放时间 = Config.Bind<long>("3. 随机语音", "多人语音播放时间", -1, new ConfigDescription("小于0表示别的女仆娇喘等待时间到了就播放语音，1000表示等待上一个女仆语音播放1秒后再播放当前语音，单位是毫秒", null, new ConfigurationManagerAttributes { Order = 20 }));
            多人语音强制播放 = Config.Bind<bool>("3. 随机语音", "多人语音强制播放", false, new ConfigDescription("当多人娇喘无法播放时是否需要强制播放单独语音", null, new ConfigurationManagerAttributes { Order = 10 }));

            harmony0 = Harmony.CreateAndPatchAll(typeof(Modifier));
            harmony1 = Harmony.CreateAndPatchAll(typeof(Copy_all_maids_in_private_mode));
            harmony2 = Harmony.CreateAndPatchAll(typeof(Copy_MemoriesModeUnlock));

            RandomVoice.initialize();

            // 修补可能不存在的函数
            var EnableNightWorkNewBodyCheckOnly = AccessTools.Method("Schedule.ScheduleAPI:EnableNightWorkNewBodyCheckOnly");
            if (EnableNightWorkNewBodyCheckOnly != null)
            {
                harmony0.Patch(EnableNightWorkNewBodyCheckOnly, null, new HarmonyMethod(typeof(Modifier).GetMethod(nameof(Modifier.EnableNightWork))));
            }
            var isEnabled = AccessTools.Method("FreeModeItemEveryday:isEnabled");
            if (isEnabled != null)
            {
                harmony2.Patch(isEnabled, null, new HarmonyMethod(typeof(Copy_MemoriesModeUnlock).GetMethod(nameof(Copy_MemoriesModeUnlock.FreeModeItemEverydayIsEnabled))));
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (turnOffSep.Elapsed.TotalSeconds >= 60)
            {
                turnOffSep.Reset();
                turnOffHarmony(harmony1);
                turnOffHarmony(harmony2);
            }
            if (frameUpdate.ElapsedMilliseconds >= 1000)
            {
                frameUpdate.Reset();
                frameUpdate.Start();
                UnityEngine.Application.targetFrameRate = -1;
            }
        }

        public void OnDestroy()
        {
            harmony0.UnpatchSelf();
            harmony1.UnpatchSelf();
            harmony2.UnpatchSelf();
            RandomVoice.unload();
        }

        private void turnOffHarmony(Harmony self)
        {
            foreach (var patched in Harmony.GetAllPatchedMethods().ToArray())
            {
                if (!self.GetPatchedMethods().Contains(patched))
                    continue;
                foreach (var owner in Harmony.GetPatchInfo(patched).Owners)
                {
                    if (owner != self.Id)
                    {
                        Harmony.UnpatchID(owner);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(PlayerStatus.Status), nameof(PlayerStatus.Status.lockNTRPlay), MethodType.Getter)]
        [HarmonyPostfix]
        private static void get_lockNTRPlay(ref bool __result)
        {
            if (开启NTR事件.Value)
                __result = false;
        }

        [HarmonyPatch(typeof(Yotogis.Skill.Data), MethodType.Constructor, new Type[] { typeof(CsvParser), typeof(CsvParser), typeof(int), typeof(Dictionary<int, int[]>) })]
        [HarmonyPostfix]
        static void 告白技能应该属于NTR才对(Yotogis.Skill.Data __instance)
        {
            if (__instance.name.StartsWith("【告白】"))
            {
                Traverse.Create(__instance).Field("specialConditionType").SetValue(Yotogis.Skill.Data.SpecialConditionType.Confess);
                if (__instance.getcondition_data == null)
                    Traverse.Create(__instance).Field("getcondition_data").SetValue(new Yotogis.Skill.Data.GetConditionData());
                __instance.getcondition_data.yotogi_class = YotogiClass.GetData("Kokuhakuplay");
                __instance.getcondition_data.yotogi_class_level = 0;
            }
        }


        [HarmonyPatch(typeof(ScheduleCSVData.ScheduleBase), nameof(ScheduleCSVData.ScheduleBase.CheckMainHeroineBodyTypeMatch), new Type[] { typeof(HashSet<string>), typeof(Maid) })]
        [HarmonyPostfix]
        public static void CheckMainHeroineBodyTypeMatch(ref bool __result)
        {
            if (强制允许CR身体.Value)
                __result = true;
        }


        [HarmonyPatch(typeof(FacilityDefaultData), MethodType.Constructor, new Type[] { typeof(CsvParser), typeof(Dictionary<int, int[]>), typeof(Dictionary<int, List<int>>), typeof(HashSet<int>), typeof(int) })]
        [HarmonyPostfix]
        public static void FacilityDefaultDataContructor(FacilityDefaultData __instance)
        {
            if (强制允许CR身体.Value)
                __instance.workData.isNewBodyBlock = false;
        }


        [HarmonyPatch(typeof(KaraokeDataManager.FoodData), nameof(KaraokeDataManager.FoodData.isNewBodyBlock), MethodType.Getter)]
        [HarmonyPostfix]
        public static void KaraokeDataManagerFoodDataIsNewBodyBlock(ref bool __result)
        {
            if (强制允许CR身体.Value)
                __result = false;
        }



        [HarmonyPatch(typeof(PrivateModeMgr), nameof(PrivateModeMgr.LoadPrivateMaid))]
        [HarmonyPrefix]
        public static void SetPrivateMaidPrefix()
        {
            if (所有女仆私有化.Value)
                privateModeEnter.Add(Thread.CurrentThread.ManagedThreadId);
        }

        [HarmonyPatch(typeof(PrivateModeMgr), nameof(PrivateModeMgr.LoadPrivateMaid))]
        [HarmonyPostfix]
        public static void SetPrivateMaidPostfix()
        {
            if (所有女仆私有化.Value)
                privateModeEnter.Remove(Thread.CurrentThread.ManagedThreadId);
        }



        [HarmonyPatch(typeof(FreeModeItemLifeMode), "is_enabled", MethodType.Getter)]
        [HarmonyPrefix]
        public static void FreeModeItemLifeMode_is_enabled_Prefix(FreeModeItemLifeMode __instance)
        {
            if (解锁所有回忆事件.Value)
                lifeModeEnter.Add(Thread.CurrentThread.ManagedThreadId);
        }

        [HarmonyPatch(typeof(FreeModeItemLifeMode), "is_enabled", MethodType.Getter)]
        [HarmonyPostfix]
        public static void FreeModeItemLifeMode_is_enabled_Postfix()
        {
            if (解锁所有回忆事件.Value)
                lifeModeEnter.Remove(Thread.CurrentThread.ManagedThreadId);
        }

        [HarmonyPatch(typeof(EmpireLifeModeData.Data), MethodType.Constructor, new Type[] { typeof(int), typeof(CsvParser) })]
        [HarmonyPostfix]
        public static void EmpireLifeModeData_Data(EmpireLifeModeData.Data __instance)
        {
            if (解锁所有回忆事件.Value && 强制允许CR身体.Value)
                Traverse.Create(__instance).Field("dataNewBodyBlock").SetValue(false);
        }

        [HarmonyPatch(typeof(EmpireLifeModeManager), "GetScenarioExecuteCount")]
        [HarmonyPostfix]
        public static void GetScenarioExecuteCount_PostFix(ref int __result)
        {
            if (解锁所有回忆事件.Value)
            {
                if (__result <= 0 && lifeModeEnter.Contains(Thread.CurrentThread.ManagedThreadId))
                    __result = 1;
            }
        }


        [HarmonyPatch(typeof(TeikokusouDatabase.PlayModeRoomData), nameof(TeikokusouDatabase.PlayModeRoomData.CheckTargetMaid))]
        [HarmonyPostfix]
        private static void TeikokusouDatabase_CheckTargetMaid(Maid maid, ref bool __result)
        {
            if (解锁所有回忆事件.Value && maid != null && maid.status.heroineType != HeroineType.Sub)
                __result = true;
        }



        // FIX: ScheduleAPI.EnableNightWorkNewBodyCheckOnly
        [HarmonyPatch(typeof(Schedule.ScheduleAPI), nameof(ScheduleAPI.EnableNightWork))]
        [HarmonyPostfix]
        public static void EnableNightWork(ref bool __result)
        {
            if (解锁日程表事件.Value)
                __result = true;
        }

        [HarmonyPatch(typeof(Schedule.ScheduleAPI), nameof(ScheduleAPI.VisibleNightWork))]
        [HarmonyPrefix]
        public static void VisibleNightWork(ref bool __result)
        {
            if (解锁日程表事件.Value && 强制允许CR身体.Value)
            {
                foreach (var item in Traverse.Create(typeof(ScheduleCSVData)).Field("YotogiDataDic").GetValue() as Dictionary<int, ScheduleCSVData.Yotogi>)
                {
                    item.Value.isNewBodyBlock = false;
                    item.Value.isCheckBodyType = false;
                }
            }
        }


        [HarmonyPatch(typeof(ScenarioData), nameof(ScenarioData.CheckPlayableCondition), new Type[] { typeof(PlayableCondition), typeof(bool) })]
        [HarmonyPrefix]
        public static void CheckPlayableConditionPrefix()
        {
            if (强制允许CR身体.Value)
                CheckScenarioEnter.Add(Thread.CurrentThread.ManagedThreadId);
        }

        [HarmonyPatch(typeof(ScenarioData), nameof(ScenarioData.CheckPlayableCondition), new Type[] { typeof(PlayableCondition), typeof(bool) })]
        [HarmonyPostfix]
        public static void CheckPlayableConditionPostfix(ref bool __result)
        {
            if (强制允许CR身体.Value)
                CheckScenarioEnter.Remove(Thread.CurrentThread.ManagedThreadId);
        }

        [HarmonyPatch(typeof(Maid), nameof(Maid.IsCrcBody), MethodType.Getter)]
        [HarmonyPostfix]
        public static void MaidIsCrcBody(ref bool __result)
        {
            if (强制允许CR身体.Value && CheckScenarioEnter.Contains(Thread.CurrentThread.ManagedThreadId))
                __result = false;
        }



        [HarmonyPatch(typeof(AudioSourceMgr), nameof(AudioSourceMgr.Play))]
        [HarmonyPatch(typeof(AudioSourceMgr), nameof(AudioSourceMgr.PlayOneShot))]
        [HarmonyPrefix]
        private static void PlayGlobalVoice(AudioSourceMgr __instance)
        {
            if (!使用2D声音.Value || __instance.SoundType < AudioSourceMgr.Type.Voice)
                return;
            Traverse.Create(__instance).Field("m_bThreeD").SetValue(true);
            Traverse.Create(__instance).Field("m_bThreeDNow").SetValue(true);
            __instance.ApplyThreeD();
        }

        [HarmonyPatch(typeof(SoundMgr), nameof(SoundMgr.GetThreeD))]
        [HarmonyPostfix]
        private static void SoundMgrGetThreeD(AudioSourceMgr.Type f_eType, ref bool __result)
        {
            if (!使用2D声音.Value || f_eType < AudioSourceMgr.Type.Voice)
                return;
            __result = false;
        }


        // 自动跳过标题画面
        [HarmonyPatch(typeof(SceneWarning), "GetAnyMouseAndKey")]
        [HarmonyPatch(typeof(SceneLogo), "GetAnyMouseAndKey")]
        [HarmonyPostfix]
        public static void HookLoadingEnd(ref bool __result)
        {
            __result = true;
        }


        static class Copy_all_maids_in_private_mode
        {
            // 和all_maids_in_private_mode冲突
            [HarmonyPatch(typeof(uGUICharacterSelectManager), nameof(uGUICharacterSelectManager.PrivateMaidList))]
            [HarmonyPrefix]
            public static bool PrivateMaidList(List<Maid> drawList)
            {
                if (所有女仆私有化.Value)
                {
                    drawList.Clear();
                    CharacterMgr characterMgr = GameMain.Instance.CharacterMgr;
                    foreach (Maid maid in characterMgr.GetStockMaidList())
                    {
                        if (maid.status.heroineType != HeroineType.Sub)
                            drawList.Add(maid);
                    }
                    return false;
                }
                return true;
            }

            [HarmonyPatch(typeof(PrivateModeMgr), nameof(PrivateModeMgr.SetPrivateMaid))]
            [HarmonyPrefix]
            public static bool SetPrivateMaid(Maid maid)
            {
                if (所有女仆私有化.Value && maid == null && privateModeEnter.Contains(Thread.CurrentThread.ManagedThreadId))
                    return false;
                return true;
            }
        }

        static class Copy_MemoriesModeUnlock
        {
            // 和MemoriesModeUnlock冲突
            [HarmonyPatch(typeof(FreeModeItemEveryday), nameof(FreeModeItemEveryday.IsEnabledFlag))]
            [HarmonyPostfix]
            public static void FreeModeItemEverydayIsEnabledFlag(ref bool __result)
            {
                if (解锁所有回忆事件.Value)
                    __result = true;
            }

            // FIX: FreeModeItemEveryday.isEnabled
            public static void FreeModeItemEverydayIsEnabled(ref bool __result)
            {
                if (解锁所有回忆事件.Value && 强制允许CR身体.Value)
                    __result = true;
            }

            [HarmonyPatch(typeof(FreeModeItemVip), nameof(FreeModeItemVip.CreateItemVipList))]
            [HarmonyPostfix]
            public static void FreeModeItemVipCreateItemVipList(List<FreeModeItemVip> __result)
            {
                if (解锁所有回忆事件.Value)
                {
                    foreach (var item in __result)
                        Traverse.Create(item).Field("is_enabled_").SetValue(true);
                }
            }
        }
    }
}
