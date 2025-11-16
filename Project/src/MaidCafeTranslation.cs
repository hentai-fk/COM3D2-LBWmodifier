using HarmonyLib;
using System;
using System.Collections;

namespace LBWmodifier
{
    public class MaidCafeTranslation
    {
        private static Harmony harmony0;

        public static void load()
        {
            var AddCommentData = AccessTools.Method("MaidCafe.MaidCafeStreamManager:AddCommentData");
            var PlayCommentFile = AccessTools.Method("MaidCafe.MaidCafeStreamManager:PlayCommentFile");
            var localCommentData = typeof(MaidCafeTranslation).GetMethod(nameof(MaidCafeTranslation.AddCommentData));
            if (AddCommentData != null && PlayCommentFile != null)
            {
                harmony0 = new Harmony($"MaidCafeTranslation-{Guid.NewGuid()}");
                if (AddCommentData != null) harmony0.Patch(AddCommentData, null, new HarmonyMethod(localCommentData));
                if (AddCommentData != null) harmony0.Patch(PlayCommentFile, null, new HarmonyMethod(localCommentData));
            }
        }

        public static void unload()
        {
            harmony0?.UnpatchSelf();
        }

        public static void AddCommentData(MaidCafe.MaidCafeStreamManager __instance)
        {
            var commentList = Traverse.Create(__instance).Field("m_commentList").GetValue() as IList;
            for (int i = 0; i < commentList.Count; i++)
            {
                if (Translation.TranslateText(Traverse.Create(commentList[i]).Field("comment").GetValue() as string, out var translation) == Translation.RESULT.SUCCESS)
                    Traverse.Create(commentList[i]).Field("comment").SetValue(translation);
            }
        }
    }
}
