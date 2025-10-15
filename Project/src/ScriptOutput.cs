using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static UITweener;

namespace LBWmodifier
{
    public class ScriptOutput
    {
        // https://zodgame.xyz/forum.php?mod=viewthread&tid=418785
        public static readonly string[] 有用的CSV文件 = new string[] 
        {
            "character_preset_basedata.nei",
            "dance_enabled_list.nei",
            "dance_freecamera_setting.nei",
            "dance_setting.nei",
            "dance_singpart_setting.nei",
            "desk_item_category.nei",
            "desk_item_detail.nei",
            "desk_item_enabled_id.nei",
            "edit_bg.nei",
            "edit_bg_enabled_list.nei",
            "edit_category_define.nei",
            "edit_collabo_category.nei",
            "edit_custom_view.nei",
            "edit_mask_define.nei",
            "edit_pose.nei",
            "edit_pose_enabled_list.nei",
            "edit_voice.nei",
            "empire_life_mode_list.nei",
            "game_in_shop_bg_setting.nei",
            "game_in_shop_building_setting.nei",
            "game_in_shop_category_list.nei",
            "game_in_shop_clubgrade_setting.nei",
            "game_in_shop_event_item_setting.nei",
            "game_in_shop_item_groups_setting.nei",
            "game_in_shop_item_setting.nei",
            "game_in_shop_itemlist.nei",
            "game_in_shop_setcard_setting.nei",
            "honeymoonmode_event_list.nei",
            "honeymoonmode_location_list.nei",
            "init_keiken_num_setting.nei",
            "maid_personaleventblocker_list.nei",
            "maid_personaleventblocker_list_jp.nei",
            "maid_status_feature_correction.nei",
            "maid_status_feature_enabled_list.nei",
            "maid_status_feature_list.nei",
            "maid_status_jobclass_acquired_condition.nei",
            "maid_status_jobclass_bonus.nei",
            "maid_status_jobclass_enabled_list.nei",
            "maid_status_jobclass_experiences.nei",
            "maid_status_jobclass_list.nei",
            "maid_status_personal_additionalrelation_enabled_list.nei",
            "maid_status_personal_enabled_list.nei",
            "maid_status_personal_feature_condition.nei",
            "maid_status_personal_list.nei",
            "maid_status_propensity_correction.nei",
            "maid_status_propensity_enabled_list.nei",
            "maid_status_propensity_list.nei",
            "maid_status_submaid_enabled_list.nei",
            "maid_status_submaid_kiss_side_list.nei",
            "maid_status_submaid_list.nei",
            "maid_status_title_list.nei",
            "maid_status_yotogiclass_acquired_condition.nei",
            "maid_status_yotogiclass_bonus.nei",
            "maid_status_yotogiclass_enabled_list.nei",
            "maid_status_yotogiclass_experiences.nei",
            "maid_status_yotogiclass_list.nei",
            "maid_status_yotogiclass_transplant.nei",
            "npcedit_enabled_list.nei",
            "npcedit_list.nei",
            "npcmanedit_enabled_list.nei",
            "npcmanedit_list.nei",
            "parts_swap_list.nei",
            "phot_alignmentpreset_list.nei",
            "phot_bg_enabled_list.nei",
            "phot_bg_list.nei",
            "phot_bg_object_enabled_list.nei",
            "phot_bg_object_list.nei",
            "phot_face_list.nei",
            "phot_motion_enabled_list.nei",
            "phot_motion_list.nei",
            "phot_scenepreset_list.nei",
            "phot_sound_list.nei",
            "phot_undressing_list.nei",
            "phot_undressing_realman_list.nei",
            "plugin_check_list.nei",
            "private_maidmode_event_list.nei",
            "private_maidmode_eventinformation_list.nei",
            "private_maidmode_eventlink_list.nei",
            "private_maidmode_group_list.nei",
            "private_maidmode_location_list.nei",
            "private_maidmode_personalcheck_list.nei",
            "private_maidmode_touch_list.nei",
            "profile_comment_1-7.nei",
            "random_preset_mpn_map.nei",
            "random_preset_parameter_define.nei",
            "recollection_enabled_id_list.nei",
            "recollection_legacy_disable.nei",
            "recollection_life_mode.nei",
            "recollection_normal2.nei",
            "recollection_story.nei",
            "recollection_subheroine.nei",
            "recollection_vip2.nei",
            "scenario_get_item_list.nei",
            "schedule_define.nei",
            "schedule_entertain_guest.nei",
            "schedule_entertain_number.nei",
            "schedule_init_night.nei",
            "schedule_init_noon.nei",
            "schedule_work_easyyotogi.nei",
            "schedule_work_facility.nei",
            "schedule_work_facility_enabled.nei",
            "schedule_work_legacy_disable.nei",
            "schedule_work_netorare.nei",
            "schedule_work_night.nei",
            "schedule_work_night_category_list.nei",
            "schedule_work_night_enabled.nei",
            "schedule_work_night_legacy.nei",
            "schedule_work_noon.nei",
            "schedule_work_noon_enabled.nei",
            "schedule_work_noon_legacy.nei",
            "scoutmode_personal_enabled_list.nei",
            "staffroll_data.nei",
            "staffroll_stafflist.nei",
            "staffroll_stafflist_ch.nei",
            "staffroll_stafflist_en.nei",
            "staffroll_stafflist_enpublic.nei",
            "staffroll_stafflist_public.nei",
            "trophy_enabled_list.nei",
            "trophy_list.nei",
            "yotogi_play_undressing.nei",
            "yotogi_skill_acquisition.nei",
            "yotogi_skill_command_data.nei",
            "yotogi_skill_command_status.nei",
            "yotogi_skill_enabled_list.nei",
            "yotogi_skill_list.nei",
            "yotogi_skill_select_wait_motion.nei",
            "yotogi_stage_compatibility_list.nei",
            "yotogi_stage_enabled_list.nei",
            "yotogi_stage_list.nei",
        };

