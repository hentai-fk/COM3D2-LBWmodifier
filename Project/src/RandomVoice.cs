using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using CRCEdit;
using UnityEngine;
using Yotogis;
using LBWmodifier.Loader;

namespace LBWmodifier
{
    internal class RandomVoice : MonoBehaviour
    {
        public static object randomVoiceLock = new object();
        public static Maid[] personalMaid;
        public static Maid[] targetMaid;
        private static Stopwatch[] elapsedTime;
        private static Stopwatch[] loopTime;
        private static RandomVoiceParam[] voiceParams;
        public static YotogiPlayManager yotogiPlayManager;
        public static YotogiOldPlayManager yotogiOldPlayManager;

        public static long lastTalkTimeStamp;
        public static long lastTalkLength;

        private static GameObject gameobject_1;
        private static Harmony harmony_1;


        public static void initialize()
        {
            gameobject_1 = new GameObject();
            gameobject_1.AddComponent<RandomVoice>();
            GameObject.DontDestroyOnLoad(gameobject_1);
            harmony_1 = Harmony.CreateAndPatchAll(typeof(RandomVoice));
        }

        public static void unload()
        {
            GameObject.Destroy(gameobject_1);
            harmony_1.UnpatchSelf();
            gameobject_1 = null;
        }
        
        private void Awake()
        {
            new Thread(() =>
            {
                while (gameobject_1 != null)
                {
                    lock (randomVoiceLock)
                    {
                        updateThreadState();
                    }
                    Thread.Sleep(100);
                }
            }).Start();
        }
        
        private static void playLoopVoice(int maid)
        {
            var param = voiceParams[maid];
            for (int j = 0; j < param.getMaxLoopCount(); ++j)
            {
                if (startLoopVoice(param, maid) != -2)
                {
                    break;
                }
            }
        }

        private static void updateThreadState()
        {
            if (targetMaid == null || targetMaid.Length == 0 || !Modifier.女仆语音随机播放.Value)
                return;
            for (int i = 0; i < targetMaid.Length; ++i)
            {
                var audio = targetMaid[i].AudioMan;
                if (audio == null)
                    continue;
                long audioLen = (long) (1000 * audio.GetLength());
                if (audio.isPlay())
                {
                    if (audio.isLoop())
                    {
                        // 进入了循环语音以后才能开始下一个语音播放
                        if (loopTime[i].ElapsedMilliseconds > (Modifier.语音娇喘等待时间.Value < 0 ? audioLen : Modifier.语音娇喘等待时间.Value) &&
                            DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond > lastTalkTimeStamp + (Modifier.多人语音播放时间.Value < 0 ? lastTalkLength : Math.Min(lastTalkLength, Modifier.多人语音播放时间.Value)))
                        {
                            for (int j = 0; j < voiceParams[i].getMaxCount(); ++j)
                            {
                                var result = startVoice(voiceParams[i], i);
                                if (result == 0)
                                {
                                    loopTime[i].Reset();
                                }
                                if (result != -2)
                                    break;
                            }
                        }
                        loopTime[i].Start();
                    }
                    else
                    {
                        loopTime[i].Stop();
                    }
                    // 当循环时间超过娇喘长度后播放一次娇喘语音
                    if (loopTime[i].ElapsedMilliseconds > audioLen)
                    {
                        playLoopVoice(i);
                        loopTime[i].Reset();
                        loopTime[i].Start();
                    }
                    elapsedTime[i].Reset();
                }
                // int YotogiPlayManager.kRepeatWaitTime = 200
                else if (elapsedTime[i].ElapsedMilliseconds < 150)
                {
                    elapsedTime[i].Start();
                }
                else
                {
                    playLoopVoice(i);
                    loopTime[i].Reset();
                    loopTime[i].Start();
                    elapsedTime[i].Reset();
                }
            }
        }

        private static int startVoice(RandomVoiceParam param, int maid)
        {
            var m = param.getParam();
            if (param.isSingleVoice)
            {
                if (!param.isOldYotogi)
                {
                    return RcFileVoicePlayer.playSingle(m.level, m.relation, m.excite, targetMaid[maid]);
                }
                else
                {
                    return RcFileVoicePlayer.playSingleOld(m.level, m.relationOld, m.exciteOld, targetMaid[maid]);
                }
            }
            if (!param.isOldYotogi)
            {
                return RcFileVoicePlayer.playVoice(m.level, m.relation, m.rctype, m.rrctype, targetMaid[maid]);
            }
            else
            {
                return RcFileVoicePlayer.playVoiceOld(m.level, m.relationOld, m.rctypeOld, m.rrctypeOld, targetMaid[maid]);
            }
        }

        private static int startLoopVoice(RandomVoiceParam param, int maid)
        {
            var m = param.getParamLoop();
            if (!param.isOldYotogi)
            {
                /// <see cref="YotogiPlay.GetRRType(int)"/>
                return RcFileVoicePlayer.playRR(m.rrtype, targetMaid[maid]);
            }
            else
            {
                /// <see cref="YotogiOldPlay.GetRRType(int)"/>
                /// <same with cref="YotogiPlay.GetRRType(int)"/>
                return RcFileVoicePlayer.playRROld(m.rrtypeOld, targetMaid[maid]);
            }
        }

        private static void initVoice(string normal, string rc, string rrc, string rr, YotogiPlayManager ypm, YotogiOldPlayManager yopm, int playType)
        {
            lock (randomVoiceLock)
            {
                targetMaid = null;
                if (ypm == null && yopm == null)
                {
                    LBWmodifier.logger.LogWarning("initVoice ypm == null && yopm == null");
                    return;
                }
                
                personalMaid = getPersonalMaids(ypm, yopm);
                targetMaid = getActiveMaids();
                if (personalMaid == null || personalMaid.Length == 0 || targetMaid == null || targetMaid.Length == 0)
                    return;
                
                elapsedTime = new Stopwatch[targetMaid.Length];
                loopTime = new Stopwatch[targetMaid.Length];
                voiceParams = new RandomVoiceParam[targetMaid.Length];
                for (int i = 0; i < targetMaid.Length; ++i)
                {
                    if (targetMaid[i].status == null)
                    {
                        targetMaid = null;
                        LBWmodifier.logger.LogWarning($"initVoice targetMaid {targetMaid} has null status");
                        return;
                    }
                    elapsedTime[i] = new Stopwatch();
                    loopTime[i] = new Stopwatch();
                    voiceParams[i] = new RandomVoiceParam()
                    {
                        currentExcite = targetMaid[i].status.currentExcite,
                        isOldYotogi = ypm == null && yopm != null,
                        isSingleVoice = !string.IsNullOrEmpty(normal) && string.IsNullOrEmpty(rc) && string.IsNullOrEmpty(rrc),
                    };
                }

                yotogiPlayManager = ypm;
                yotogiOldPlayManager = yopm;

                lastTalkTimeStamp = 0;
                lastTalkLength = 0;

                RcFileVoicePlayer.init(normal, rc, rrc, rr, ypm, playType);
            }
        }
        
