# Boomerang Fu Stuff
Some mods I wrote. Probably all rely on BepInEx.

The game itself was built with Unity 2019.2.21 (or at least the assets), so you probably need that if you want to build custom assets.

If something isn't written here, you could look into [my notes](Notes/Boomerang%20Fu.md).

## Hat Loader
Allows to add custom hats into the game. To add your own hat you need basically need two things: 
* A metadata file, which contains informations about your hat(s)
* One or multiple asset bundles, from which the hats are loaded (actually optional, but you probably want your hat to have a model)

### More detailled tutorial on how to add a hat
* Create a new folder in `BepInEx\plugins`
* Inside that folder create a `HatLoader` folder. This mod looks for this folder.
* Inside the `HatLoader` folder create a `meta.xml` file.
* Fill in the `meta.xml` file (more info below)
* Create a hat prefab (or multiple) inside Unity
* Export that prefab into an asset bundle
* Add your asset bundles right next to the `meta.xml` (or at least know the relative path)
* You're done!

### The `meta.xml` file
This file contains information about the hats you wanna add.
An example file:
```xml
<?xml version="1.0" encoding="utf-16"?>
<Metadata>
  <hats>
    <HatMetadata>
      <id>38</id>
      <hatPrefabBundle>hats</hatPrefabBundle>
      <hatPrefabName>HatSquare</hatPrefabName>
      <debrisPrefabBundle>hats</debrisPrefabBundle>
      <debrisPrefabName>DebrisHatSquare</debrisPrefabName>
      <isTall>true</isTall>
      <hideHair>false</hideHair>
      <hatAttachment>HatJoint</hatAttachment>
      <bannedCharacters>
        <CharacterType>Banana</CharacterType>
        <CharacterType>Eggplant</CharacterType>
      </bannedCharacters>
    </HatMetadata>
    <HatMetadata>
      <id>39</id>
      <hatPrefabBundle>hats</hatPrefabBundle>
      <hatPrefabName>HatCowboy</hatPrefabName>
      <isTall>false</isTall>
      <hideHair>true</hideHair>
      <hatAttachment>HatJoint</hatAttachment>
      <bannedCharacters>
      </bannedCharacters>
    </HatMetadata>
  </hats>
</Metadata>
```
This file contains definitions for two hats (a square hat and a cowboy hat).  
The fields:  
`id`: The hat ID. This should be unique (or things will break). Will probably deprecate this soon (hopefully :tm:)  
`hatPrefabBundle`: The relative path to the bundle that contains the hat prefab. (Optional)  
`hatPrefabName`: The name of the prefab inside the bundle. (Optional)  
`debrisPrefabBundle`: The relative path to the bundle that contains the debris prefab. (Optional)  
`debrisPrefabName`: The name of the debris prefab inside the bundle. (Optional)  
`hatAttachment`: Where the hat is attached (currently either the top of the head or the face)  
`bannedCharacters`: The characters which aren't allowed to wear this hat