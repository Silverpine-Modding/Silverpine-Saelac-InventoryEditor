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
        => AddCore(player, inventory, source, quantity, fromInventory, custom, name, description, edits, MaxBatch);

    internal static void AddToStack(Player player, Inventory inventory, Item source, int quantity)
        => AddCore(player, inventory, source, quantity, true, false, "", "", null, InventoryStacks.MaxQuantity, stackCopies: true);

    private static List<Item> AddCore(Player player, Inventory inventory, Item source, int quantity,
        bool fromInventory, bool custom, string name, string description, ItemEditOptions? edits, int limit, bool stackCopies = false)
    {
        RequireCurrent(player, inventory);
        if (fromInventory) RequireItem(inventory, source);
        else if (!ItemLibrary.Items.Contains(source))
            throw new InvalidOperationException("That template is no longer loaded. Refresh the library.");
        if (quantity < 1 || quantity > limit)
            throw new InvalidDataException($"Choose between 1 and {limit} items per action.");
        if (custom) GeneratedItemText.Validate(name.Trim(), description);
        if (edits != null) ItemEditing.Validate(source, edits);
        string? stackKey = stackCopies ? InventoryStacks.Key(source) : null;

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
            if (stackKey != null && InventoryStacks.Key(copy) != stackKey)
                throw new InvalidOperationException("Copies of this item do not retain matching stack data. No items were added.");
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
            if (stackKey != null && prepared.Any(i => !inventory.ContainsItem(i) || InventoryStacks.Key(i) != stackKey))
                throw new InvalidOperationException("An item hook changed the added stack's contents.");
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
        => RemoveMany(player, inventory, new List<Item> { item });

    internal static RemovedItem RemoveMany(Player player, Inventory inventory, List<Item> items)
    {
        RequireCurrent(player, inventory);
        foreach (Item item in items) RequireItem(inventory, item);
        var receipt = new RemovedItem(player, inventory, items);
        try
        {
            foreach (Item item in items)
            {
                inventory.RemoveItem(item);
                if (inventory.ContainsItem(item)) throw new InvalidOperationException("An item hook prevented removal.");
            }
        }
        catch (Exception failure)
        {
            // Native callbacks can throw after removal; attempt to restore this exact object.
            try { receipt.Restore(); }
            catch (Exception restore) { throw new InvalidOperationException(failure.Message + " Restore failed: " + restore.Message, failure); }
            throw new InvalidOperationException("Remove failed; the item was restored. " + failure.Message, failure);
        }
        return receipt;
    }

    internal sealed class RemovedItem
    {
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly HashSet<Item> equipped;
        internal readonly List<Item> Items;
        internal Item Item => Items[0];
        internal RemovedItem(Player player, Inventory inventory, List<Item> items)
        {
            this.player = player; this.inventory = inventory; Items = new List<Item>(items);
            equipped = new HashSet<Item>(items.Where(player.IsItemEquipped));
        }

        internal void Restore()
        {
            RequireCurrent(player, inventory);
            List<Item> missing = Items.Where(i => !inventory.ContainsItem(i)).ToList();
            var capacity = new Inventory { maxItems = inventory.maxItems, maxBulk = inventory.maxBulk,
                allowEncumbered = inventory.allowEncumbered };
            capacity.items.AddRange(inventory.GetAllItemsShallowListCopy());
            foreach (Item item in missing)
            {
                if (!capacity.CanAddItem(item))
                    throw new InvalidOperationException("Free enough inventory capacity to restore the whole removal.");
                capacity.items.Add(item);
            }
            var previouslyEquipped = player.equippedItems.Values.Where(i => i != null).Distinct().ToList();
            int originalGold = inventory.gold;
            try
            {
                foreach (Item item in missing)
                    if (!inventory.TryAddItem(item)) throw new InvalidOperationException("The inventory rejected a restored item.");
                foreach (Item item in equipped)
                    if (!player.IsItemEquipped(item)) player.EquipItem(item);
            }
            catch (Exception failure)
            {
                try
                {
                    foreach (Item item in missing.Where(inventory.ContainsItem)) inventory.RemoveItem(item);
                    foreach (Item item in equipped.Where(player.IsItemEquipped).Where(i => !previouslyEquipped.Contains(i))) player.UnequipItem(item);
                    foreach (Item item in previouslyEquipped)
                        if (!player.IsItemEquipped(item)) player.EquipItem(item);
                    inventory.gold = originalGold;
                    inventory.UpdateEncumbered();
                }
                catch (Exception rollback) { throw new InvalidOperationException(failure.Message + " Undo rollback also failed: " + rollback.Message, failure); }
                throw new InvalidOperationException("Could not restore the removal; undo was rolled back. " + failure.Message, failure);
            }
        }
    }
}
