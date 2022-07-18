using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
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

    [Serializable]
    public class Metadata
    {
        public List<HatMetadata> hats;
    }

    [Serializable]
    public class HatMetadata
    {
        // The id of the hat (enum HatType)
        public int id; //TODO: change to name
        // The relative path to the asset bundle that contains the prefab
        public string hatPrefabBundle;
        // The name of the prefab inside the asset bundle
        public string hatPrefabName;
        public string debrisPrefabBundle;
        public string debrisPrefabName;

        //TODO: comment on these
        public bool isTall = false;
        public bool hideHair = false;
        // Where the hat is placed
        public Hat.HatAttachment hatAttachment = Hat.HatAttachment.HatJoint;
        // Which characters aren't allowed to wear this hat
        public List<CharacterType> bannedCharacters = new List<CharacterType>();
        //TODO: do we need the dlc?
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Start))]
    class GameManager_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameManager __instance)
        {
            try
            {
                //TODO: use the ID to dynamically assign ids instead of having them need to be set manually
                HatLoader.Log.LogMessage("Currently have these hats in hatLibrary:");
                foreach (var h in __instance.hatLibrary)
                    HatLoader.Log.LogMessage($"Type: {h.hatType} ({(int)h.hatType}), Prefab: {h.hatPrefab}, DebrisPrefab: {h.hatDebrisPrefab}, isTall: {h.isTall}, hideHair: {h.hideHair}, attachment: {h.hatAttachment}, bannedChars: {string.Join("-", h.bannedCharacters)}, dlc: {h.dlcID}");
                var id = Enum.GetValues(typeof(HatType)).Cast<HatType>().Max() + 1;
                HatLoader.Log.LogMessage($"Next free ID: {id}");


                // find all bundles by looking through all directories and finding "meta.xml" files under "BepInEx/plugins/<folder>/HatLoader/"
                HatLoader.Log.LogMessage("Now looking for custom hat files:");
                var basePath = Paths.PluginPath;
                // temp list to hold the original + our hats (so we dont have to allocate new arrays every time)
                var hatLibrary = __instance.hatLibrary.ToList();
                // cache asset bundles we've already loaded and access them by path //TODO: maybe instead cache the assets?
                var cachedBundles = new Dictionary<string, AssetBundle>();
                foreach (string dir in Directory.GetDirectories(basePath))
                {
                    var hatLoaderPath = Path.Combine(dir, "HatLoader");
                    if (!Directory.Exists(hatLoaderPath))
                        continue;

                    var metaPath = Path.Combine(hatLoaderPath, "meta.xml");
                    if (!File.Exists(metaPath))
                        continue;

                    // now parse that metadata file
                    HatLoader.Log.LogMessage($"Found metadata: {metaPath}");
                    var hats = ParseMetadata(metaPath, cachedBundles);
                    HatLoader.Log.LogMessage($"Contains {hats.Count} Hats!");

                    // add the hats we found into the library
                    foreach (var hat in hats)
                    {
                        hatLibrary.Add(hat);
                        // FIXME: this doesnt require hatLibrary to be replaced, but may break shit on update?
                        //__instance.UnlockHatForCharacter(CharacterType.Banana, id, false); //TODO: comment, doesnt work anyway because the save is loaded after that
                    }

                    // now replace the original array with our "enhanced" list
                    __instance.hatLibrary = hatLibrary.ToArray();
                }
            }
            catch (Exception e)
            {
                HatLoader.Log.LogMessage($"Exception during GameManager.Start hook: {e}");
            }
        }


        private static UnityEngine.Object GetAssetFromBundle(string basePath, Dictionary<string, AssetBundle> cachedBundles, string bundleName, string assetName)
        {
            if (!string.IsNullOrEmpty(bundleName))
            {
                AssetBundle bundle;
                // check if the bundle cached
                var bundlePath = Path.Combine(basePath, bundleName);
                if (!cachedBundles.TryGetValue(bundlePath, out bundle))
                {
                    // if not load it
                    if (File.Exists(bundlePath))
                    {
                        //TODO: error handling?
                        bundle = AssetBundle.LoadFromFile(bundlePath);
                        // cache it
                        cachedBundles[bundlePath] = bundle;
                    }
                    else
                    {
                        HatLoader.Log.LogWarning($"Couldn't find the asset bundle {bundlePath} ({bundleName})");
                        return null;
                    }
                }
                // now load the asset from the bundle
                if (!string.IsNullOrEmpty(assetName))
                {
                    var asset = bundle.LoadAsset(assetName);
                    if (asset == null)
                        HatLoader.Log.LogWarning($"Failed loading asset {assetName} from {bundle} ({bundleName})");

                    return asset;
                }
                else
                {
                    HatLoader.Log.LogWarning("assetName is empty or null!");
                }
            }
            return null;
        }

        // INFO: why are we using xml? cause JsonUtility sucks and can't properly work with lists or arrays. Thanks unity
        // also i dont wanna carry dependencies with us, so newtonsoft falls away too
        private static List<Hat> ParseMetadata(string metaPath, Dictionary<string, AssetBundle> cachedBundles)
        {
            List<Hat> hats = new List<Hat>();
            var dirPath = Path.GetDirectoryName(metaPath);

            HatLoader.Log.LogInfo("Parsing metadata");
            XmlSerializer xml = new XmlSerializer(typeof(Metadata));
            StreamReader sr = new StreamReader(metaPath);
            // Parse the file
            Metadata meta;
            try
            {
                meta = xml.Deserialize(sr) as Metadata;
            } 
            catch (Exception e)
            {
                HatLoader.Log.LogWarning($"{metaPath} couldn't be parsed properly: {e}");
                return hats;
            }
            if (meta.hats == null)
            {
                HatLoader.Log.LogWarning($"{metaPath} doesn't contain a \"hats\" array.");
                return hats;
            }
            // Now look at the hats inside
            foreach (var hatMeta in meta.hats)
            {
                // Create a new hat with the properties we can copy directly over
                var hat = new Hat()
                {
                    hatType = (HatType)hatMeta.id,
                    isTall = hatMeta.isTall,
                    hideHair = hatMeta.hideHair,
                    hatAttachment = hatMeta.hatAttachment,
                    dlcID = DownloadableContentID.None,
                    bannedCharacters = hatMeta.bannedCharacters,
                };
                HatLoader.Log.LogInfo($"Parsing hat with id {(int)hat.hatType}");
                // Parse the prefab from the asset bundle
                var prefab = GetAssetFromBundle(dirPath, cachedBundles, hatMeta.hatPrefabBundle, hatMeta.hatPrefabName);
                if (prefab != null)
                    hat.hatPrefab = prefab as GameObject;
                // The same for the debris prefab
                var debris = GetAssetFromBundle(dirPath, cachedBundles, hatMeta.debrisPrefabBundle, hatMeta.debrisPrefabName);
                if (debris != null)
                    hat.hatDebrisPrefab = debris as Debris3D;

                HatLoader.Log.LogMessage($"Loading new Hat: Type: {hat.hatType} ({(int)hat.hatType}), Prefab: {hat.hatPrefab}, DebrisPrefab: {hat.hatDebrisPrefab}, isTall: {hat.isTall}, hideHair: {hat.hideHair}, attachment: {hat.hatAttachment}, bannedChars: {string.Join("-", hat.bannedCharacters)}, dlc: {hat.dlcID}");
                //TODO: should we really allow hats that have neither prefabs nor debrisprefabs?
                // Add it to our list
                hats.Add(hat);
            }
            return hats;
        }
    }
}
