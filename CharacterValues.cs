using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace SilverpineMods.InventoryEditor;

internal static class CharacterValues
{
    internal const int MaxGold = 1000000000;
    internal const int MaxBaseCapacity = 1000000;

    internal static int CapacityBonus(Player player)
    {
        int bonus = 0;
        checked
        {
            foreach (Item item in player.equippedItems.Values.Where(i => i != null).Distinct())
            {
                bonus += item.GetItemComponents<ItemComponent_Enchantment>()
                    .Count(e => e.enchantmentType == ItemComponent_Enchantment.EnchantmentType.Effortlessness) * 10;
                bonus += CustomItemBonus(item);
            }
            if (player.statusEffectsTarget != null)
                bonus += player.statusEffectsTarget.statusEffects.OfType<StatusEffect_LiftBurden>().Count() * 1000;
        }
        return bonus;
    }

    // Optional compatibility: the installed Custom Item Loader applies this modifier
    // by sprite name from an internal registry, rather than an item component.
    private static int CustomItemBonus(Item item)
    {
        Type? plugin = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("SilverpineMods.CustomItemLoader.Plugin", false)).FirstOrDefault(t => t != null);
        if (plugin == null) return 0;
        FieldInfo? field = plugin.GetField("AttributeModifiers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (field?.GetValue(null) is not IDictionary registry)
            throw new InvalidOperationException("Cannot read Custom Item Loader's carrying bonuses. Capacity editing is unavailable.");
        if (string.IsNullOrWhiteSpace(item.spriteName) || !registry.Contains(item.spriteName)) return 0;
        object definition = registry[item.spriteName]!;
        if (definition.GetType().GetField("carryCapacity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(definition) is not int amount)
            throw new InvalidOperationException("Cannot read this custom item's carrying bonus.");
        return amount;
    }

    internal static int BaseCapacity(Player player, Inventory inventory) => checked(inventory.maxBulk - CapacityBonus(player));

    internal static void SetGold(Player player, Inventory inventory, int gold)
    {
        InventoryService.RequireCurrent(player, inventory);
        if (gold < 0 || gold > MaxGold) throw new ArgumentOutOfRangeException(nameof(gold), $"Gold must be between 0 and {MaxGold:N0}.");
        int original = inventory.gold;
        try { inventory.gold = gold; inventory.UpdateEncumbered(); }
        catch { inventory.gold = original; inventory.UpdateEncumbered(); throw; }
    }

    internal static void SetBaseCapacity(Player player, Inventory inventory, int baseCapacity)
    {
        InventoryService.RequireCurrent(player, inventory);
        if (baseCapacity < 1 || baseCapacity > MaxBaseCapacity)
            throw new ArgumentOutOfRangeException(nameof(baseCapacity), $"Base capacity must be between 1 and {MaxBaseCapacity:N0} bulk.");
        int effective = checked(baseCapacity + CapacityBonus(player));
        // Player movement compares against maxBulk * 2 using int arithmetic.
        if (effective < 1 || effective > int.MaxValue / 2)
            throw new InvalidOperationException("Equipment bonuses would produce an invalid total carrying capacity.");
        int original = inventory.maxBulk;
        try { inventory.maxBulk = effective; inventory.UpdateEncumbered(); }
        catch { inventory.maxBulk = original; inventory.UpdateEncumbered(); throw; }
    }
}
