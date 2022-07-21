using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        public const string CustomHatsSaveFile = "hatLoader.xml";
#if DEBUG
        private ConfigEntry<KeyboardShortcut> LevelUpKey { get; set; }
#endif
        // bidirectional name<->ID dicts
        public static int nextFreeID = (int)Enum.GetValues(typeof(HatType)).Cast<HatType>().Max() + 1;
        public static Dictionary<string, int> Name2ID = new Dictionary<string, int>();
        public static Dictionary<int, string> ID2Name = new Dictionary<int, string>();


        public static ManualLogSource Log;

        private void Awake()
        {
            try
            {
                Log = Logger;
                Log.LogMessage("Awake");

#if DEBUG
                LevelUpKey = Config.Bind("Hotkeys", "LevelUp", new KeyboardShortcut(KeyCode.U, KeyCode.LeftControl));
#endif

                var harmony = new Harmony(ID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Log.LogError($"Exception during HatLoader.Awake: {ex}");
            }
        }

        public static int GetNextHatID(string name)
        {
            Name2ID[name] = nextFreeID;
            ID2Name[nextFreeID] = name;
            return nextFreeID++;
        }

#if DEBUG
        private void Update()
        {
            // Get a random hat
            if (LevelUpKey.Value.IsDown())
            {
                Singleton<GameManager>.Instance.UnlockHat();
            }
        }
#endif

        [Conditional("DEBUG")]
        public static void DebugLog(string text, LogLevel logLevel = LogLevel.Info)
        {
            Log.Log(logLevel, text);
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
        // The (inside of your metadata unique) name of the hat
        public string name; 
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

    // Injects our hats on game startup
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Start))]
    class GameManager_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameManager __instance)
        {
            try
            {
                HatLoader.DebugLog("Currently have these hats in hatLibrary:");
                foreach (var h in __instance.hatLibrary)
                    HatLoader.DebugLog($"Type: {h.hatType} ({(int)h.hatType}), Prefab: {h.hatPrefab}, DebrisPrefab: {h.hatDebrisPrefab}, isTall: {h.isTall}, hideHair: {h.hideHair}, attachment: {h.hatAttachment}, bannedChars: {string.Join("-", h.bannedCharacters)}, dlc: {h.dlcID}");
                HatLoader.DebugLog($"Next free ID: {HatLoader.nextFreeID}");


                // find all bundles by looking through all directories and finding "meta.xml" files under "BepInEx/plugins/<folder>/HatLoader/"
                HatLoader.DebugLog("Now looking for custom hat files:");
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
                    HatLoader.DebugLog($"Found metadata: {metaPath}");
                    var hats = ParseMetadata(metaPath, cachedBundles);
                    HatLoader.DebugLog($"Contains {hats.Count} Hats!");

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
                HatLoader.Log.LogError($"Exception during GameManager.Start hook: {e}");
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
            // the full directory path of the meta.xml
            var dirPath = Path.GetDirectoryName(metaPath);
            // get the mod folder name
            var split = dirPath.Split(Path.DirectorySeparatorChar);
            var modName = split[split.Length - 2];

            HatLoader.DebugLog($"Parsing metadata for {modName}");
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
                if (string.IsNullOrEmpty(hatMeta.name))
                {
                    HatLoader.Log.LogWarning($"Hat with prefab {hatMeta.hatPrefabName}/{hatMeta.debrisPrefabName} is missing a name and therefore can't be loaded!");
                    continue;
                }

                // Create a new hat with the properties we can copy directly over
                var uniqueName = $"{modName}_{hatMeta.name}";
                if (HatLoader.Name2ID.ContainsKey(uniqueName))
                {
                    HatLoader.Log.LogWarning($"Duplicate unique name {uniqueName}! Not loading the duplicate hat.");
                    continue;
                }

                var hat = new Hat()
                {
                    hatType = (HatType)HatLoader.GetNextHatID(uniqueName), // dynamic ID assignment
                    isTall = hatMeta.isTall,
                    hideHair = hatMeta.hideHair,
                    hatAttachment = hatMeta.hatAttachment,
                    dlcID = DownloadableContentID.None,
                    bannedCharacters = hatMeta.bannedCharacters,
                };
                HatLoader.DebugLog($"Parsing hat with id {(int)hat.hatType}");
                // Parse the prefab from the asset bundle
                var prefab = GetAssetFromBundle(dirPath, cachedBundles, hatMeta.hatPrefabBundle, hatMeta.hatPrefabName);
                if (prefab != null)
                    hat.hatPrefab = prefab as GameObject;
                // The same for the debris prefab
                var debris = GetAssetFromBundle(dirPath, cachedBundles, hatMeta.debrisPrefabBundle, hatMeta.debrisPrefabName);
                if (debris != null)
                    hat.hatDebrisPrefab = debris as Debris3D;

                HatLoader.DebugLog($"Loading new Hat ({uniqueName}): Type: {hat.hatType} ({(int)hat.hatType}), Prefab: {hat.hatPrefab}, DebrisPrefab: {hat.hatDebrisPrefab}, isTall: {hat.isTall}, hideHair: {hat.hideHair}, attachment: {hat.hatAttachment}, bannedChars: {string.Join("-", hat.bannedCharacters)}, dlc: {hat.dlcID}");
                //TODO: should we really allow hats that have neither prefabs nor debrisprefabs?
                // Add it to our list
                hats.Add(hat);
            }
            return hats;
        }
    }

    [Serializable]
    public class CustomCharacterSaveData
    {
        public CustomCharacterSaveData() { }
        public CustomCharacterSaveData(CharacterType type)
        {
            CharacterType = type;
        }

        public CharacterType CharacterType;
        public List<string> Hats = new List<string>();
    }

    // Saves our unlocked custom hats into a file and prevents them from being saved in the main library
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.SaveUnlockedItemsToDisk))]
    class DataManager_SaveUnlockedItemsToDisk_Patch
    {
        //https://github.com/exp111/OutwardStuff/blob/master/FailedRecipesFix/FailedRecipes.cs#L55
        //[HarmonyDebug]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                /*
        public void SaveUnlockedItemsToDisk(UnlockedItems unlockedItems, Action<bool> OnUnlockedItemsSaved)
	    {
		    if (unlockedItems == null)
		    {
			    Debug.Log("CRITICAL ERROR - Unlocked items data is empty, creating new instance");
			    unlockedItems = new UnlockedItems();
		    }
		    this.platformData.SaveJSON<UnlockedItems>("save.data", unlockedItems, OnUnlockedItemsSaved);
	    }
        =
        ldarg.0
        ldfld     class PlatformData DataManager::platformData
        ldstr     "save.data"
        ldarg.1
    =>
        ...
        trimmed = unlockedItems.Trim()
        this.platformData.SaveJSON<UnlockedItems>("save.data", trimmed, OnUnlockedItemsSaved);
    }
                */
                var cur = new CodeMatcher(instructions);

                // find this.platformData...
                var dataManager_platformData = AccessTools.Field(typeof(DataManager), nameof(DataManager.platformData));
                cur.MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_0), // this.
                    new CodeMatch(OpCodes.Ldfld, dataManager_platformData), //platformData.
                    new CodeMatch(OpCodes.Ldstr, "save.data"), //"save.data", 
                    new CodeMatch(OpCodes.Ldarg_1) // unlockedItems, 
                );

                // replace the unlockedItems with our own item
                // remote the unlockedItems load (this is actually redundant as we could keep this ldarg_1 but meh)
                cur.RemoveInstruction();
                // insert our code which trims the data
                cur.Insert(
                        new CodeInstruction(OpCodes.Ldarg_1), // put "unlockedItems" on the stack
                        Transpilers.EmitDelegate<Func<UnlockedItems, UnlockedItems>>((items) =>
                        {
                            try
                            {
                                // Create a trimmed version of the items (where our custom hats dont exist)
                                var trimmed = new UnlockedItems()
                                {
                                    unlockedCharacters = items.unlockedCharacters,
                                    matchesPlayedSinceLastCharacterUnlock = items.matchesPlayedSinceLastCharacterUnlock,
                                    totalMatchesPlayed = items.totalMatchesPlayed,
                                    totalProgress = items.totalProgress,
                                    hatTypesUnlockedSoFar = new List<int>()
                                };

                                // to save our custom hats, so we can load/save them seperately
                                var customHats = new List<CustomCharacterSaveData>();
                                foreach (CharacterType character in Enum.GetValues(typeof(CharacterType)))
                                {
                                    if (character == CharacterType.None) // doesnt have any hats
                                        continue;

                                    customHats.Add(new CustomCharacterSaveData(character));
                                }

                                // Filter out the custom hats
                                foreach (var hat in items.hatTypesUnlockedSoFar)
                                {
                                    if (Enum.IsDefined(typeof(HatType), hat))
                                        trimmed.hatTypesUnlockedSoFar.Add(hat);
                                }
                                foreach (var origChar in items.unlockedCharacterHats)
                                {
                                    var trimmedChar = trimmed.unlockedCharacterHats.Find(c => c.characterType == origChar.characterType);
                                    if (trimmedChar == null) // not found because char isnt unlocked by default
                                    {
                                        trimmedChar = new UnlockedCharacterHats(origChar.characterType);
                                        trimmed.unlockedCharacterHats.Add(trimmedChar);
                                    }
                                    var saveData = customHats.Find(c => c.CharacterType == origChar.characterType);
                                    foreach (var hat in origChar.unlockedHats)
                                    {
                                        //TODO: optimize Enum.IsDefined usage?
                                        if (Enum.IsDefined(typeof(HatType), hat)) // only hats which exist
                                        {
                                            if (hat == 0) // we already have that one (None) by default, so we would only create duplicates (FIXME: ?which the game cant handle)
                                                continue;

                                            trimmedChar.unlockedHats.Add(hat);
                                        }
                                        else // custom hat
                                        {
                                            saveData.Hats.Add(HatLoader.ID2Name[hat]); //transform id into name
                                        }

                                        trimmedChar.lastSelectedHat = origChar.lastSelectedHat;
                                    }
                                }

                                // trim empty characters beforehand
                                var count = customHats.RemoveAll(c => c.Hats.Count == 0);
                                HatLoader.DebugLog($"Removed {count} empty characters from custom save data");

                                // save the custom hats into a new file
                                var xml = new XmlSerializer(typeof(List<CustomCharacterSaveData>));
                                var path = Path.Combine(Application.persistentDataPath, HatLoader.CustomHatsSaveFile);
                                using (var file = File.Create(path))
                                    xml.Serialize(file, customHats);
                                //TODO: also save hatTypesUnlockedSoFar?

                                // let the game save the trimmed version
                                return trimmed;
                            }
                            catch (Exception ex)
                            {
                                HatLoader.Log.LogError($"Exception during DataManager.SaveUnlockedItemsToDisk transpiler: {ex}");
                                return items;
                            }
                        })
                    );

                var e = cur.InstructionEnumeration();
                return e;
            }
            catch (Exception e)
            {
                HatLoader.Log.LogError($"Exception during DataManager.SaveUnlockedItemsToDisk hook: {e}");
                return instructions;
            }
        }
    }

    // Loads our unlocked custom hats and injects them into the save
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.LoadUnlockedItemsFromDisk))]
    class DataManager_LoadUnlockedItemsFromDisk_Patch
    {
        //[HarmonyDebug]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                /*
        public void LoadUnlockedItemsFromDisk(Action<UnlockedItems> OnUnlockedItemsLoaded)
        {
            if (this.platformData.FileExists("save.data"))
            {
                this.platformData.LoadJSON<UnlockedItems>("save.data", OnUnlockedItemsLoaded);

                return;
            }
            ...
        }

        ldarg.0
        class PlatformData DataManager::platformData
        ldstr     "save.data"
        ldarg.1
        callvirt instance void PlatformData::LoadJSON<class UnlockedItems>(string, class [netstandard] System.Action`1<!!0>)

        =>
        if (this.platformData.FileExists("save.data"))
        {
            this.platformData.LoadJSON<UnlockedItems>("save.data", (unlocked) => 
                    {
                        unlocked.Add(LoadCustomHats());
                        OnUnlockedItemsLoaded(unlocked)
                    });

            return;
        }
                     */
                var cur = new CodeMatcher(instructions);

                // find this.platformData...
                var dataManager_platformData = AccessTools.Field(typeof(DataManager), nameof(DataManager.platformData));
                cur.MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_0), // this.
                    new CodeMatch(OpCodes.Ldfld, dataManager_platformData), //platformData.
                    new CodeMatch(OpCodes.Ldstr, "save.data"), //"save.data", 
                    new CodeMatch(OpCodes.Ldarg_1) // unlockedItems, 
                );

                // Don't push ldarg_1 
                cur.RemoveInstruction();

                cur.Insert(
                        new CodeInstruction(OpCodes.Ldarg_1), // put "unlockedItems" on the stack
                        Transpilers.EmitDelegate<Func<Action<UnlockedItems>, Action<UnlockedItems>>>((original) =>
                        {
                            try
                            {
                                Action<UnlockedItems> replace = (items) =>
                                {
                                    try
                                    {
                                        var path = Path.Combine(Application.persistentDataPath, HatLoader.CustomHatsSaveFile);
                                        if (!File.Exists(path)) // save data doesnt exist
                                        {
                                            original(items);
                                            return;
                                        }

                                        // add custom hats to the items
                                        // read from hatLoader.xml
                                        var xml = new XmlSerializer(typeof(List<CustomCharacterSaveData>));
                                        List<CustomCharacterSaveData> customHats;
                                        using (var file = File.OpenRead(path))
                                            customHats = (List<CustomCharacterSaveData>)xml.Deserialize(file);
                                        
                                        // now add the custom hats into the list
                                        foreach (var character in customHats)
                                        {
                                            var origChar = items.unlockedCharacterHats.Find(c => c.characterType == character.CharacterType);
                                            if (origChar == null)
                                            {
                                                // no need to do anything probably. either a predefined character that isnt available or we lost a character in the main save (which we probably shouldnt recover)
                                                HatLoader.DebugLog($"Missing character {character.CharacterType} in unlockedItems while parsing custom hats");
                                                continue;
                                            }

                                            foreach (var hat in character.Hats)
                                            {
                                                // try to get the dynamic id from the unique name
                                                if (!HatLoader.Name2ID.TryGetValue(hat, out var id))
                                                {
                                                    HatLoader.Log.LogWarning($"Tried to add non existing hat {hat} from save");
                                                    continue;
                                                }

                                                origChar.unlockedHats.Add(id); 
                                                HatLoader.DebugLog($"Added hat {hat} ({HatLoader.Name2ID[hat]}) to character {character.CharacterType}");
                                            }
                                        }
                                        //TODO: hatTypesUnlockedSoFar?
                                    } 
                                    catch (Exception ex)
                                    {
                                        HatLoader.Log.LogError($"Exception during DataManager.LoadUnlockedItemsFromDisk OnUnlockedItemsLoaded replacement: {ex}");
                                    }
                                    // return the list with (hopefully) our added hats
                                    original(items);
                                };
                                return replace;
                            }
                            catch (Exception ex)
                            {
                                HatLoader.Log.LogError($"Exception during DataManager.LoadUnlockedItemsFromDisk transpiler: {ex}");
                                return original;
                            }
                        })
                    );

                var e = cur.InstructionEnumeration();
                return e;
            }
            catch (Exception e)
            {
                HatLoader.Log.LogError($"Exception during DataManager.LoadUnlockedItemsFromDisk hook: {e}");
                return instructions;
            }
        }
    }
}
