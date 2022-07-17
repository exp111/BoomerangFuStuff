# Game
Unity 2019.2.21 (at least that is what asset ripper says to the resource files)

## Logos
`Startup.ShowLogos()`
TODO: skip logos

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

### Creating a costume
Create a new scene
Place a character from a prefab and hide the other layers (like death models and such)
Place a empty object (the parent) on the head joint (for the banana the "tip_joint")
Add your model or whatever you want on the head under the parent
Remove any colliders
Disable "Cast Shadows"
Move the parent into the assets to make it a prefab
Export the prefab into a asset bundle or whatever

#### Asset Bundles
Either use ThunderKit (https://github.com/PassivePicasso/ThunderKit), which didnt work on unity 2019.2.21, or use this unity tutorial (https://docs.unity3d.com/Manual/AssetBundles-Workflow.html), which takes way longer but at least it works.


## Ideas
More levels
More costumes
More characters
Level selection/blacklisting
speed up option when all players are dead (like in shipped)