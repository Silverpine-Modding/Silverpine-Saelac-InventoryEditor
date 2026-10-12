using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SilverpineMods.InventoryEditor;

internal static class InventoryService
{
    // A per-click editor limit, not a restriction imposed by Silverpine.
    internal const int MaxBatch = 100;

    internal static void RequireCurrent(Player player, Inventory inventory)
    {
        if (player == null || player != Player.Instance || player.worldInventory == null ||
            !ReferenceEquals(player.worldInventory.inventory, inventory))
            throw new InvalidOperationException("This character's inventory is no longer available. Reopen the editor.");
    }

    internal static void RequireItem(Inventory inventory, Item item)
    {
        if (!inventory.ContainsItem(item))
            throw new InvalidOperationException("That item is no longer in your inventory. Select another item.");
    }

    internal static List<Item> Add(Player player, Inventory inventory, Item source, int quantity,
        bool fromInventory, bool custom, string name, string description, ItemEditOptions? edits = null)
    {
        RequireCurrent(player, inventory);
        if (fromInventory) RequireItem(inventory, source);
        else if (!ItemLibrary.Items.Contains(source))
            throw new InvalidOperationException("That template is no longer loaded. Refresh the library.");
        if (quantity < 1 || quantity > MaxBatch)
            throw new InvalidDataException($"Choose between 1 and {MaxBatch} items per action.");
        if (custom) GeneratedItemText.Validate(name.Trim(), description);
        if (edits != null) ItemEditing.Validate(source, edits);

        var prepared = new List<Item>();
        var capacity = new Inventory { maxItems = inventory.maxItems, maxBulk = inventory.maxBulk,
            allowEncumbered = inventory.allowEncumbered };
        capacity.items.AddRange(inventory.GetAllItemsShallowListCopy());
        for (int i = 0; i < quantity; i++)
        {
            Item copy = source.DeepClone();
            // Each copy has independent serialized components; no global template is modified.
            copy.RemoveItemComponentsOfType<ItemComponent_VendorItem>();
            if (custom) { copy.name = name.Trim(); copy.description = description; }
            if (edits != null) ItemEditing.Apply(copy, edits);
            if (float.IsNaN(copy.bulk) || float.IsInfinity(copy.bulk) || copy.bulk < 0)
                throw new InvalidDataException("The selected item has invalid bulk.");
            if (!capacity.CanAddItem(copy))
                throw new InvalidOperationException("There is not enough inventory capacity for the entire quantity. No items were added.");
            prepared.Add(copy);
            // Native gold pickup turns the item into currency and removes it immediately.
            if (copy.GetItemComponent<ItemComponent_Gold>() == null) capacity.items.Add(copy);
        }

        int originalGold = inventory.gold;
        try
        {
            foreach (Item copy in prepared)
                if (!inventory.TryAddItem(copy))
                    throw new InvalidOperationException("The inventory rejected an item during the operation.");
            return prepared;
        }
        catch (Exception failure)
        {
            var rollbackErrors = new List<string>();
            foreach (Item copy in prepared.Where(inventory.ContainsItem))
            {
                try { inventory.RemoveItem(copy); }
                catch (Exception ex) { rollbackErrors.Add(ex.Message); }
            }
            inventory.gold = originalGold;
            try { inventory.UpdateEncumbered(); }
            catch (Exception ex) { rollbackErrors.Add(ex.Message); }
            throw new InvalidOperationException("Could not add items: " + failure.Message +
                (rollbackErrors.Count == 0 ? " Added copies were rolled back." :
                    " Rollback encountered an error: " + string.Join("; ", rollbackErrors)), failure);
        }
    }

    internal static void Customize(Player player, Inventory inventory, Item item, string name, string description)
    {
        RequireCurrent(player, inventory);
        RequireItem(inventory, item);
        GeneratedItemText.Validate(name.Trim(), description);
        string oldName = item.name, oldDescription = item.description;
        try
        {
            item.name = name.Trim();
            item.description = description;
            // Refresh native inventory observers without removing or re-equipping this object.
            inventory.UpdateEncumbered();
        }
        catch
        {
            item.name = oldName;
            item.description = oldDescription;
            inventory.UpdateEncumbered();
            throw;
        }
    }

    internal static RemovedItem Remove(Player player, Inventory inventory, Item item)
    {
        RequireCurrent(player, inventory);
        RequireItem(inventory, item);
        var receipt = new RemovedItem(player, inventory, item, player.IsItemEquipped(item));
        try { inventory.RemoveItem(item); }
        catch (Exception failure)
        {
            // Native callbacks can throw after removal; attempt to restore this exact object.
            try { receipt.Restore(); }
            catch (Exception restore) { throw new InvalidOperationException(failure.Message + " Restore failed: " + restore.Message, failure); }
            throw new InvalidOperationException("Remove failed; the item was restored. " + failure.Message, failure);
        }
        if (inventory.ContainsItem(item)) throw new InvalidOperationException("An item hook prevented removal.");
        return receipt;
    }

    internal sealed class RemovedItem
    {
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly bool equipped;
        internal Item Item { get; }
        internal RemovedItem(Player player, Inventory inventory, Item item, bool equipped)
        { this.player = player; this.inventory = inventory; Item = item; this.equipped = equipped; }

        internal void Restore()
        {
            RequireCurrent(player, inventory);
            if (!inventory.ContainsItem(Item) && !inventory.TryAddItem(Item))
                throw new InvalidOperationException("Free some inventory capacity before undoing the removal.");
            if (equipped && !player.IsItemEquipped(Item)) player.EquipItem(Item);
        }
    }
}
