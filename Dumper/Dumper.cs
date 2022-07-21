using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Dumper
{
    [BepInPlugin(ID, NAME, VERSION)]
    public class Dumper : BaseUnityPlugin
    {
        public const string ID = "com.exp111.Dumper";
        public const string NAME = "Dumper";
        public const string VERSION = "1.0.0";

        public static ManualLogSource Log;

        private void Awake()
        {
            try
            {
                Log = Logger;
                Log.LogMessage("Awake");

                var harmony = new Harmony(ID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Log.LogError($"Exception during HatLoader.Awake: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Start))]
    class GameManager_Start_Patch
    {
        // Run before any other plugin
        [HarmonyPriority(Priority.First)]
        [HarmonyPostfix]
        public static void Postfix(GameManager __instance)
        {
            try
            {
                Dumper.Log.LogMessage("Currently have these hats in hatLibrary:");
                foreach (var h in __instance.hatLibrary)
                    Dumper.Log.LogMessage($"Type: {h.hatType} ({(int)h.hatType}), Prefab: {h.hatPrefab}, DebrisPrefab: {h.hatDebrisPrefab}, isTall: {h.isTall}, hideHair: {h.hideHair}, attachment: {h.hatAttachment}, bannedChars: {string.Join("-", h.bannedCharacters)}, dlc: {h.dlcID}");
            }
            catch (Exception e)
            {
                Dumper.Log.LogError($"Exception during GameManager.Start hook: {e}");
            }
        }
    }
}