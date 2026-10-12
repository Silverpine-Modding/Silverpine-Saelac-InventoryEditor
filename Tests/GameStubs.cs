// Test doubles for Unity-only types; the installed game DLL is inspected separately.
public enum EquipmentSlotType { NotEquipable, Chest, Weapon }
public class ItemComponent_VendorItem { }
public class ItemComponent_Gold { }
public class Condition { public int durability = 37; }
public class ItemComponent_Edible
{
    private int hungerLoss, thirstLoss;
    public readonly int sanityGain;
    public readonly bool wellFed;
    public ItemComponent_Edible(int food, int water, int sanity = 0, bool meal = false)
    { hungerLoss = food; thirstLoss = water; sanityGain = sanity; wellFed = meal; }
    public ItemComponent_Edible Copy() => new(hungerLoss, thirstLoss, sanityGain, wellFed);
    public (int Hunger, int Thirst) Effects => (hungerLoss, thirstLoss);
}
public class ItemComponent_Durability
{
    public float maxDurability = 100;
    private float durability = 100;
    public float Durability { get => durability; set => throw new Exception("A direct editor assignment must bypass randomized/destructive damage callbacks."); }
    public ItemComponent_Durability Copy()
    { var copy = new ItemComponent_Durability { maxDurability = maxDurability }; copy.durability = durability; return copy; }
}
public class ItemComponent_Expirable
{
    private int turnsToExpire;
    public ItemComponent_Expirable(int minutes) => turnsToExpire = minutes * 2;
    public ItemComponent_Expirable Copy() { var copy = new ItemComponent_Expirable(1); copy.turnsToExpire = turnsToExpire; return copy; }
    public void Tick() { if (turnsToExpire > 0) turnsToExpire--; }
}
public class ItemComponent_Enchantment
{
    public enum EnchantmentType { Effortlessness, Vitality }
    public EnchantmentType enchantmentType;
    public ItemComponent_Enchantment() { }
    public ItemComponent_Enchantment(EnchantmentType value) => enchantmentType = value;
    public static bool IsEnchantable(Item item) => item.slot != EquipmentSlotType.NotEquipable;
}
public class Item
{
    public string name = "", description = "", spriteName = "";
    public float bulk;
    public int value;
    public EquipmentSlotType slot;
    public Inventory? inventory;
    public Action<Item>? added, removed;
    public List<object> components = new();
    public T? GetItemComponent<T>() => components.OfType<T>().FirstOrDefault();
    public List<T> GetItemComponents<T>() => components.OfType<T>().ToList();
    public void RemoveItemComponentsOfType<T>() => components.RemoveAll(c => c is T);
    public Action<object>? addingComponent;
    public void AddItemComponent(object component) { addingComponent?.Invoke(component); components.Add(component); }
    public Item DeepClone() => new()
    {
        name = name, description = description, spriteName = spriteName, bulk = bulk, value = value,
        slot = slot, added = added, removed = removed,
        components = components.Select(c => c switch
        {
            Condition d => (object)new Condition { durability = d.durability },
            ItemComponent_Edible e => e.Copy(),
            ItemComponent_Durability d => d.Copy(),
            ItemComponent_Expirable e => e.Copy(),
            ItemComponent_Enchantment e => new ItemComponent_Enchantment { enchantmentType = e.enchantmentType },
            ItemComponent_VendorItem => new ItemComponent_VendorItem(),
            ItemComponent_Gold => new ItemComponent_Gold(),
            _ => throw new Exception("Unknown test component")
        }).ToList()
    };
}
public static class ItemLibrary { public static List<Item> Items = new(); }
public class Inventory
{
    public List<Item> items = new();
    public int maxItems = int.MaxValue, maxBulk = 50, gold;
    public bool allowEncumbered, encumbered;
    public int notifications;
    public float GetBulk() => items.Sum(i => i.bulk);
    public bool ContainsItem(Item item) => items.Contains(item);
    public bool CanAddItem(Item item) => (allowEncumbered || GetBulk() + item.bulk <= maxBulk) && items.Count + 1 <= maxItems;
    public bool TryAddItem(Item item)
    {
        if (!CanAddItem(item)) return false;
        items.Add(item); item.inventory = this; UpdateEncumbered(); item.added?.Invoke(item);
        if (item.GetItemComponent<ItemComponent_Gold>() != null) { gold += 5; RemoveItem(item); }
        return true;
    }
    public void RemoveItem(Item item)
    {
        if (Player.Instance.IsItemEquipped(item)) Player.Instance.UnequipItem(item);
        items.Remove(item); UpdateEncumbered(); item.removed?.Invoke(item);
    }
    public List<Item> GetAllItemsShallowListCopy() => new(items);
    public void UpdateEncumbered() { encumbered = GetBulk() > maxBulk; notifications++; }
}
public class WorldInventory { public Inventory inventory = new(); }
public class StatusEffect { }
public class StatusEffect_LiftBurden : StatusEffect { }
public class StatusEffectsTarget { public List<StatusEffect> statusEffects = new(); }
public class Player
{
    public static Player Instance = new();
    public WorldInventory worldInventory = new();
    public StatusEffectsTarget statusEffectsTarget = new();
    public Dictionary<EquipmentSlotType, Item> equippedItems = new();
    public bool IsItemEquipped(Item item) => equippedItems.Values.Contains(item);
    public void EquipItem(Item item)
    {
        if (equippedItems.TryGetValue(item.slot, out Item? previous)) UnequipItem(previous);
        equippedItems[item.slot] = item;
        worldInventory.inventory.maxBulk += Bonus(item);
    }
    public void UnequipItem(Item item)
    { equippedItems.Remove(item.slot); worldInventory.inventory.maxBulk -= Bonus(item); }
    private int Bonus(Item item) => item.GetItemComponents<ItemComponent_Enchantment>()
        .Count(e => e.enchantmentType == ItemComponent_Enchantment.EnchantmentType.Effortlessness) * 10 +
        (SilverpineMods.CustomItemLoader.Plugin.AttributeModifiers.TryGetValue(item.spriteName, out var definition) ? definition.carryCapacity : 0);
}
namespace SilverpineMods.CustomItemLoader
{
    internal class Definition { public int carryCapacity; }
    internal static class Plugin
    { internal static readonly Dictionary<string, Definition> AttributeModifiers = new(); }
}