        public static Maid[] getActiveMaids()
        {
            var list = new List<Maid>();
            for (int i = 0; i < GameMain.Instance?.CharacterMgr.GetMaidCount(); i++)
            {
                var maid = GameMain.Instance.CharacterMgr.GetMaid(i);
                if (maid != null && maid.isActiveAndEnabled)
                    list.Add(maid);
            }
            return list.ToArray();
        }
        
        public static Maid[] getPersonalMaids(YotogiPlayManager ypm, YotogiOldPlayManager yopm)
        {
            var tv = ypm != null ? Traverse.Create(ypm) : yopm != null ? Traverse.Create(yopm) : null;
            return tv?.Field("replace_personal_target_maid_array_").GetValue() as Maid[];
        }

        [HarmonyPatch(typeof(YotogiPlayManager), "PlayNormalContinue")]
        [HarmonyPostfix]
        public static void PlayNormalContinue(YotogiPlayManager __instance, Skill.Data.Command.Data command_data, bool lockRRUpdate)
        {
            initVoice(command_data.basic.call_file.normal, command_data.basic.call_file.rc, command_data.basic.call_file.rrc, command_data.basic.call_file.rr, __instance, null, 1);
        }

        [HarmonyPatch(typeof(YotogiPlayManager), "PlayDrunkContinue")]
        [HarmonyPostfix]
        public static void PlayDrunkContinue(YotogiPlayManager __instance, Skill.Data.Command.Data command_data, bool lockRRUpdate)
        {
            initVoice(command_data.basic.call_file.normal, command_data.basic.call_file.rc, command_data.basic.call_file.rrc, command_data.basic.call_file.rr, __instance, null, 2);
        }

        [HarmonyPatch(typeof(YotogiPlayManager), "PlayNormalSingle")]
        [HarmonyPostfix]
        public static void PlayNormalSingle(YotogiPlayManager __instance, Skill.Data.Command.Data command_data, bool lockRRUpdate)
        {
            initVoice(command_data.basic.call_file.normal, command_data.basic.call_file.rc, command_data.basic.call_file.rrc, command_data.basic.call_file.rr, __instance, null, 4);
        }

        [HarmonyPatch(typeof(YotogiPlayManager), "PlayDrunkSingle")]
        [HarmonyPostfix]
        public static void PlayDrunkSingle(YotogiPlayManager __instance, Skill.Data.Command.Data command_data, bool lockRRUpdate)
        {
            initVoice(command_data.basic.call_file.normal, command_data.basic.call_file.rc, command_data.basic.call_file.rrc, command_data.basic.call_file.rr, __instance, null, 5);
        }

        [HarmonyPatch(typeof(YotogiPlayManager), "PlayProclivitySingle")]
        [HarmonyPostfix]
        public static void PlayProclivitySingle(YotogiPlayManager __instance, Skill.Data.Command.Data command_data, bool lockRRUpdate)
        {
            initVoice(command_data.basic.call_file.normal, command_data.basic.call_file.rc, command_data.basic.call_file.rrc, command_data.basic.call_file.rr, __instance, null, 6);
        }

        [HarmonyPatch(typeof(YotogiOldPlayManager), "PlayNormalContinue")]
        [HarmonyPostfix]
        public static void PlayNormalContinue(YotogiOldPlayManager __instance, Skill.Old.Data.Command.Data command_data)
        {
            initVoice(command_data.basic.normal_file, command_data.basic.rc_file, command_data.basic.rrc_file, command_data.basic.rr_file, null, __instance, 1);
        }

        [HarmonyPatch(typeof(YotogiOldPlayManager), "PlayDrunkContinue")]
        [HarmonyPostfix]
        public static void PlayDrunkContinue(YotogiOldPlayManager __instance, Skill.Old.Data.Command.Data command_data)
        {
            initVoice(command_data.basic.normal_file, command_data.basic.rc_file, command_data.basic.rrc_file, command_data.basic.rr_file, null, __instance, 2);
        }

        [HarmonyPatch(typeof(YotogiOldPlayManager), "PlayProclivityContinue")]
        [HarmonyPostfix]
        public static void PlayProclivityContinue(YotogiOldPlayManager __instance, Skill.Old.Data.Command.Data command_data)
        {
            initVoice(command_data.basic.normal_file, command_data.basic.rc_file, command_data.basic.rrc_file, command_data.basic.rr_file, null, __instance, 3);
        }

        [HarmonyPatch(typeof(YotogiOldPlayManager), "PlayNormalSingle")]
        [HarmonyPostfix]
        public static void PlayNormalSingle(YotogiOldPlayManager __instance, Skill.Old.Data.Command.Data command_data)
        {
            initVoice(command_data.basic.normal_file, command_data.basic.rc_file, command_data.basic.rrc_file, command_data.basic.rr_file, null, __instance, 4);
        }

        [HarmonyPatch(typeof(YotogiOldPlayManager), "PlayDrunkSingle")]
        [HarmonyPostfix]
        public static void PlayDrunkSingle(YotogiOldPlayManager __instance, Skill.Old.Data.Command.Data command_data)
        {
            initVoice(command_data.basic.normal_file, command_data.basic.rc_file, command_data.basic.rrc_file, command_data.basic.rr_file, null, __instance, 5);
        }

        [HarmonyPatch(typeof(YotogiOldPlayManager), "PlayProclivitySingle")]
        [HarmonyPostfix]
        public static void PlayProclivitySingle(YotogiOldPlayManager __instance, Skill.Old.Data.Command.Data command_data)
        {
            initVoice(command_data.basic.normal_file, command_data.basic.rc_file, command_data.basic.rrc_file, command_data.basic.rr_file, null, __instance, 6);
        }

        [HarmonyPatch(typeof(YotogiPlayManager), "OnFinish")]
        [HarmonyPatch(typeof(YotogiOldPlayManager), "OnFinish")]
        [HarmonyPatch(typeof(YotogiPlayManager), "OnClickCommand")]
        [HarmonyPatch(typeof(YotogiOldPlayManager), "OnClickCommand")]
        [HarmonyPrefix]
        public static void OnFinish()
        {
            targetMaid = null;
            RcFileVoicePlayer.finish();
        }
    }

