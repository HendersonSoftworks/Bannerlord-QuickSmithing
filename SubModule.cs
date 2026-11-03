using System;
using System.IO;
using System.Text;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace QuickSmithing
{
    public class SubModule : MBSubModuleBase
    {
        internal const string HarmonyId = "com.quicksmithing.bulkrefine";

        private const string LogFileName = "QuickSmithing.log";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                harmony.PatchAll(typeof(SubModule).Assembly);

                StringBuilder patched = new StringBuilder();
                foreach (System.Reflection.MethodBase method in harmony.GetPatchedMethods())
                {
                    if (patched.Length > 0)
                    {
                        patched.Append(", ");
                    }

                    patched.Append(method.DeclaringType?.FullName).Append('.').Append(method.Name);
                }

                //Log("Harmony patches applied: " + (patched.Length == 0 ? "(none)" : patched.ToString()));
            }
            catch (Exception exception)
            {
                //Log("Harmony patching FAILED: " + exception);
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            //Log("Quick Smithing loaded.");
            Message("Quick Smithing loaded: hold Shift to refine 10, hold Ctrl to refine all.");
        }

        internal static void Message(string text)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(text));
            }
            catch
            {
                // Never let a notification break the game.
            }
        }

        // internal static void Log(string text)
        // {
        //     string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + text + Environment.NewLine;

        //     try
        //     {
        //         string assemblyDirectory = Path.GetDirectoryName(typeof(SubModule).Assembly.Location);
        //         if (!string.IsNullOrEmpty(assemblyDirectory) &&
        //             TryAppend(Path.Combine(assemblyDirectory, LogFileName), line))
        //         {
        //             return;
        //         }
        //     }
        //     catch
        //     {
        //         // Fall through to the Documents folder.
        //     }

        //     try
        //     {
        //         string documents = Path.Combine(
        //             Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        //             "Mount and Blade II Bannerlord");

        //         if (Directory.Exists(documents))
        //         {
        //             TryAppend(Path.Combine(documents, LogFileName), line);
        //         }
        //     }
        //     catch
        //     {
        //         // Logging must never break the game.
        //     }
        // }

        private static bool TryAppend(string path, string line)
        {
            try
            {
                File.AppendAllText(path, line);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(CraftingCampaignBehavior), "DoRefinement")]
    internal static class RefinementBatchPatch
    {
        private const int ShiftRefineAmount = 10;

        [ThreadStatic]
        private static bool _isBulkRefining;

        private static bool _diagnosticShown;

        [HarmonyPostfix]
        private static void Postfix(
            CraftingCampaignBehavior __instance,
            Hero hero,
            Crafting.RefiningFormula refineFormula)
        {
            // Extra refinements triggered by the loop below must not start another loop.
            if (_isBulkRefining)
            {
                return;
            }

            bool shift = IsAnyKeyDown(InputKey.LeftShift, InputKey.RightShift);
            bool control = IsAnyKeyDown(InputKey.LeftControl, InputKey.RightControl);

            if (!_diagnosticShown)
            {
                _diagnosticShown = true;
                //SubModule.Log("DoRefinement hook ran. Shift detected: " + shift + ", Ctrl detected: " + control + ".");
                SubModule.Message(
                    "Quick Smithing: refine hook active (Shift detected: " + (shift ? "yes" : "no") + ").");
            }

            int requestedCount = control ? int.MaxValue : (shift ? ShiftRefineAmount : 1);
            if (requestedCount <= 1)
            {
                return;
            }

            // The game already performed one refinement, so only the remainder is left.
            int remainingCount = GetAvailableRefineCount(refineFormula);
            int extraCount = control
                ? remainingCount
                : Math.Min(requestedCount - 1, remainingCount);

            if (extraCount <= 0)
            {
                return;
            }

            int completedCount = 0;
            _isBulkRefining = true;
            try
            {
                for (int i = 0; i < extraCount; i++)
                {
                    if (__instance.GetHeroCraftingStamina(hero) <= 0)
                    {
                        break;
                    }

                    __instance.DoRefinement(hero, refineFormula);
                    completedCount++;
                }
            }
            catch (Exception exception)
            {
                //SubModule.Log("Bulk refine failed: " + exception);
            }
            finally
            {
                _isBulkRefining = false;
            }

            // SubModule.Log(
            //     "Bulk refine: shift=" + shift + ", ctrl=" + control +
            //     ", extra=" + extraCount + ", completed=" + completedCount + ".");

            if (completedCount > 0)
            {
                SubModule.Message("Refined " + (completedCount + 1) + " times.");
            }
        }

        private static bool IsAnyKeyDown(InputKey first, InputKey second)
        {
            return Input.IsKeyDown(first) || Input.IsKeyDown(second);
        }

        private static int GetAvailableRefineCount(Crafting.RefiningFormula formula)
        {
            ItemRoster itemRoster = MobileParty.MainParty?.ItemRoster;
            SmithingModel smithingModel = Campaign.Current?.Models?.SmithingModel;

            if (itemRoster == null || smithingModel == null)
            {
                return 0;
            }

            bool hasInput = false;
            int count = int.MaxValue;

            if (formula.Input1Count > 0)
            {
                hasInput = true;
                count = Math.Min(
                    count,
                    GetAvailableForMaterial(formula.Input1, formula.Input1Count, itemRoster, smithingModel));
            }

            if (formula.Input2Count > 0)
            {
                hasInput = true;
                count = Math.Min(
                    count,
                    GetAvailableForMaterial(formula.Input2, formula.Input2Count, itemRoster, smithingModel));
            }

            return hasInput ? Math.Max(0, count) : 0;
        }

        private static int GetAvailableForMaterial(
            CraftingMaterials material,
            int amountPerRefinement,
            ItemRoster itemRoster,
            SmithingModel smithingModel)
        {
            if (amountPerRefinement <= 0)
            {
                return int.MaxValue;
            }

            ItemObject materialItem = smithingModel.GetCraftingMaterialItem(material);
            if (materialItem == null)
            {
                return 0;
            }

            return itemRoster.GetItemNumber(materialItem) / amountPerRefinement;
        }
    }
}