        public static void WriteTranslatedCSV(string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var nei in 有用的CSV文件)
            {
                if (nei == null || !nei.ToLower().EndsWith(".nei"))
                    continue;
                using (var avi = FileTool.OpenFileCompatible(nei))
                {
                    if (avi == null)
                        continue;
                    using (var csv = new CsvParser())
                    {
                        if (!csv.Open(avi.file))
                            continue;
                        var origArr = new string[csv.max_cell_y][];
                        var transArr = new string[csv.max_cell_y][];
                        for (int y = 0; y < csv.max_cell_y; y++)
                        {
                            origArr[y] = new string[csv.max_cell_x];
                            transArr[y] = new string[csv.max_cell_x];
                            for (int x = 0; x < csv.max_cell_x; x++)
                            {
                                origArr[y][x] = csv.GetCellAsString(x, y);
                                transArr[y][x] = getInitedTranslation(origArr[y][x]);
                            }
                        }
                        CSVUtils.SaveArray2CSVFile(Path.Combine(dir, nei.Substring(0, nei.Length - 4) + "-原始.csv"), origArr);
                        CSVUtils.SaveArray2CSVFile(Path.Combine(dir, nei.Substring(0, nei.Length - 4) + "-翻译.csv"), transArr);
                        if (origArr.Length > 0 )
                        {
                            CSVUtils.SaveArray2CSVFile(Path.Combine(dir, nei.Substring(0, nei.Length - 3) + "csv"), new string[][] { origArr[0] });
                        }
                    }
                }
            }
            LBWmodifier.logger.LogInfo("Dump simple CSV to " + Path.GetFullPath(dir));
        }

        public static void WriteCSV2Directory(string dir, AFileSystemBase system)
        {
            foreach (var nei in system.GetFileListAtExtension("nei"))
            {
                using (var fd = system.FileOpen(nei))
                {
                    if (fd == null)
                        continue;
                    using (var csv = new CsvParser())
                    {
                        if (!csv.Open(fd))
                            continue;
                        var origArr = new string[csv.max_cell_y][];
                        for (int y = 0; y < csv.max_cell_y; y++)
                        {
                            origArr[y] = new string[csv.max_cell_x];
                            for (int x = 0; x < csv.max_cell_x; x++)
                            {
                                origArr[y][x] = csv.GetCellAsString(x, y);
                            }
                        }
                        var filePath = Path.Combine(dir, nei.Substring(0, nei.Length - 3) + "csv");
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                        CSVUtils.SaveArray2CSVFile(filePath, origArr);
                        LBWmodifier.logger.LogInfo("Dump CSV to " + Path.GetFullPath(filePath));
                    }
                }
            }
        }

        public static void WriteKS2Directory(string dir, AFileSystemBase system)
        {
            foreach (var ks in system.GetFileListAtExtension("ks"))
            {
                Console.WriteLine("WriteKS2Directory write " + ks);
                using (var fd = system.FileOpen(ks))
                {
                    if (fd == null)
                        continue;
                    var filePath = Path.Combine(dir, ks);
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    File.WriteAllText(filePath, NUty.SjisToUnicode(fd.ReadAll()), Encoding.UTF8);
                    LBWmodifier.logger.LogInfo("Dump KS to " + Path.GetFullPath(filePath));
                }
            }
        }

        private static MethodInfo[][] matchedMethodInfo;

        public static string getInitedTranslation(string text)
        {
            if (matchedMethodInfo == null)
            {
                foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
                {
                    /// <see cref="Loader.Translator"/>
                    var loaderGuid = (ass.GetType("LBWmodifier.Loader.Translator")?.GetCustomAttributes(typeof(BepInPlugin), true)?.FirstOrDefault() as BepInPlugin)?.GUID;
                    if (loaderGuid != "lbwnb.translation")
                        continue;
                    var typeList = new List<MethodInfo[]>();
                    foreach (var type in ass.GetTypes())
                    {
                        if (type.Name != nameof(Translation))
                            continue;
                        var method1 = type.GetMethod(nameof(Translation.isInited));
                        var method2 = type.GetMethod(nameof(Translation.TranslateText));
                        if (method1 == null || method2 == null)
                            continue;
                        if (method1.ReturnType != typeof(bool) || !method2.ReturnType.IsEnum)
                            continue;
                        if (method1.GetParameters().Length != 0 || method2.GetParameters().Length != 2)
                            continue;
                        if (method2.GetParameters()[0].ParameterType != typeof(string) || 
                            method2.GetParameters()[1].ParameterType != typeof(string).MakeByRefType())
                            continue;
                        typeList.Add(new MethodInfo[] { method1, method2 });
                    }
                    if (typeList.Count > 0)
                    {
                        matchedMethodInfo = typeList.ToArray();
                        break;
                    }
                }
                if (matchedMethodInfo == null)
                    matchedMethodInfo = new MethodInfo[0][];
            }
            foreach (var methods in matchedMethodInfo)
            {
                if (!(bool)methods[0].Invoke(null, null))
                    continue;
                var param = new object[] { text, null };
                var result = (Translation.RESULT)methods[1].Invoke(null, param);
                return result == Translation.RESULT.SUCCESS ? (string)param[1] : text;
            }
            return text;
        }
    }
}