    class RandomVoiceParam
    {
        public struct Param
        {
            public int level;
            public MaidStatus.Relation relation;
            public MaidStatus.Old.Relation relationOld;
            public Yotogi.RCType rctype;
            public YotogiOld.RCType rctypeOld;
            public Yotogi.RRCType rrctype;
            public YotogiOld.RRCType rrctypeOld;
            public Yotogi.ExcitementStatus excite;
            public YotogiOld.ExcitementStatus exciteOld;
        }

        public struct ParamLoop
        {
            public Yotogi.RRType rrtype;
            public YotogiOld.RRType rrtypeOld;
        }
        
        private Queue<Param> paramQueue = new Queue<Param>();
        private Queue<ParamLoop> paramLoopQueue = new Queue<ParamLoop>();
        private int paramQueueMaxCount;
        private int paramLoopQueueMaxCount;
        public int currentExcite;
        public bool isOldYotogi;
        public bool isSingleVoice;

        private void fillQueue()
        {
            lock (paramQueue)
            {
                if (paramQueue.Count != 0)
                    return;
                var random = new System.Random();
                var list = new List<Param>();
                foreach (var level in Enumerable.Range(1, 3).OrderBy(x => random.Next()))
                {
                    foreach (var relation in Enumerable.Range(0, !isOldYotogi ? 3 : 5).OrderBy(x => random.Next()))
                    {
                        if (!isSingleVoice)
                        {
                            if (currentExcite < 100)
                            {
                                foreach (var type in Enumerable.Range(0, !isOldYotogi ? 6 : 9)
                                             .OrderBy(x => random.Next()))
                                {
                                    list.Add(new Param()
                                    {
                                        level = level,
                                        relation = MaidStatus.Relation.Contact + relation,
                                        relationOld = MaidStatus.Old.Relation.Tonus + relation,
                                        rctype = type < 1 ? Yotogi.RCType.RC0 : Yotogi.RCType.RCNull,
                                        rctypeOld = type < 2 ? YotogiOld.RCType.RC_1 + type : YotogiOld.RCType.RCNull,
                                        rrctype = Yotogi.RRCType.RRC_2 + type - 1,
                                        rrctypeOld = YotogiOld.RRCType.RRC_4 + type - 2,
                                    });
                                }
                            }
                            else if (currentExcite < 200)
                            {
                                foreach (var type in Enumerable.Range(0, 4).OrderBy(x => random.Next()))
                                {
                                    list.Add(new Param()
                                    {
                                        level = level,
                                        relation = MaidStatus.Relation.Contact + relation,
                                        relationOld = MaidStatus.Old.Relation.Tonus + relation,
                                        rctype = type < 1 ? Yotogi.RCType.RC1 : Yotogi.RCType.RCNull,
                                        rctypeOld = type < 1 ? YotogiOld.RCType.RC1 : YotogiOld.RCType.RCNull,
                                        rrctype = Yotogi.RRCType.RRC_2 + type - 1,
                                        rrctypeOld = YotogiOld.RRCType.RRC4 + type - 1,
                                    });
                                }
                            }
                            else
                            {
                                foreach (var type in Enumerable.Range(0, !isOldYotogi ? 4 : 5)
                                             .OrderBy(x => random.Next()))
                                {
                                    list.Add(new Param()
                                    {
                                        level = level,
                                        relation = MaidStatus.Relation.Contact + relation,
                                        relationOld = MaidStatus.Old.Relation.Tonus + relation,
                                        rctype = type < 1 ? Yotogi.RCType.RC2 : Yotogi.RCType.RCNull,
                                        rctypeOld = type < 1 ? YotogiOld.RCType.RC2 : YotogiOld.RCType.RCNull,
                                        rrctype = Yotogi.RRCType.RRC7 + type - 1,
                                        rrctypeOld = YotogiOld.RRCType.RRC7 + type - 1,
                                    });
                                }
                            }
                        }
                        else
                        {
                            if (currentExcite < 100)
                            {
                                foreach (var excite in Enumerable.Range(0, 2).OrderBy(x => random.Next()))
                                {
                                    list.Add(new Param()
                                    {
                                        level = level,
                                        relation = MaidStatus.Relation.Contact + relation,
                                        relationOld = MaidStatus.Old.Relation.Tonus + relation,
                                        excite = Yotogi.ExcitementStatus.Minus + excite,
                                        exciteOld = YotogiOld.ExcitementStatus.Minus + excite,
                                    });
                                }
                            }
                            else if (currentExcite < 200)
                            {
                                list.Add(new Param()
                                {
                                    level = level,
                                    relation = MaidStatus.Relation.Contact + relation,
                                    relationOld = MaidStatus.Old.Relation.Tonus + relation,
                                    excite = Yotogi.ExcitementStatus.Medium,
                                    exciteOld = YotogiOld.ExcitementStatus.Medium,
                                });
                            }
                            else
                            {
                                list.Add(new Param()
                                {
                                    level = level,
                                    relation = MaidStatus.Relation.Contact + relation,
                                    relationOld = MaidStatus.Old.Relation.Tonus + relation,
                                    excite = Yotogi.ExcitementStatus.Large,
                                    exciteOld = YotogiOld.ExcitementStatus.Large,
                                });
                            }
                        }
                    }
                }

                foreach (var param in list.OrderBy(x => random.Next()))
                {
                    paramQueue.Enqueue(param);
                }

                paramQueueMaxCount = paramQueue.Count;
            }
        }

        private void fillLoopQueue()
        {
            lock (paramLoopQueue)
            {
                if (paramLoopQueue.Count != 0)
                    return;
                var random = new System.Random();
                if (currentExcite < 100) 
                {
                    foreach (var type in Enumerable.Range(0, 4).OrderBy(x => random.Next()))
                    {
                        paramLoopQueue.Enqueue(new ParamLoop()
                        {
                            rrtype = Yotogi.RRType.RR_2 + type,
                            rrtypeOld = YotogiOld.RRType.RR_2 + type,
                        });
                    }
                }
                else if (currentExcite < 200)
                {
                    foreach (var type in Enumerable.Range(0, 2).OrderBy(x => random.Next()))
                    {
                        paramLoopQueue.Enqueue(new ParamLoop()
                        {
                            rrtype = Yotogi.RRType.RR3 + type,
                            rrtypeOld = YotogiOld.RRType.RR3 + type,
                        });
                    }
                }
                else
                {
                    foreach (var type in Enumerable.Range(0, 2).OrderBy(x => random.Next()))
                    {
                        paramLoopQueue.Enqueue(new ParamLoop()
                        {
                            rrtype = Yotogi.RRType.RR5 + type,
                            rrtypeOld = YotogiOld.RRType.RR5 + type,
                        });
                    }
                }

                paramLoopQueueMaxCount = paramLoopQueue.Count;
            }
        }

