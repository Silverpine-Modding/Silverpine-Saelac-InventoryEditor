using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SilverpineMods.InventoryEditor;

internal sealed class InventoryStack
{
    internal readonly List<Item> Items = new();
    internal Item Item => Items[0];
    internal int Count => Items.Count;
    internal readonly string? Error;
    internal bool CanResize => Error == null && Item.GetItemComponent<ItemComponent_Gold>() == null &&
        Item.GetItemComponent<ItemComponent_VendorItem>() == null;
    internal InventoryStack(Item item, string? error = null) { Items.Add(item); Error = error; }
}

internal static class InventoryStacks
{
    internal const int MaxQuantity = 10000;

    // InventoryUI groups by name and IsSameSerializedAs, which compares these exact
    // type-prefixed bytes. Serialize once per item instead of once per comparison.
    internal static List<InventoryStack> Group(IEnumerable<Item> items)
    {
        var rows = new List<InventoryStack>();
        var groups = new Dictionary<string, InventoryStack>(StringComparer.Ordinal);
        foreach (Item item in items)
        {
            string key;
            try { key = Key(item); }
            catch (Exception ex)
            {
                rows.Add(new InventoryStack(item, "Cannot read this item's saved data: " + ex.Message));
                continue;
            }
            if (groups.TryGetValue(key, out InventoryStack? stack) && stack != null) stack.Items.Add(item);
            else { stack = new InventoryStack(item); groups.Add(key, stack); rows.Add(stack); }
        }
        return rows;
    }

    internal static string Key(Item item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        TypeSerializationUtility.SerializeWithType(item, writer);
        writer.Flush();
        return item.name + "\0" + Convert.ToBase64String(stream.ToArray());
    }

    internal static InventoryStack Find(Inventory inventory, Item selected) =>
        Group(inventory.GetAllItemsShallowListCopy()).First(s => s.Items.Contains(selected));

    internal static InventoryService.RemovedItem? SetQuantity(Player player, Inventory inventory,
        Item selected, int expectedCount, int quantity)
    {
        InventoryService.RequireCurrent(player, inventory);
        InventoryService.RequireItem(inventory, selected);
        if (quantity < 0 || quantity > MaxQuantity)
            throw new InvalidDataException($"Stack count must be between 0 and {MaxQuantity:N0}.");
        InventoryStack stack = Find(inventory, selected);
        if (!stack.CanResize)
            throw new InvalidOperationException(stack.Error ?? "Currency and vendor-owned items cannot be resized as stacks.");
        if (stack.Count != expectedCount)
            throw new InvalidOperationException("The stack changed. Check its current count and try again.");
        if (quantity > stack.Count)
            InventoryService.AddToStack(player, inventory, selected, quantity - stack.Count);
        else if (quantity < stack.Count)
        {
            // Keep equipped objects and the inspected object whenever possible.
            var remove = stack.Items.OrderBy(player.IsItemEquipped)
                .ThenBy(i => ReferenceEquals(i, selected)).Take(stack.Count - quantity).ToList();
            return InventoryService.RemoveMany(player, inventory, remove);
        }
        return null;
    }
}
