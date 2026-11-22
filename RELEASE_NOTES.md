# Inventory Editor 1.3.0

Matching items now appear as stacks, with editable total counts. Eligible items
also gain a native rarity / quality editor.

## Changes

- Current inventory rows group items using Silverpine's serialized-data stacking
  rule and show an **×count** badge. Distinct condition, text, effects or quality
  keep items in separate rows. Equipped stacks appear first.
- Select a stack and use **Details > Stack count > Set stack count** to set its
  total to 0–10,000. Zero removes the stack. Native slot and bulk limits still apply.
- Growth creates independent copies using current saved item values. Reduction
  keeps equipped items where possible; **Undo last removal** restores the whole
  reduction and its original objects after a complete capacity check.
- **Copies to add** remains separate from the total stack count. Property edits
  apply to one inspected item; changed items split from their stack.
- The **Quality** tab offers the seven native tiers from **Normal** through
  **Legendary**, plus **Unchanged**. It is available only for items supporting
  the game's quality system.
- Equipped quality edits refresh armor and other equipment bonuses. Potions
  use the native tier-to-potency conversion; rollback preserves exact prior potency.
- Added validation and rollback checks for stack operations, quality changes,
  stale selections, unsupported items and failing third-party hooks.

## Requirements and installation

Requires Silverpine 1.7.3, BepInEx 5 and
[Modding Tools 1.10.0 or newer](https://github.com/Silverpine-Modding/Silverpine-Saelac-Modding-Tools/releases).
Custom Item Loader is optional for its item templates and capacity modifiers.

Close the game and extract `InventoryEditor-1.3.0.zip` into `BepInEx/plugins/`.
The ZIP contains the `InventoryEditor` folder with the DLL and README.
Keep one installed copy and restart Silverpine. Open
**Inventory > Mods > Inventory Editor** or use the default Modding Tools shortcut **M**.

Modding Tools is a separate dependency. Game assemblies, local configuration
and player data are not included.

## Save compatibility

Changes use native item components and inventory data. Save normally to retain
them. Vanilla item customizations should survive removing Inventory Editor;
items from other mods can still depend on their original assets or behavior.
There is no editor-specific stack or rarity data in saves.

## Validation

- Release build succeeded with zero warnings and errors.
- All **216** automated behavior and installed-assembly contract checks passed.
- Built, installed and packaged DLL hashes match.
- The new controls have not been visually verified in a live Unity session.
- No live character data or saves were changed during validation. A live
  save, uninstall and reload test has not been performed.

## SHA-256

`InventoryEditor.dll`: `92D6F0A968936E86D9F235E5E01AF128004A2456EACB59361FA37F945D59795F`

`InventoryEditor-1.3.0.zip`: `E33CC11BE256169B15E411CDBEF6E3E64A3CD4D6C33F0DAA61F75F4980C47AC2`

Created by **Saelac and ChatGPT**.
