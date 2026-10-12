using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SilverpineMods.InventoryEditor;

internal sealed class ItemEditOptions
{
    internal float? Durability;
    internal int? SpoilageTurns;
    internal int? HungerRestored, ThirstRestored;
    // null keeps every existing enchantment; "None" explicitly removes it.
    internal string? Enchantment;
    internal bool HasChanges => Durability.HasValue || SpoilageTurns.HasValue || Enchantment != null || HungerRestored.HasValue || ThirstRestored.HasValue;
}

internal sealed class ItemEditDraft
{
    internal readonly bool HasDurability, HasSpoilage, CanEnchant, IsEdible;
    internal readonly float MaxDurability;
    internal readonly string OriginalDurability, OriginalMinutes, CurrentEnchantment;
    internal string Durability, Minutes, Enchantment = "Unchanged";
    internal readonly string OriginalHunger, OriginalThirst;
    internal string Hunger, Thirst;
    internal bool HasChanges => Durability != OriginalDurability || Minutes != OriginalMinutes || Enchantment != "Unchanged" ||
        Hunger != OriginalHunger || Thirst != OriginalThirst;

    internal ItemEditDraft(Item item)
    {
        ItemComponent_Durability? condition = item.GetItemComponent<ItemComponent_Durability>();
        HasDurability = condition != null;
        MaxDurability = condition?.maxDurability ?? 0;
        OriginalDurability = Durability = (condition?.Durability ?? 0).ToString("R", CultureInfo.InvariantCulture);
        HasSpoilage = item.GetItemComponent<ItemComponent_Expirable>() != null;
        OriginalMinutes = Minutes = HasSpoilage ? (ItemEditing.GetSpoilageTurns(item) / 2m).ToString(CultureInfo.InvariantCulture) : "";
        CanEnchant = ItemComponent_Enchantment.IsEnchantable(item);
        CurrentEnchantment = string.Join(", ", item.GetItemComponents<ItemComponent_Enchantment>().Select(e => e.enchantmentType.ToString()));
        if (CurrentEnchantment.Length == 0) CurrentEnchantment = "None";
        IsEdible = item.GetItemComponent<ItemComponent_Edible>() != null;
        OriginalHunger = Hunger = IsEdible ? ItemEditing.GetHungerRestored(item).ToString(CultureInfo.InvariantCulture) : "";
        OriginalThirst = Thirst = IsEdible ? ItemEditing.GetThirstRestored(item).ToString(CultureInfo.InvariantCulture) : "";
    }

    internal ItemEditOptions Build()
    {
        var options = new ItemEditOptions();
        if (Durability != OriginalDurability)
        {
            if (!float.TryParse(Durability, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                throw new InvalidDataException("Enter a number for durability, using a period for decimals.");
            options.Durability = value;
        }
        if (Minutes != OriginalMinutes)
        {
            if (!decimal.TryParse(Minutes, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal minutes) ||
                minutes < 0.5m || minutes > 10000000m || minutes * 2m != decimal.Truncate(minutes * 2m))
                throw new InvalidDataException("Spoilage time must be 0.5–10,000,000 game minutes, in half-minute increments.");
            options.SpoilageTurns = (int)(minutes * 2m);
        }
        if (Enchantment != "Unchanged") options.Enchantment = Enchantment;
        if (Hunger != OriginalHunger) options.HungerRestored = ParseRestoration(Hunger, "Food");
        if (Thirst != OriginalThirst) options.ThirstRestored = ParseRestoration(Thirst, "Water");
        return options;
    }

    private static int ParseRestoration(string text, string label)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            throw new InvalidDataException(label + " restoration must be a whole number.");
        return value;
    }
}

internal static class ItemEditing
{
    // Match the installed Custom Item Loader's editable food-value range.
    internal const int MaxRestoration = 100000;
    // These are serialized native fields. Direct condition edits are deterministic:
    // setting durability must not invoke Unbreaking's random roll or destroy the item.
    private static readonly FieldInfo DurabilityField = RequiredField(typeof(ItemComponent_Durability), "durability", typeof(float));
    private static readonly FieldInfo SpoilageField = RequiredField(typeof(ItemComponent_Expirable), "turnsToExpire", typeof(int));
    private static readonly FieldInfo HungerField = RequiredField(typeof(ItemComponent_Edible), "hungerLoss", typeof(int));
    private static readonly FieldInfo ThirstField = RequiredField(typeof(ItemComponent_Edible), "thirstLoss", typeof(int));
    internal static readonly string[] Enchantments = new[] { "Unchanged", "None" }.Concat(Enum.GetNames(typeof(ItemComponent_Enchantment.EnchantmentType))).ToArray();

    private static FieldInfo RequiredField(Type type, string name, Type valueType)
    {
        FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || field.FieldType != valueType)
            throw new InvalidOperationException("The game's item condition format has changed: " + type.Name + "." + name);
        return field;
    }

    internal static int GetSpoilageTurns(Item item) => (int)SpoilageField.GetValue(item.GetItemComponent<ItemComponent_Expirable>())!;
    internal static int GetHungerRestored(Item item) => (int)HungerField.GetValue(item.GetItemComponent<ItemComponent_Edible>())!;
    internal static int GetThirstRestored(Item item) => (int)ThirstField.GetValue(item.GetItemComponent<ItemComponent_Edible>())!;