        public int getMaxCount()
        {
            fillQueue();
            return paramQueueMaxCount;
        }

        public int getMaxLoopCount()
        {
            fillLoopQueue();
            return paramLoopQueueMaxCount;
        }
        
        public Param getParam()
        {
            fillQueue();
            return paramQueue.Dequeue();
        }

        public ParamLoop getParamLoop()
        {
            fillLoopQueue();
            return paramLoopQueue.Dequeue();
        }
    }

    class ParseRcFileVoice
    {
        private static Dictionary<string, string[]> globalRcFileText = new Dictionary<string, string[]>();
        private string[] nowText;

        public readonly string filename;
        private Maid[] maidRecurse;
        private bool useInternalMaid;

        public ParseRcFileVoice(string file, Maid[] maids, bool useInternal)
        {
            if (string.IsNullOrEmpty(file) || maids == null || maids.Length == 0)
                return;
            if (!file.EndsWith(".ks"))
                file += ".ks";
            filename = ScriptManager.ReplacePersonal(maids, file);
            maidRecurse = maids;
            useInternalMaid = useInternal;
            using (var fd = FileTool.OpenFileCompatible(filename))
            {
                if (fd == null)
                    return;
                var gKey = file + "@" + fd.system.GetHashCode();
                if (!globalRcFileText.ContainsKey(gKey))
                {
                    var text = NUty.SjisToUnicode(fd.file.ReadAll());
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; ++i)
                        lines[i] = lines[i].Trim();
                    globalRcFileText[gKey] = lines;
                }
                nowText = globalRcFileText[gKey];
            }
        }
        
        public static int playLabelWithMaids(Maid maid, string file, string label, bool isLoop)
        {
            var rcVoice = new ParseRcFileVoice(file, RandomVoice.personalMaid, false);
            var result = rcVoice.playLabelWithMaid(label, maid, isLoop);
            if (result != 0 && isLoop && Modifier.多人语音强制播放.Value && rcVoice.filename != file)
            {
                result = new ParseRcFileVoice(file, new []{maid}, true).playLabelWithMaid(label, maid, isLoop);
            }
            return result;
        }

        public static T execScript<T>(string script)
        {
            using (var tjs = new TJSVariant())
            {
                GameMain.Instance.ScriptMgr.EvalScript(script, tjs);
                if (typeof(T) == typeof(bool))
                    return (T)(object)tjs.AsBool();
                if (typeof(T) == typeof(int))
                    return (T)(object)tjs.AsInteger();
                if (typeof(T) == typeof(float))
                    return (T)(object)tjs.AsReal();
                if (typeof(T) == typeof(string))
                    return (T)(object)tjs.AsString();
                LBWmodifier.logger.LogWarning($"unsupported return type {typeof(T)}");
                return default;
            }
        }
        
        private static bool matchLabel(string line, string label)
        {
            return line.StartsWith("*") && line.StartsWith(label, StringComparison.CurrentCultureIgnoreCase);
        }

        private static KeyValuePair<string, Dictionary<string, string>>? parseLine(string line)
        {
            line = line.Trim();
            if (!line.StartsWith("@"))
                return null;
            int idx = line.IndexOf(' ');
            if (idx == -1)
                return new KeyValuePair<string, Dictionary<string, string>>(line, new Dictionary<string, string>());
            var head = line.Substring(0, idx).ToLower();
            line = line.Substring(idx + 1);
            var list = new Dictionary<string, string>();
            string sp = "";
            string st = "";
            char se = '\0';
            bool eq = false;
            foreach (var c in line)
            {
                if (se != '\0')
                {
                    if (se != c)
                        st += c;
                    else
                        se = '\0';
                }
                else if (c != ' ')
                {
                    if (eq && st.Length == 0 && (c == '"' || c == '\''))
                        se = c;
                    else if (c == '=')
                    {
                        if (st.Length > 0)
                        {
                            sp = st;
                            st = "";
                        }
                        eq = true;
                    }
                    else
                        st += c;
                }
                else if (st.Length > 0)
                {
                    if (eq && sp.Length > 0)
                    {
                        list[sp] = st;
                        sp = "";
                    }
                    else
                        sp = st;
                    st = "";
                }
            }
            if (eq && st.Length > 0 && st.Length > 0)
            {
                list[sp] = st;
            }
            return new KeyValuePair<string, Dictionary<string, string>>(head, list);
        }

        private bool loadVoice(string file, int maid, Maid target_maid, bool isLoop)
        {
            if (file == null)
                return false;
            if (!file.EndsWith(".ogg"))
                file += ".ogg";
            if ((0 <= maid && maid < GameMain.Instance?.CharacterMgr?.GetMaidCount() &&
                 GameMain.Instance?.CharacterMgr?.GetMaid(maid) == target_maid) ||
                (useInternalMaid && maid == 0 && maidRecurse[0] == target_maid))
            {
                if (!isLoop)
                {
                    RandomVoice.lastTalkTimeStamp = long.MaxValue - RandomVoice.lastTalkLength;
                }
                LBWmodifier.DoMainAction(()=>target_maid.AudioMan?.LoadPlay(file, 0, false, isLoop));
                if (!isLoop)
                {
                    RandomVoice.lastTalkTimeStamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
                    RandomVoice.lastTalkLength = (long)(target_maid.AudioMan.GetLength() * 1000);
                }
                else
                {
                    if (RandomVoice.yotogiPlayManager != null)
                        RandomVoice.yotogiPlayManager.ClearRepeatVoiceData();
                    if (RandomVoice.yotogiOldPlayManager != null)
                        Traverse.Create(RandomVoice.yotogiOldPlayManager).Field("repeat_time_").SetValue(0);
                }
                return true;
            }
            return false;
        }

