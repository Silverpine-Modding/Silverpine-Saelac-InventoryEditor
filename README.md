# Inventory Editor

A live, in-game debug inventory editor for **Silverpine 1.7.3**, created by **Saelac and ChatGPT**.

Version: **1.2.0**. Requires BepInEx 5 and [Modding Tools **1.10.0 or newer**](https://github.com/Silverpine-Modding/Silverpine-Saelac-Modding-Tools/releases).

[Download the latest release](https://github.com/Silverpine-Modding/Silverpine-Saelac-InventoryEditor/releases/latest).

## Open

Load your character, open **Inventory > Mods > Inventory Editor**, or press **M** with the default Modding Tools shortcut and choose **Inventory Editor**. Close or Escape returns to the framework's Mods menu.

The charcoal-black interface uses three panels: item library, current inventory, and item details. The details panel has **Details**, **Custom text**, **Condition**, and **Food / water** tabs. Edits take effect when you click **Apply changes to item**, **Add**, or **Duplicate**. Gold, capacity and removal actions take effect when clicked. Save normally in Silverpine to keep changes; the editor does not force a save.

## Items

- Search the complete loaded `ItemLibrary` by name, description or category. The scrollable browser includes loaded custom packs, same-name template variants, and internal `HIDDEN_` templates. It has no result cap; only visible rows are drawn.
- Click **+** to add one library item. Select a template to inspect it and add 1–100 copies per action.
- Every current inventory row represents one actual item. Equipped items appear first. Select a row to duplicate its serialized state, including durability, quality and enchantment.
- **Remove item** removes only the selected object and uses native unequip behavior. **Undo last removal** restores the exact item and its equipped state when capacity permits. This single removal receipt lasts until another successful removal or closing the window.
- In **Custom text**, enable **Use custom name & description** to add personalized copies, or use **Apply changes to item** to edit the selected existing item. Templates and other item instances are not changed. Appearance, stats and components are inherited except for your explicit condition, enchantment, food and water edits.
- Names use the native generation grammar: letters A–Z/a–z, digits, spaces, and `. , ! ? ' -`. The native name grammar has no length cap.
- Descriptions use letters, spaces, and that punctuation, without digits, up to 30 words. These are the `NPCFunction_GiveItem` generation grammar and prompt rules; the game serializer itself does not impose a word cap. Input is filtered and excess description words are trimmed.
- The editor honors native slot capacity and `allowEncumbered`. A batch is checked in full before any copies are added. Gold items convert to currency through the native pickup behavior. New copies discard vendor-stock ownership.

## Durability, spoilage and enchantments

- Select an item and open **Condition**. Controls are enabled only for supported components. Pending edits apply to the selected item with **Apply changes to item**, or to independent copies with **Add/Duplicate**. Selecting another item discards the previous draft.
- **Durability** accepts values from zero to the item's existing maximum, including decimals. **Repair** fills the draft with the maximum; **Reset** discards that edit. These debug assignments set the serialized durability directly, so Unbreaking cannot randomly reject the change. Setting zero does not invoke break callbacks or destroy the item.
- **Remaining spoilage time** is the remaining base timer in game minutes, in 0.5-minute steps. **1 day** sets 1,440 minutes. The native timer uses two turns per minute and ticks at half speed during winter. Valid editor values are 0.5–10,000,000 minutes; zero is rejected because the native timer stops ticking at zero instead of firing expiration. Normal expiration continues through the native game loop.
- **Choose enchantment** offers **Unchanged**, **None**, and every native enchantment. Equipment eligibility follows `ItemComponent_Enchantment.IsEnchantable`; non-equipment and scroll payloads are left intact. Applying a replacement removes the previous enchantment(s), uses a fresh component, and unequips/re-equips worn gear so old bonuses are removed and new bonuses applied.
- Unedited values remain untouched, including timers that changed while the editor was open. Invalid input is rejected before mutation. A failed combined edit attempts to restore text, condition, enchantments and equipped state.
- Durability and spoilage edits use the native serialized component fields, so they persist in ordinary game saves without adding editor-specific components.

## Food and water values

- Select a food or drink and open **Food / water**. **Food value** controls hunger restored; **Water value** controls thirst restored. These controls appear for any item with the native edible component, including drinks and custom meals, regardless of category or whether it has a spoilage timer.
- Enter whole numbers. Positive values restore the resource, zero has no effect, and negative values increase hunger or thirst. The editor allows -100,000 to 100,000, matching Custom Item Loader's food-value editor range; this is an editor guardrail rather than a native cap.
- Click **Apply changes to item** to update only the selected object, or **Add/Duplicate** to create copies with those values. **Reset** restores each field's original draft value. The library's quick **+** continues to add an unedited template.
- Hunger and thirst values are edited on the existing edible component. Sanity effects, the well-fed effect, spoilage and other properties remain unchanged unless separately edited. Values use the native consumption and save fields, so they persist in normal saves.

## How generated item appearances work

Silverpine's NPC food-generation path chooses an existing food/beverage sprite independently, generates food effects from meal size and complexity, and asks separately for shelf life. Ordinary NPC item gifts choose a matching template that supplies both behavior and appearance, with possible quality/enchantment/text changes. The separate **Manual Custom Item Generator** plugin also allows independent stat-base and inventory-model selection, without LLM calls. Inventory Editor preserves the selected item's existing appearance; it does not yet include a separate appearance picker.

## Gold and carrying capacity

- Enter a whole number beside **Gold** and click **Set gold**. This sets an absolute amount, including zero.
- Enter a whole number beside **Base carrying capacity** and click **Set base limit**. Current bulk, effective limit, base limit and equipment/spell bonuses are displayed separately.
- Capacity editing preserves the base game's Effortlessness bonus (+10 per equipped enchantment), Lift Burden (+1000), and the installed Custom Item Loader's `carryCapacity` modifiers. Unequipping those items or expiring the spell leaves the edited base amount intact.
- Editor input ranges are 0–1,000,000,000 gold and 1–1,000,000 base bulk. These are editor guardrails, not native text or item limitations. Negative/overflowing totals are rejected.
- Silverpine serializes `gold`, `maxBulk` and each item's text in the character's normal inventory data, so no editor-specific save component is required.
- Third-party capacity effects other than the supported Custom Item Loader modifier have no common framework reporting API. Unknown bonuses may be included in the displayed base amount. Capacity editing is disabled if the known Custom Item Loader registry cannot be read.

## Save compatibility

The editor changes native item fields and components and uses the game's inventory and equipment operations. It adds no editor-specific saved components or separate save files. Save normally after editing; changes to vanilla items, inventory contents, gold and carrying capacity are stored in the character's ordinary game save and should remain when Inventory Editor is removed.

Items supplied by other mods may still require their original assets, components or behavior plugins. Editing or duplicating an item does not remove those dependencies. Durability and spoilage continue to change through normal gameplay. Save/load code and installed-assembly contracts were checked; a live save, uninstall and reload test has not been performed.

## Installation

Close Silverpine and extract `InventoryEditor-1.2.0.zip` into `BepInEx/plugins/`. The ZIP contains an `InventoryEditor` folder with the DLL and README. Alternatively, place the standalone `InventoryEditor.dll` under `BepInEx/plugins/InventoryEditor/`.

Keep one copy of Inventory Editor and exactly one existing copy of `ModdingTools.dll`; this plugin does not bundle or replace the framework. Restart Silverpine after installation. Custom Item Loader is optional for loaded custom templates and its carrying-capacity modifiers.

## Build and validation

Requires a .NET SDK supporting .NET Standard 2.1; the validation harness targets .NET 9. Game and framework assemblies are read from your own installation and are not included in the repository or downloads.

Build from the game folder:

```powershell
dotnet build 'Plugin Development\InventoryEditor\InventoryEditor.csproj' -c Release
dotnet run --project 'Plugin Development\InventoryEditor\Tests\InventoryEditor.Tests.csproj' -c Release -- .
```

For a standalone checkout, run from the repository root:

```powershell
dotnet build InventoryEditor.csproj -c Release -p:SilverpineGameDir="C:\path\to\Silverpine"
dotnet run --project Tests/InventoryEditor.Tests.csproj -c Release -p:SilverpineGameDir="C:\path\to\Silverpine" -- "C:\path\to\Silverpine"
```

The validation harness additionally inspects the installed Custom Item Loader assembly for the optional carrying-capacity integration. Install that dependency to run the full harness. Production builds use only local game/framework references.

The validation harness tests production inventory/text/stat services with Unity runtime doubles, then checks the installed game and plugin assemblies for menu integration, save-field compatibility and custom-text rules. It does not substitute for an in-game rendering check.

Modding Tools owns the window session, scaling and input lock. Static registration survives Silverpine's bootstrap-host destruction; temporary windows release their events and generated theme textures when closed. No Harmony patches or framework modifications are required.