    internal static void Validate(Item item, ItemEditOptions options)
    {
        if (options.HungerRestored.HasValue || options.ThirstRestored.HasValue)
        {
            if (item.GetItemComponent<ItemComponent_Edible>() == null)
                throw new InvalidDataException("This item has no food or drink effects to edit.");
            foreach (int? value in new[] { options.HungerRestored, options.ThirstRestored })
                if (value.HasValue && (value.Value < -MaxRestoration || value.Value > MaxRestoration))
                    throw new InvalidDataException($"Food and water restoration must be between {-MaxRestoration:N0} and {MaxRestoration:N0}.");
        }
        if (options.Durability.HasValue)
        {
            var component = item.GetItemComponent<ItemComponent_Durability>();
            float value = options.Durability.Value;
            if (component == null) throw new InvalidDataException("This item does not have durability.");
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > component.maxDurability ||
                float.IsNaN(component.maxDurability) || float.IsInfinity(component.maxDurability) || component.maxDurability <= 0)
                throw new InvalidDataException($"Durability must be between 0 and {component.maxDurability:0.##}.");
        }
        if (options.SpoilageTurns.HasValue)
        {
            if (item.GetItemComponent<ItemComponent_Expirable>() == null)
                throw new InvalidDataException("This item does not have a spoilage timer.");
            if (options.SpoilageTurns < 1 || options.SpoilageTurns > 20000000)
                throw new InvalidDataException("Spoilage time must be 0.5–10,000,000 game minutes.");
        }
        if (options.Enchantment != null)
        {
            if (!ItemComponent_Enchantment.IsEnchantable(item))
                throw new InvalidDataException("This item cannot be enchanted under the game's equipment rules.");
            if (options.Enchantment != "None" && !Enum.GetNames(typeof(ItemComponent_Enchantment.EnchantmentType)).Contains(options.Enchantment))
                throw new InvalidDataException("Select an enchantment from the list, or None.");
        }
    }

    // Call only on detached copies or between the native UnequipItem/EquipItem calls.
    internal static void Apply(Item item, ItemEditOptions options)
    {
        Validate(item, options);
        if (options.Durability.HasValue)
            DurabilityField.SetValue(item.GetItemComponent<ItemComponent_Durability>(), options.Durability.Value);
        if (options.SpoilageTurns.HasValue)
            SpoilageField.SetValue(item.GetItemComponent<ItemComponent_Expirable>(), options.SpoilageTurns.Value);
        if (options.HungerRestored.HasValue)
            HungerField.SetValue(item.GetItemComponent<ItemComponent_Edible>(), options.HungerRestored.Value);
        if (options.ThirstRestored.HasValue)
            ThirstField.SetValue(item.GetItemComponent<ItemComponent_Edible>(), options.ThirstRestored.Value);
        if (options.Enchantment != null)
        {
            item.RemoveItemComponentsOfType<ItemComponent_Enchantment>();
            if (options.Enchantment != "None")
                item.AddItemComponent(new ItemComponent_Enchantment(Enum.Parse<ItemComponent_Enchantment.EnchantmentType>(options.Enchantment)));
        }
    }

    internal static void ApplyToInventory(Player player, Inventory inventory, Item item, ItemEditOptions options,
        bool custom, string name, string description)
    {
        InventoryService.RequireCurrent(player, inventory);
        InventoryService.RequireItem(inventory, item);
        Validate(item, options);
        if (custom) GeneratedItemText.Validate(name.Trim(), description);
        var durability = item.GetItemComponent<ItemComponent_Durability>();
        float? oldDurability = durability?.Durability;
        int? oldSpoilage = item.GetItemComponent<ItemComponent_Expirable>() == null ? null : GetSpoilageTurns(item);
        var edible = item.GetItemComponent<ItemComponent_Edible>();
        int? oldHunger = edible == null ? null : GetHungerRestored(item);
        int? oldThirst = edible == null ? null : GetThirstRestored(item);
        List<ItemComponent_Enchantment> oldEnchantments = item.GetItemComponents<ItemComponent_Enchantment>();
        string oldName = item.name, oldDescription = item.description;
        bool reEquip = options.Enchantment != null && player.IsItemEquipped(item);
        try
        {
            if (reEquip) player.UnequipItem(item);
            Apply(item, options);
            if (custom) { item.name = name.Trim(); item.description = description; }
            if (reEquip) player.EquipItem(item);
            inventory.UpdateEncumbered();
        }
        catch (Exception failure)
        {
            try
            {
                if (reEquip && player.IsItemEquipped(item)) player.UnequipItem(item);
                if (oldDurability.HasValue) DurabilityField.SetValue(durability, oldDurability.Value);
                if (oldSpoilage.HasValue) SpoilageField.SetValue(item.GetItemComponent<ItemComponent_Expirable>(), oldSpoilage.Value);
                if (oldHunger.HasValue) HungerField.SetValue(edible, oldHunger.Value);
                if (oldThirst.HasValue) ThirstField.SetValue(edible, oldThirst.Value);
                if (options.Enchantment != null)
                {
                    item.RemoveItemComponentsOfType<ItemComponent_Enchantment>();
                    foreach (var enchantment in oldEnchantments) item.AddItemComponent(enchantment);
                }
                item.name = oldName; item.description = oldDescription;
                if (reEquip) player.EquipItem(item);
                inventory.UpdateEncumbered();
            }
            catch (Exception rollback)
            { throw new InvalidOperationException(failure.Message + " Restoring the item also failed: " + rollback.Message, failure); }
            throw new InvalidOperationException("Item edits failed; original values restored. " + failure.Message, failure);
        }
    }
}