        /// <returns>0 成功 -1 读取失败 -2 无法读取语音 -3 无匹配的符号</returns>
        public int playLabelWithMaid(string target_label, Maid target_maid, bool isLoop)
        {
            if (nowText == null || string.IsNullOrEmpty(target_label))
                return -1;
            target_label = target_label.Trim();
            bool enterLabel = false;
            Stack<bool> enterIfElse = new Stack<bool>();
            foreach (var line in nowText)
            {
                if (matchLabel(line, target_label))
                {
                    enterLabel = true;
                    continue;
                }
                if (enterLabel)
                {
                    var sline = parseLine(line);
                    if (!sline.HasValue)
                        continue;
                    var tag = sline.Value.Key;
                    var dic = sline.Value.Value;
                    if (enterIfElse.Count == 0 || enterIfElse.Last())
                    {
                        if (tag == "@talk" || tag == "@talkrepeat" || tag == "@talkrepeatadd")
                        {
                            int maid = 0;
                            if (dic.TryGetValue("maid", out var exp))
                            {
                                if (exp.TrimStart().StartsWith("&"))
                                {
                                    maid = execScript<int>(exp);
                                }
                                else
                                {
                                    if (!int.TryParse(exp, out maid))
                                        maid = -1;
                                }
                            }
                            if (dic.TryGetValue("voice", out var voice))
                            {
                                if (loadVoice(voice, maid, target_maid, isLoop))
                                    return 0;
                            }
                        }
                        else if (tag == "@eval")
                        {
                            if (dic.TryGetValue("exp", out var exp))
                                execScript<bool>(exp);
                        }
                        else if (tag == "@call")
                        {
                            dic.TryGetValue("file", out var file);
                            if (dic.TryGetValue("label", out var label))
                            {
                                if (label.StartsWith("&"))
                                    label = execScript<string>(label);
                                if ((file == null ? this : new ParseRcFileVoice(file, maidRecurse, useInternalMaid)).playLabelWithMaid(label, target_maid, isLoop) == 0)
                                    return 0;
                            }
                        }
                        else if (tag == "@return")
                        {
                            return -2;
                        }
                        else if (tag == "@if")
                        {
                            enterIfElse.Push(dic.TryGetValue("exp", out var exp) && execScript<bool>(exp));
                        }
                    }
                    if (enterIfElse.Count != 0)
                    {
                        if (enterIfElse.Last())
                        {
                            if (tag == "@elsif" || tag == "@else")
                            {
                                enterIfElse.Pop();
                                enterIfElse.Push(false);
                            }
                        }
                        else
                        {
                            if (tag == "@else" || (tag == "@elsif" && dic.TryGetValue("exp", out var exp) && execScript<bool>(exp)))
                            {
                                enterIfElse.Pop();
                                enterIfElse.Push(true);
                            }
                        }
                        if (tag == "@endif")
                        {
                            enterIfElse.Pop();
                        }
                    }
                }
            }
            return -3;
        }
    }

    class RcFileVoicePlayer
    {
        private static string playingNormal;
        private static string playingRC;
        private static string playingRRC;
        private static string playingRR;
        private static bool estrusMode;
        private static int playFunc;


        /// <see cref="YotogiPlayManager.PlayNormalContinue"/>
        public static bool PlayNormalContinue(int playing_skill_level, MaidStatus.Relation relation, Yotogi.RCType rctype, Yotogi.RRCType rrctype, Maid maid)
        {
            string text = string.Empty;
            string text2 = string.Empty;
            if (rctype != Yotogi.RCType.RCNull)
            {
                string str = rctype.ToString();
                text = playingRC;
                if (estrusMode)
                {
                    text2 = ((playing_skill_level != 3) ? "*初々しい発情" : "*慣れ発情");
                    text2 += str;
                }
                else if (playing_skill_level == 3)
                {
                    if (relation == MaidStatus.Relation.Lover)
                    {
                        text2 = "*慣れ恋人" + str;
                    }
                    else
                    {
                        text2 = "*緊張orお近づきor信頼" + str;
                    }
                }
                else if (playing_skill_level == 2 || relation == MaidStatus.Relation.Trust)
                {
                    text2 = "*フラット、変化進行" + str;
                }
                else if (relation != MaidStatus.Relation.Contact)
                {
                    if (relation == MaidStatus.Relation.Lover)
                    {
                        text2 = "*初々しい恋人" + str;
                    }
                }
                else
                {
                    text2 = "*初々しいお近づき" + str;
                }
            }
            else
            {
                HashSet<Yotogi.RRCType> hashSet = new HashSet<Yotogi.RRCType>();
                if (estrusMode)
                {
                    hashSet.Add(Yotogi.RRCType.RRC1);
                    hashSet.Add(Yotogi.RRCType.RRC3);
                    hashSet.Add(Yotogi.RRCType.RRC4);
                    hashSet.Add(Yotogi.RRCType.RRC6);
                }
                if (rrctype != Yotogi.RRCType.RRCNull && (!estrusMode || !hashSet.Contains(rrctype)))
                {
                    if (estrusMode)
                    {
                        Dictionary<Yotogi.RRCType, Yotogi.RRCType> dictionary = new Dictionary<Yotogi.RRCType, Yotogi.RRCType>();
                        dictionary.Add(Yotogi.RRCType.RRC2, Yotogi.RRCType.RRC1);
                        dictionary.Add(Yotogi.RRCType.RRC5, Yotogi.RRCType.RRC2);
                        dictionary.Add(Yotogi.RRCType.RRC7, Yotogi.RRCType.RRC3);
                        dictionary.Add(Yotogi.RRCType.RRC8, Yotogi.RRCType.RRC4);
                        dictionary.Add(Yotogi.RRCType.RRC9, Yotogi.RRCType.RRC5);
                        if (dictionary.ContainsKey(rrctype))
                        {
                            rrctype = dictionary[rrctype];
                        }
                    }
                    string str2 = rrctype.ToString();
                    if (playing_skill_level == 1 || playing_skill_level == 2 || rrctype == Yotogi.RRCType.RRC_1 || rrctype == Yotogi.RRCType.RRC_2)
                    {
                        text2 = "*初々しい";
                    }
                    else if (playing_skill_level == 3)
                    {
                        text2 = "*慣れ";
                    }
                    if (estrusMode)
                    {
                        text2 += "発情";
                    }
                    text2 += str2;
                    if (!string.IsNullOrEmpty(text2))
                    {
                        text = playingRRC;
                    }
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, text, text2, false) == 0;
        }

        /// <see cref="YotogiPlayManager.PlayDrunkContinue"/>
        public static bool PlayDrunkContinue(MaidStatus.Relation relation, Yotogi.RCType rctype, Maid maid)
        {
            string text = string.Empty;
            string label = string.Empty;
            if (rctype != Yotogi.RCType.RCNull)
            {
                string str = rctype.ToString();
                text = playingRC;
                if (relation != MaidStatus.Relation.Contact)
                {
                    if (relation != MaidStatus.Relation.Trust)
                    {
                        if (relation == MaidStatus.Relation.Lover)
                        {
                            label = "*酔い恋人" + str;
                        }
                    }
                    else
                    {
                        label = "*酔い信頼" + str;
                    }
                }
                else
                {
                    label = "*酔いお近づき" + str;
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, text, label, false) == 0;
        }
        
        /// <see cref="YotogiPlayManager.PlayNormalSingle"/>
        public static bool PlayNormalSingle(int playing_skill_level, MaidStatus.Relation relation, Yotogi.ExcitementStatus excitementStatus, Maid maid)
        {
            string label = string.Empty;
            if (estrusMode)
            {
                if (playing_skill_level == 1 || playing_skill_level == 2)
                {
                    switch (excitementStatus)
                    {
                        case Yotogi.ExcitementStatus.Minus:
                            label = "*発情SA1";
                            break;
                        case Yotogi.ExcitementStatus.Small:
                            label = "*発情SA2";
                            break;
                        case Yotogi.ExcitementStatus.Medium:
                            label = "*発情SA3";
                            break;
                        case Yotogi.ExcitementStatus.Large:
                            label = "*発情SA4";
                            break;
                    }
                }
                else
                {
                    switch (excitementStatus)
                    {
                        case Yotogi.ExcitementStatus.Minus:
                            label = "*発情SA5";
                            break;
                        case Yotogi.ExcitementStatus.Small:
                            label = "*発情SA6";
                            break;
                        case Yotogi.ExcitementStatus.Medium:
                            label = "*発情SA7";
                            break;
                        case Yotogi.ExcitementStatus.Large:
                            label = "*発情SA8";
                            break;
                    }
                }
            }
            else if (playing_skill_level == 1 || playing_skill_level == 2)
            {
                if (relation == MaidStatus.Relation.Lover)
                {
                    switch (excitementStatus)
                    {
                        case Yotogi.ExcitementStatus.Minus:
                            label = "*SA5";
                            break;
                        case Yotogi.ExcitementStatus.Small:
                            label = "*SA6";
                            break;
                        case Yotogi.ExcitementStatus.Medium:
                            label = "*SA7";
                            break;
                        case Yotogi.ExcitementStatus.Large:
                            label = "*SA8";
                            break;
                    }
                }
                else if (playing_skill_level == 2 || relation == MaidStatus.Relation.Trust)
                {
                    switch (excitementStatus)
                    {
                        case Yotogi.ExcitementStatus.Minus:
                            label = "*SA9";
                            break;
                        case Yotogi.ExcitementStatus.Small:
                            label = "*SA10";
                            break;
                        case Yotogi.ExcitementStatus.Medium:
                            label = "*SA11";
                            break;
                        case Yotogi.ExcitementStatus.Large:
                            label = "*SA12";
                            break;
                    }
                }
                else
                {
                    switch (excitementStatus)
                    {
                        case Yotogi.ExcitementStatus.Minus:
                            label = "*SA1";
                            break;
                        case Yotogi.ExcitementStatus.Small:
                            label = "*SA2";
                            break;
                        case Yotogi.ExcitementStatus.Medium:
                            label = "*SA3";
                            break;
                        case Yotogi.ExcitementStatus.Large:
                            label = "*SA4";
                            break;
                    }
                }
            }
            else if (playing_skill_level == 3)
            {
                if (relation == MaidStatus.Relation.Lover)
                {
                    switch (excitementStatus)
                    {
                        case Yotogi.ExcitementStatus.Minus:
                            label = "*SA17";
                            break;
                        case Yotogi.ExcitementStatus.Small:
                            label = "*SA18";
                            break;
                        case Yotogi.ExcitementStatus.Medium:
                            label = "*SA19";
                            break;
                        case Yotogi.ExcitementStatus.Large:
                            label = "*SA20";
                            break;
                    }
                }
                else
                {
                    switch (excitementStatus)
                    {
                        case Yotogi.ExcitementStatus.Minus:
                            label = "*SA13";
                            break;
                        case Yotogi.ExcitementStatus.Small:
                            label = "*SA14";
                            break;
                        case Yotogi.ExcitementStatus.Medium:
                            label = "*SA15";
                            break;
                        case Yotogi.ExcitementStatus.Large:
                            label = "*SA16";
                            break;
                    }
                }
            } 
            return ParseRcFileVoice.playLabelWithMaids(maid, playingNormal, label, false) == 0;
        }
        
        /// <see cref="YotogiPlayManager.PlayDrunkSingle"/>
        public static bool PlayDrunkSingle(MaidStatus.Relation relation, Yotogi.ExcitementStatus excitementStatus, Maid maid)
        {
            string label = string.Empty;
            if (relation == MaidStatus.Relation.Contact)
            {
                switch (excitementStatus)
                {
                    case Yotogi.ExcitementStatus.Minus:
                        label = "*酔いSA1";
                        break;
                    case Yotogi.ExcitementStatus.Small:
                        label = "*酔いSA2";
                        break;
                    case Yotogi.ExcitementStatus.Medium:
                        label = "*酔いSA3";
                        break;
                    case Yotogi.ExcitementStatus.Large:
                        label = "*酔いSA4";
                        break;
                }
            }
            else if (relation == MaidStatus.Relation.Trust)
            {
                switch (excitementStatus)
                {
                    case Yotogi.ExcitementStatus.Minus:
                        label = "*酔いSA5";
                        break;
                    case Yotogi.ExcitementStatus.Small:
                        label = "*酔いSA6";
                        break;
                    case Yotogi.ExcitementStatus.Medium:
                        label = "*酔いSA7";
                        break;
                    case Yotogi.ExcitementStatus.Large:
                        label = "*酔いSA8";
                        break;
                }
            }
            else if (relation == MaidStatus.Relation.Lover)
            {
                switch (excitementStatus)
                {
                    case Yotogi.ExcitementStatus.Minus:
                        label = "*酔いSA9";
                        break;
                    case Yotogi.ExcitementStatus.Small:
                        label = "*酔いSA10";
                        break;
                    case Yotogi.ExcitementStatus.Medium:
                        label = "*酔いSA11";
                        break;
                    case Yotogi.ExcitementStatus.Large:
                        label = "*酔いSA12";
                        break;
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, playingNormal, label, false) == 0;
        }
        
        /// <see cref="YotogiPlayManager.PlayProclivitySingle"/>
        public static bool PlayProclivitySingle(Yotogi.ExcitementStatus excitementStatus, Maid maid)
        {
            string label = string.Empty;
            switch (excitementStatus)
            {
                case Yotogi.ExcitementStatus.Minus:
                    label = "*性癖SA1";
                    break;
                case Yotogi.ExcitementStatus.Small:
                    label = "*性癖SA2";
                    break;
                case Yotogi.ExcitementStatus.Medium:
                    label = "*性癖SA3";
                    break;
                case Yotogi.ExcitementStatus.Large:
                    label = "*性癖SA4";
                    break;
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, playingNormal, label, false) == 0;
        }


        /// <see cref="YotogiOldPlayManager.PlayNormalContinue"/>
        public static bool PlayNormalContinueOld(int playing_skill_level, MaidStatus.Old.Relation relation, YotogiOld.RCType rctype, YotogiOld.RRCType rrctype, Maid maid)
        {
            string text = string.Empty;
            string label = string.Empty;
            if (rctype != YotogiOld.RCType.RCNull)
            {
                string str = rctype.ToString();
                text = playingRC;
                if (relation == MaidStatus.Old.Relation.Slave)
                {
                    label = "*愛奴" + str;
                }
                else if (playing_skill_level == 3)
                {
                    if (relation == MaidStatus.Old.Relation.Lover)
                    {
                        label = "*慣れ恋人" + str;
                    }
                    else
                    {
                        label = "*緊張orお近づきor信頼" + str;
                    }
                }
                else if (playing_skill_level == 2 || relation == MaidStatus.Old.Relation.Trust)
                {
                    label = "*フラット、変化進行" + str;
                }
                else if (relation != MaidStatus.Old.Relation.Tonus)
                {
                    if (relation != MaidStatus.Old.Relation.Contact)
                    {
                        if (relation == MaidStatus.Old.Relation.Lover)
                        {
                            label = "*不慣れ恋人" + str;
                        }
                    }
                    else
                    {
                        label = "*不慣れお近づき" + str;
                    }
                }
                else
                {
                    label = "*不慣れ緊張" + str;
                }
            }
            else
            {
                if (rrctype != YotogiOld.RRCType.RRCNull)
                {
                    string str2 = rrctype.ToString();
                    text = playingRRC;
                    if (playing_skill_level == 1 || playing_skill_level == 2)
                    {
                        label = "*不慣れ" + str2;
                    }
                    else
                    {
                        label = "*慣れ" + str2;
                    }
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, text, label, false) == 0;
        }

        /// <see cref="YotogiOldPlayManager.PlayDrunkContinue"/>
        public static bool PlayDrunkContinueOld(YotogiOld.RCType rctype, MaidStatus.Old.Relation relation, Maid maid)
        {
            string text = string.Empty;
            string label = string.Empty;
            if (rctype != YotogiOld.RCType.RCNull)
            {
                string str = rctype.ToString();
                text = playingRC;
                switch (relation)
                {
                    case MaidStatus.Old.Relation.Tonus:
                        label = "*酔い緊張" + str;
                        break;
                    case MaidStatus.Old.Relation.Contact:
                        label = "*酔いお近づき" + str;
                        break;
                    case MaidStatus.Old.Relation.Trust:
                        label = "*酔い信頼" + str;
                        break;
                    case MaidStatus.Old.Relation.Lover:
                        label = "*酔い恋人" + str;
                        break;
                    case MaidStatus.Old.Relation.Slave:
                        label = "*酔い愛奴" + str;
                        break;
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, text, label, false) == 0;
        }

        /// <see cref="YotogiOldPlayManager.PlayProclivityContinue"/>
        public static bool PlayProclivityContinueOld(YotogiOld.RCType rctype, YotogiOld.RRCType rrctype, Maid maid)
        {
            string text = string.Empty;
            string label = string.Empty;
            if (rctype != YotogiOld.RCType.RCNull)
            {
                string str = rctype.ToString();
                text = playingRC;
                label = "*性癖" + str;
            }
            else
            {
                if (rrctype != YotogiOld.RRCType.RRCNull)
                {
                    string str2 = rrctype.ToString();
                    text = playingRRC;
                    label = "*性癖" + str2;
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, text, label, false) == 0;
        }
        
        /// <see cref="YotogiOldPlayManager.PlayNormalSingle"/>
        public static bool PlayNormalSingleOld(int playing_skill_level, MaidStatus.Old.Relation relation, YotogiOld.ExcitementStatus excitementStatus, Maid maid)
        {
            string label = string.Empty;
            if (relation == MaidStatus.Old.Relation.Slave)
            {
                switch (excitementStatus)
                {
                    case YotogiOld.ExcitementStatus.Minus:
                        label = "*SA21";
                        break;
                    case YotogiOld.ExcitementStatus.Small:
                        label = "*SA22";
                        break;
                    case YotogiOld.ExcitementStatus.Medium:
                        label = "*SA23";
                        break;
                    case YotogiOld.ExcitementStatus.Large:
                        label = "*SA24";
                        break;
                }
            }
            else if (playing_skill_level == 1 || playing_skill_level == 2)
            {
                if (relation == MaidStatus.Old.Relation.Lover)
                {
                    switch (excitementStatus)
                    {
                        case YotogiOld.ExcitementStatus.Minus:
                            label = "*SA5";
                            break;
                        case YotogiOld.ExcitementStatus.Small:
                            label = "*SA6";
                            break;
                        case YotogiOld.ExcitementStatus.Medium:
                            label = "*SA7";
                            break;
                        case YotogiOld.ExcitementStatus.Large:
                            label = "*SA8";
                            break;
                    }
                }
                else if (playing_skill_level == 2 || relation == MaidStatus.Old.Relation.Trust)
                {
                    switch (excitementStatus)
                    {
                        case YotogiOld.ExcitementStatus.Minus:
                            label = "*SA9";
                            break;
                        case YotogiOld.ExcitementStatus.Small:
                            label = "*SA10";
                            break;
                        case YotogiOld.ExcitementStatus.Medium:
                            label = "*SA11";
                            break;
                        case YotogiOld.ExcitementStatus.Large:
                            label = "*SA12";
                            break;
                    }
                }
                else
                {
                    switch (excitementStatus)
                    {
                        case YotogiOld.ExcitementStatus.Minus:
                            label = "*SA1";
                            break;
                        case YotogiOld.ExcitementStatus.Small:
                            label = "*SA2";
                            break;
                        case YotogiOld.ExcitementStatus.Medium:
                            label = "*SA3";
                            break;
                        case YotogiOld.ExcitementStatus.Large:
                            label = "*SA4";
                            break;
                    }
                }
            }
            else if (playing_skill_level == 3)
            {
                if (relation == MaidStatus.Old.Relation.Lover)
                {
                    switch (excitementStatus)
                    {
                        case YotogiOld.ExcitementStatus.Minus:
                            label = "*SA17";
                            break;
                        case YotogiOld.ExcitementStatus.Small:
                            label = "*SA18";
                            break;
                        case YotogiOld.ExcitementStatus.Medium:
                            label = "*SA19";
                            break;
                        case YotogiOld.ExcitementStatus.Large:
                            label = "*SA20";
                            break;
                    }
                }
                else
                {
                    switch (excitementStatus)
                    {
                        case YotogiOld.ExcitementStatus.Minus:
                            label = "*SA13";
                            break;
                        case YotogiOld.ExcitementStatus.Small:
                            label = "*SA14";
                            break;
                        case YotogiOld.ExcitementStatus.Medium:
                            label = "*SA15";
                            break;
                        case YotogiOld.ExcitementStatus.Large:
                            label = "*SA16";
                            break;
                    }
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, playingNormal, label, false) == 0;
        }

        /// <see cref="YotogiOldPlayManager.PlayDrunkSingle"/>
        public static bool PlayDrunkSingleOld(MaidStatus.Old.Relation relation, YotogiOld.ExcitementStatus excitementStatus, Maid maid)
        {
            string label = string.Empty;
            if (relation == MaidStatus.Old.Relation.Tonus || relation == MaidStatus.Old.Relation.Contact)
            {
                switch (excitementStatus)
                {
                    case YotogiOld.ExcitementStatus.Minus:
                        label = "*酔いSA1";
                        break;
                    case YotogiOld.ExcitementStatus.Small:
                        label = "*酔いSA2";
                        break;
                    case YotogiOld.ExcitementStatus.Medium:
                        label = "*酔いSA3";
                        break;
                    case YotogiOld.ExcitementStatus.Large:
                        label = "*酔いSA4";
                        break;
                }
            }
            else if (relation == MaidStatus.Old.Relation.Trust)
            {
                switch (excitementStatus)
                {
                    case YotogiOld.ExcitementStatus.Minus:
                        label = "*酔いSA5";
                        break;
                    case YotogiOld.ExcitementStatus.Small:
                        label = "*酔いSA6";
                        break;
                    case YotogiOld.ExcitementStatus.Medium:
                        label = "*酔いSA7";
                        break;
                    case YotogiOld.ExcitementStatus.Large:
                        label = "*酔いSA8";
                        break;
                }
            }
            else if (relation == MaidStatus.Old.Relation.Lover || relation == MaidStatus.Old.Relation.Slave)
            {
                switch (excitementStatus)
                {
                    case YotogiOld.ExcitementStatus.Minus:
                        label = "*酔いSA9";
                        break;
                    case YotogiOld.ExcitementStatus.Small:
                        label = "*酔いSA10";
                        break;
                    case YotogiOld.ExcitementStatus.Medium:
                        label = "*酔いSA11";
                        break;
                    case YotogiOld.ExcitementStatus.Large:
                        label = "*酔いSA12";
                        break;
                }
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, playingNormal, label, false) == 0;
        }
        
        /// <see cref="YotogiOldPlayManager.PlayProclivitySingle"/>
        public static bool PlayProclivitySingleOld(YotogiOld.ExcitementStatus excitementStatus, Maid maid)
        {
            string label = string.Empty;
            switch (excitementStatus)
            {
                case YotogiOld.ExcitementStatus.Minus:
                    label = "*性癖SA1";
                    break;
                case YotogiOld.ExcitementStatus.Small:
                    label = "*性癖SA2";
                    break;
                case YotogiOld.ExcitementStatus.Medium:
                    label = "*性癖SA3";
                    break;
                case YotogiOld.ExcitementStatus.Large:
                    label = "*性癖SA4";
                    break;
            }
            return ParseRcFileVoice.playLabelWithMaids(maid, playingNormal, label, false) == 0;
        }

        /// <see cref="YotogiOldPlayManager.PlayRRFile"/>
        public static bool PlayRRFile(Yotogi.RRType rrtype, Maid maid)
        {
            string text = (!estrusMode) ? "*" : "*発情";
            text += rrtype.ToString();
            return ParseRcFileVoice.playLabelWithMaids(maid, playingRR, text, true) == 0;
        }

        /// <see cref="YotogiOldPlayManager.PlayRRFile"/>
        public static bool PlayRRFileOld(YotogiOld.RRType rrtype, Maid maid)
        {
            string label = "*" + rrtype.ToString();
            return ParseRcFileVoice.playLabelWithMaids(maid, playingRR, label, true) == 0;
        }


        public static void init(string normal, string rc, string rrc, string rr, YotogiPlayManager ypm, int playType)
        {
            playingNormal = normal;
            playingRC = rc;
            playingRRC = rrc;
            playingRR = rr;
            estrusMode = ypm != null && (bool)Traverse.Create(ypm).Field("estrusMode").GetValue();
            playFunc = playType;
        }

        /// <returns>0 成功 -1 未初始化 -2 无符合的标签 </returns>
        public static int playVoice(int playing_skill_level, MaidStatus.Relation relation, Yotogi.RCType rctype, Yotogi.RRCType rrctype, Maid maid)
        {
            switch (playFunc)
            {
                case 1: return PlayNormalContinue(playing_skill_level, relation, rctype, rrctype, maid) ? 0 : -2;
                case 2: return PlayDrunkContinue(relation, rctype, maid) ? 0 : -2;
                default: return -1;
            }
        }
        
        public static int playVoiceOld(int playing_skill_level, MaidStatus.Old.Relation relation, YotogiOld.RCType rctype, YotogiOld.RRCType rrctype, Maid maid)
        {
            switch (playFunc)
            {
                case 1: return PlayNormalContinueOld(playing_skill_level, relation, rctype, rrctype, maid) ? 0 : -2;
                case 2: return PlayDrunkContinueOld(rctype, relation, maid) ? 0 : -2;
                case 3: return PlayProclivityContinueOld(rctype, rrctype, maid) ? 0 : -2;
                default: return -1;
            }
        }
        
        public static int playRR(Yotogi.RRType rrtype, Maid maid)
        {
            return PlayRRFile(rrtype, maid) ? 0 : -2;
        }
        
        public static int playRROld(YotogiOld.RRType rrtype, Maid maid)
        {
            return PlayRRFileOld(rrtype, maid) ? 0 : -2;
        }

        public static int playSingle(int playing_skill_level, MaidStatus.Relation relation, Yotogi.ExcitementStatus excitementStatus, Maid maid)
        {
            switch (playFunc)
            {
                case 4: return PlayNormalSingle(playing_skill_level, relation, excitementStatus, maid) ? 0 : -2;
                case 5: return PlayDrunkSingle(relation, excitementStatus, maid) ? 0 : -2;
                case 6: return PlayProclivitySingle(excitementStatus, maid) ? 0 : -2;
                default: return -1;
            }
        }

        public static int playSingleOld(int playing_skill_level, MaidStatus.Old.Relation relation, YotogiOld.ExcitementStatus excitementStatus, Maid maid)
        {
            switch (playFunc)
            {
                case 4: return PlayNormalSingleOld(playing_skill_level, relation, excitementStatus, maid) ? 0 : -2;
                case 5: return PlayDrunkSingleOld(relation, excitementStatus, maid) ? 0 : -2;
                case 6: return PlayProclivitySingleOld(excitementStatus, maid) ? 0 : -2;
                default: return -1;
            }
        }

        public static void finish()
        {
            playFunc = 0;
        }
    }
}
