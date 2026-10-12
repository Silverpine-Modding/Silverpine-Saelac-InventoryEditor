# Inventory Editor 1.2.0

Edit your current character's inventory through a charcoal-black Modding Tools
interface. This first public release includes the inventory, condition and
food/water editing features developed through version 1.2.0.

## Features

- Open **Inventory > Mods > Inventory Editor**, or press the default Modding
  Tools shortcut **M** and choose **Inventory Editor**.
- Browse and search the complete loaded item library, including custom-pack
  templates. Inspect your current inventory and add, duplicate or remove
  individual items. Undo the most recent removal while the editor stays open.
- Customize names and descriptions using the game's item-generation character
  rules and 30-word description limit. Each copy has independent item data.
- Edit durability, remaining spoilage time and native enchantments. Equipped
  enchantment changes refresh the associated equipment bonuses.
- Edit food and water restoration on edible items and drinks. Existing sanity,
  well-fed and other unedited effects remain intact.
- Set player gold and base carrying capacity while accounting for native
  equipment/spell bonuses and supported Custom Item Loader capacity modifiers.
- Native capacity checks, input validation and rollback protect against partial
  inventory or item edits when an operation fails.

## Requirements and installation

Requires Silverpine 1.7.3, BepInEx 5 and
[Modding Tools 1.10.0 or newer](https://github.com/Silverpine-Modding/Silverpine-Saelac-Modding-Tools/releases).
Custom Item Loader is optional for its loaded item templates and capacity modifiers.

Close the game and extract `InventoryEditor-1.2.0.zip` into `BepInEx/plugins/`.
The ZIP contains the `InventoryEditor` folder with the DLL and README.
Alternatively, place the standalone `InventoryEditor.dll` into
`BepInEx/plugins/InventoryEditor/`. Keep one copy and restart the game.

Modding Tools is a separate dependency. Game assemblies, local configuration
and player data are not included.

## Saves and compatibility

Apply your edits and save normally in Silverpine. The editor uses native item
fields/components and inventory save data; it adds no editor-specific saved
components or separate save files. Edits to vanilla items, inventory contents,
gold and capacity should persist after removing Inventory Editor.

Items from other mods may still require their original assets or behavior
plugins. Editing or duplicating those items does not remove their dependencies.
Durability and spoilage continue to follow normal gameplay.

## Validation

- Release build completed with zero warnings and errors.
- All 146 automated behavior and installed-assembly contract checks passed.
- Checks cover independent item copies, native text rules, gold and capacity,
  food/water values, condition edits, equipment bonuses and failure rollback.
- Native save/load fields were inspected. A live save, uninstall and reload
  test has not been performed.
- The earlier 1.0.0 interface was checked in-game for rendering, filtering,
  menu navigation and resizing. The new 1.1.0 and 1.2.0 tabs have not been
  independently verified in a live Unity session.

## SHA-256

`InventoryEditor.dll`: `4CB3BD28282842682A41C46CDAF592A74FF6676EBF7C4C56C51121C9EE1575CA`

`InventoryEditor-1.2.0.zip`: `ECE10EAAAF7AC35CDB2970FC200BD5FB960CC14AFD34358FDA31DE2ECC35EA85`

Created by **Saelac and ChatGPT**.
