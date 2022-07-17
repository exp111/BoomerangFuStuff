using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HatLoader
{
    [BepInPlugin(ID, NAME, VERSION)]
    public class HatLoader : BaseUnityPlugin
    {
        public const string ID = "com.exp111.HatLoader";
        public const string NAME = "HatLoader";
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
                Log.LogMessage($"Exception during HatLoader.Awake: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Start))]
    class GameManager_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameManager __instance)
        {
            try
            {
                // find all bundles
                var basePath = Paths.PluginPath;
                // loads the bundle "test" from "BepInEx/plugins/exp111-HatLoader/Content/"
                var path = Path.Combine(basePath, "exp111-HatLoader", "Content", "hats");
                var assetBundle = AssetBundle.LoadFromFile(path);

                //TODO: do this with jsons or smth where you specify the prefab and all the metadata
                // then look through the bundle
                var assets = assetBundle.LoadAllAssets();
                GameObject prefab = null;
                foreach (var asset in assets)
                {
                    HatLoader.Log.LogMessage($"Found asset: {asset}, type: {asset.GetType()}");
                    if (asset is GameObject)
                    {
                        prefab = (GameObject)asset;
                        HatLoader.Log.LogMessage($"Found GameObject: {asset}");
                    }
                }

                //TODO: add hats
                HatLoader.Log.LogMessage("Currently have these hats in hatLibrary:");
                foreach (var h in __instance.hatLibrary)
                    HatLoader.Log.LogMessage($"Type: {h.hatType} ({(int)h.hatType}), Prefab: {h.hatPrefab}, DebrisPrefab: {h.hatDebrisPrefab}, isTall: {h.isTall}, hideHair: {h.hideHair}, attachment: {h.hatAttachment}, bannedChars: {(string.Join(", ", h.bannedCharacters))}, dlc: {h.dlcID}");

                HatLoader.Log.LogMessage("Adding our own now:");
                var id = Enum.GetValues(typeof(HatType)).Cast<HatType>().Max() + 1;
                HatLoader.Log.LogMessage($"ID: {id}");
                var hat = new Hat()
                {
                    hatType = id,
                    hatPrefab = prefab,
                    hatDebrisPrefab = null,
                    isTall = true, //TODO: what does this affect?
                    hideHair = true,
                    hatAttachment = Hat.HatAttachment.HatJoint,
                    bannedCharacters = new List<CharacterType>() { },
                    dlcID = DownloadableContentID.None
                };
                __instance.hatLibrary = __instance.hatLibrary.AddToArray(hat);
                __instance.UnlockHatForCharacter(CharacterType.Banana, id, false);
            }
            catch (Exception e)
            {
                HatLoader.Log.LogMessage($"Exception during GameManager.Start hook: {e}");
            }
        }
    }
}
