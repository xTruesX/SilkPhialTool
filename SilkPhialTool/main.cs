using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Needleforge;
using Needleforge.Data;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TeamCherry.Localization;
using UnityEngine;

[BepInPlugin("com.xTruesX.silkphialmod", "Silk Phial Mod", "1.0.0")]
public class main : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo("Plugin loaded");
        Harmony.CreateAndPatchAll(typeof(main));
        Harmony.CreateAndPatchAll(typeof(SilkPhial_UsableWhenEmpty_Patch));
        Harmony.CreateAndPatchAll(typeof(ToolItem_IsUnlockedTest_NullFix));
        Harmony.CreateAndPatchAll(typeof(PlayerData_CountGameCompletionFixForSiPhi));
        Harmony.CreateAndPatchAll(typeof(SilkPhial_ForceText_NoSheetKey));

        // AssetBundle
        var bundle = AssetBundle.LoadFromFile(
            Path.Combine(Paths.PluginPath, "silkphial")
        );

        var full = bundle.LoadAsset<Sprite>("SilkPhial");
        var fullhud = bundle.LoadAsset<Sprite>("SilkPhialHud");
        var poison = bundle.LoadAsset<Sprite>("SilkPhial_Poison");
        var poisonhud = bundle.LoadAsset<Sprite>("SilkPhial_PoisonHud");
        var empty = bundle.LoadAsset<Sprite>("SilkPhial_Empty");
        var emptyhud = bundle.LoadAsset<Sprite>("SilkPhial_EmptyHud");

        var fullSprites = new StateSprites
        {
            InventorySprite = full,
            HudSprite = fullhud,
            PoisonInventorySprite = poison,
            PoisonHudSprite = poisonhud
        };

        var emptySprites = new StateSprites
        {
            InventorySprite = empty,
            HudSprite = emptyhud,
            PoisonInventorySprite = empty,
            PoisonHudSprite = emptyhud
        };

        var liquidTool = NeedleforgePlugin.AddLiquidTool(
            name: "Silk Phial",
            maxRefills: 10,
            storageAmount: 4,
            InfiniteRefillsPD: "SilkPhial_InfiniteBench",
            liquidColor: Color.white,
            resource: ToolItem.ReplenishResources.Shard,
            replenishUsage: ToolItem.ReplenishUsages.Percentage,
            replenishMult: 1f,
            fullSprites: fullSprites,
            emptySprites: emptySprites,
            clip: "Reserve BindBurst Air"
        );

        int CrSilk = 1;

        PlayerData.instance.ToolLiquids.SetData(
            "Silk Phial",
            new ToolItemLiquidsData.Data
            {
                RefillsLeft = 10,
                UsedExtra = false,
                SeenEmptyState = false
            }
        );

        liquidTool.displayName = new LocalisedString { Sheet = "silkphial", Key = "Display" };
        liquidTool.description = new LocalisedString { Key = "desc", Sheet = "silkphial" };

        var liquidData = liquidTool;

        liquidData.beforeAnim = () =>
        {
            var item = liquidData.Item;
            if (item == null)
                return;

            if (item.SavedData.AmountLeft <= 0)
            {
                var binding = ToolItemManager.GetAttackToolBinding(item);
                if (binding.HasValue)
                {
                    ToolItemManager.ReportBoundAttackToolFailed(binding.Value);
                }
                return;
            }
        };
        liquidTool.afterAnim = () =>
        {
            if (HeroController.instance == null)
                return;

            if (PlayerData.instance.CurrentCrestID == "Toolmaster")
            {
                PlayerData.instance.silk += CrSilk;
                if (CrSilk == 1)
                    CrSilk = 2;
                else
                    CrSilk = 1;
            }
            else
            {
                PlayerData.instance.silk += 3;
            }
            HeroController.instance.AddSilk(0, false);
        };
        StartCoroutine(BenchRefillWatcher());
    }

    private void Update()
    {
    }

    private IEnumerator BenchRefillWatcher()
    {
        while (HeroController.instance == null || PlayerData.instance == null)
            yield return null;

        string toolName = "Silk Phial";
        bool wasAtBench = false;

        while (true)
        {
            bool atBench = PlayerData.instance.atBench;

            if (atBench && !wasAtBench)
            {
                ToolItemStatesLiquid tool = null;

                foreach (var t in NeedleforgePlugin.newTools)
                {
                    if (t.name == toolName)
                    {
                        tool = t as ToolItemStatesLiquid;
                        break;
                    }
                }

                if (!tool.IsUnlocked)
                {
                    tool.Unlock();
                }

                if (tool != null)
                {
                    /*var saved = tool.SavedData;
                    saved.AmountLeft = ToolItemManager.GetToolStorageAmount(tool);
                    tool.SavedData = saved;*/

                    var liquid = PlayerData.instance.ToolLiquids.GetData(toolName);
                    liquid.RefillsLeft = tool.RefillsMax;
                    liquid.UsedExtra = false;
                    liquid.SeenEmptyState = true;

                    PlayerData.instance.ToolLiquids.SetData(toolName, liquid);

                }
                else
                {
                    Logger.LogError("Bench refill: Tool N/A");
                }
            }

            wasAtBench = atBench;
            yield return null;
        }
    }

    private void SetPrivate(object obj, string fieldName, object value)
    {
        if (obj == null)
            throw new Exception("SetPrivate: obj is null");

        Type t = obj.GetType();

        while (t != null)
        {
            var field = t.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public
            );

            if (field != null)
            {
                field.SetValue(obj, value);
                return;
            }

            t = t.BaseType;
        }

        throw new Exception(
            $"SetPrivate: Field '{fieldName}' not found on {obj.GetType()}"
        );
    }

    [HarmonyPatch(typeof(ToolItemStatesLiquid))]
    [HarmonyPatch(nameof(ToolItemStatesLiquid.UsableWhenEmpty), MethodType.Getter)]
    class SilkPhial_UsableWhenEmpty_Patch
    {
        static void Postfix(ToolItemStatesLiquid __instance, ref bool __result)
        {
            if (__instance.name != "Silk Phial")
                return;

            if (__instance.SavedData.AmountLeft <= 0)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(ToolItem), "get_IsUnlockedTest")]
    internal static class ToolItem_IsUnlockedTest_NullFix
    {
        static bool Prefix(ToolItem __instance, ref bool __result)
        {
            var field = AccessTools.Field(typeof(ToolItem), "alternateUnlockedTest");
            var test = field?.GetValue(__instance);

            if (test == null)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.CountGameCompletion))]
    internal static class PlayerData_CountGameCompletionFixForSiPhi
    {
        static void Postfix(PlayerData __instance)
        {
            var silkPhial = ToolItemManager.GetToolByName("Silk Phial");

            if (silkPhial != null && silkPhial.IsUnlocked)
            {
                __instance.completionPercentage -= 1f;
            }
        }
    }

    [HarmonyPatch(typeof(LocalisedString))]
    [HarmonyPatch(nameof(LocalisedString.ToString), typeof(bool))]
    internal static class SilkPhial_ForceText_NoSheetKey
    {
        static void Postfix(LocalisedString __instance, ref string __result)
        {
            if (PlayerData.instance == null)
                return;

            var tool = ToolItemManager.GetToolByName("Silk Phial");
            if (tool == null || !tool.IsUnlocked)
                return;

            string current = __result;

            if (current.Contains("SilkPhial") ||
                current.Contains("silkphial") ||
                current.Contains("!!"))
            {
                if (current.ToLower().Contains("display") || current.Contains("Name"))
                {
                    __result = "Silk Phial";
                }
                else if (current.ToLower().Contains("desc"))
                {
                    if (PlayerData.instance.CurrentCrestID == "Toolmaster")
                        __result = "A phial filled to the brim with Silk. Used to inject silk into a bug's shell \n\nGives 1-2 spools of silk when injected.";
                    else
                        __result = "A phial filled to the brim with Silk. Used to inject silk into a bug's shell \n\nGives 3 spools of silk when injected.";
                }
            }
        }
    }
}
