# Game
Unity 2019.2.21 (at least that is what asset ripper says to the resource files)

## Logos
`Startup.ShowLogos()`
=> okay this code works, but it makes no difference (logo 0 isn't shown)
```c#
[HarmonyPatch(typeof(Startup), nameof(Startup.ShowLogos))]
class Startup_ShowLogos_Patch
{
    [HarmonyPrefix]
    public static void Prefix(Startup __instance)
    {
        __instance.logos = new UnityEngine.RectTransform[1] { null };
    }
}
```

## Levels
Saved in scenes + meta info in assets (`LevelAsset`)
Controlled by LevelManager

Tutorial: `LevelTutorial.unity` (`Assets\Scene\Scenes\`) + `LevelTutorial.asset` (`Assets\MonoBehaviour\`)


Levels saved in `LevelManager.levelAssets` => serialized in the `GameScene` scene => TODO: add our levels dynamically
switch blacklisted levels in `LevelManager.switchIncompatibleLevels`

`LevelManager.BuildMatchPlaylist` selects the appropriate levels (according to platform, player count, gamemode, tutorial needed) and puts them into a playlist

`Startup.LoadGameScene()`: loads "GameScene" scene => TODO: add our levels after here?

### Level Info
Scene contains parent `Level` object. That has the `Level` component on it, which contains info about => FIXME: needs `Level` class definition

* assetripper has default decompilation + stubbed decomp, but both wont work (editing the stubbed version maybe would work but idk if that breaks shit)
* i dont think you can import the game assemblies directly

## Costumes
Selection: `UILobbyPlayer.Update`:  => `GameManager.GetNextUnlockedHatForCharacter`
=> returns `HatType` enum => saved into `Character.equippedHat` => equipped with `GameManager.GetHat` => loops through `GameManager.hatLibrary` => TODO: where tf does this come from?

`Hat` class contains metadata like `HatType` (the id, enum), prefab, debris prefab (if one exists), characters that cant wear the hat etc, attachment place

=> After GameManager init'ed, change the `hatLibrary` to include our hats (by copying merging the old array and the new items into a new one)

### Costume Unlock Info
* First tries to unlock characters
* Then unlock random hat (`GameManager.UnlockHat`):
* Check for human + winning players:
* The lower your hat count for that char, the higher the chance to be selected into a list
* 50/50 chance to sort that list by least amount of hats
* Then add non human players to the end of the list
* I dont wanna parse this shitcode anymore

### Creating a costume
* Create a new scene
* Place a character from a prefab and hide the other layers (like death models and such)
* Place a empty object (the parent) on the head joint (for the banana the "tip_joint")
* Add your model or whatever you want on the head under the parent
* Remove any colliders
* Disable "Cast Shadows"
* Move the parent into the assets to make it a prefab
* Export the prefab into a asset bundle or whatever

#### Asset Bundles
Either use ThunderKit (https://github.com/PassivePicasso/ThunderKit), ~~which didnt work on unity 2019.2.21~~ fixed in 5.4.0, or use this unity tutorial (https://docs.unity3d.com/Manual/AssetBundles-Workflow.html), which takes way longer (because it compresses the bundle) but at least it works.

You should probably set some compression options if you use ThunderKit cause it makes a difference.

## Powerups
`Player.OnPickupColliderStay()` => checks for "Powerup" tag =>
`Player.CollectPowerup()` => gets powerup =>
* `Powerup.GetCollected` => if `power` not set (PowerupType.None):
* `LevelManager.SelectRandomPower`: reads from `LevelManager.possiblePowerups` (Created in `LevelManager.BuildPossiblePowerups`), filters duplicates/non compatibles
`Player.StartPowerup`: does the direct powerup effects (events, shield, reverse timer), checks for incompatibilities, adds powerup to `Player.activePower` (bit &) + Player.powerupHistory, also checks `Player.maxActivePowerups`
TODO: Prefix/Postfix this

`LevelManager.BuildPossiblePowerups`: adds from `LevelManager.powerupProbabilities` if powerup is allowed (in `SettingsManager.MatchSettings.availablePowerups` (bitwise &)), also filters if gamemode is goldendisk
TODO: hook `Powerup.AllPowerups()` to also include our custom powerups and also find out how to add our shit to UI
TODO: Postfix `LevelManager.LevelManager()` or `LevelManager.Start` and include our powerups in `powerupProbabilities`

### Custom Powerups Idea
* Create custom Powerup class (given by our Loader): name, icon, override events, CollidesWith (should we do this or rather let them do it in OnCollect?), countsToHistory (should overwrite other powerups?) in your `Plugin.Awake` (and add it to list by calling smth)
* events: OnAttack(?), OnThrow(?), OnEndThrow(? like explosive/multi), OnPlayerSpawn (?), OnCollect
* you do additional events yourself
* We call Events in perspective funcs

## Ideas
* More levels
* More costumes
* More characters
* Level selection/blacklisting
* Speed up option when all players are dead (like in shipped)
* More teams/team colors (+selection)
* Make it possible to play non team colored chars so your pineapple doesn't look like a tictac
* More powerups/events
* Powerup chance adjustment? (currently all are equal)
* Better hat selection
* Spawn point randomizer?
### Hats
* Pyramid
