using Mono.Cecil;
using SilverpineMods.InventoryEditor;

int checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Reject(Action action, string message)
{
    bool rejected = false;
    try { action(); } catch { rejected = true; }
    Check(rejected, message);
}
Player Fresh()
{
    var player = new Player(); Player.Instance = player;
    player.worldInventory.inventory.gold = 13;
    return player;
}
Item sword = new() { name = "Sword", description = "A battered blade.", bulk = 2, value = 32,
    slot = EquipmentSlotType.Weapon, components = new() { new Condition(), new ItemComponent_VendorItem() } };
Item coins = new() { name = "Gold", components = new() { new ItemComponent_Gold() } };
Item hidden = new() { name = "HIDDEN_test", bulk = 1 };
ItemLibrary.Items.AddRange(new[] { sword, coins, hidden });
Player p = Fresh(); Inventory bag = p.worldInventory.inventory;
List<Item> added = InventoryService.Add(p, bag, sword, 3, false, true, "  Traveler's Blade 2  ", "A blade that feels familiar.");
Check(added.Count == 3 && bag.items.Count == 3, "Add quantity failed");
Check(added.Select(i => i).Distinct().Count() == 3, "Copies share objects");
Check(added.All(i => i.name == "Traveler's Blade 2" && i.description == "A blade that feels familiar."), "Custom text missing");
Check(added.All(i => i.GetItemComponent<ItemComponent_VendorItem>() == null), "Vendor ownership retained");
Check(sword.name == "Sword" && sword.GetItemComponent<ItemComponent_VendorItem>() != null, "Global template mutated");
added[0].GetItemComponent<Condition>()!.durability = 11;
Check(added[1].GetItemComponent<Condition>()!.durability == 37, "Clone components are shared");
var duplicate = InventoryService.Add(p, bag, added[0], 1, true, false, "", "").Single();
Check(duplicate.name == added[0].name && duplicate.GetItemComponent<Condition>()!.durability == 11 && duplicate.bulk == 2, "Duplicate did not retain instance state");
p.EquipItem(added[0]);
InventoryService.Customize(p, bag, added[0], "Keepsake", "Your trusted blade.");
Check(p.IsItemEquipped(added[0]) && added[0].name == "Keepsake" && duplicate.name != "Keepsake", "Text edit changed other instances/equipment");
var receipt = InventoryService.Remove(p, bag, added[0]);
Check(!bag.ContainsItem(added[0]) && !p.IsItemEquipped(added[0]), "Native removal did not unequip");
receipt.Restore(); receipt.Restore();
Check(bag.items.Count(i => ReferenceEquals(i, added[0])) == 1 && p.IsItemEquipped(added[0]), "Undo did not restore exact item/equipment or duplicated it");
var stale = InventoryService.Remove(p, bag, duplicate);
Reject(() => InventoryService.Add(p, bag, duplicate, 1, true, false, "", ""), "Stale inventory selection accepted");
Reject(() => InventoryService.Customize(p, bag, duplicate, "Bad", ""), "Removed item was edited");
InventoryService.Add(p, bag, hidden, 1, false, false, "", "");
Check(bag.items.Any(i => i.name == hidden.name), "Internal library template inaccessible");

foreach (int quantity in new[] { 0, -1, 101, int.MaxValue })
    Reject(() => InventoryService.Add(p, bag, sword, quantity, false, false, "", ""), "Invalid batch size accepted");
int count = bag.items.Count;
bag.maxItems = count + 1;
Reject(() => InventoryService.Add(p, bag, sword, 2, false, false, "", ""), "Partial addition occurred at slot capacity");
Check(bag.items.Count == count, "Capacity failure changed inventory");
bag.maxItems = int.MaxValue; bag.maxBulk = (int)bag.GetBulk() + 1;
Reject(() => InventoryService.Add(p, bag, sword, 1, false, false, "", ""), "Bulk limit bypassed");
bag.allowEncumbered = true;
InventoryService.Add(p, bag, sword, 1, false, false, "", "");
Check(bag.encumbered, "Native encumbrance behavior not honored");
Item malformed = new() { name = "Malformed", bulk = float.NaN }; ItemLibrary.Items.Add(malformed);
Reject(() => InventoryService.Add(p, bag, malformed, 1, false, false, "", ""), "NaN bulk accepted");

p = Fresh(); bag = p.worldInventory.inventory; bag.maxItems = 1;
InventoryService.Add(p, bag, coins, 3, false, false, "", "");
Check(bag.items.Count == 0 && bag.gold == 28, "Currency pickup did not use native conversion");
int calls = 0;
Item failing = new() { name = "Broken pack item", added = i => { i.inventory!.gold += 7; if (++calls == 2) throw new Exception("Injected hook failure"); } };
ItemLibrary.Items.Add(failing); bag.maxItems = 20;
Reject(() => InventoryService.Add(p, bag, failing, 3, false, false, "", ""), "Broken item hook ignored");
Check(bag.items.Count == 0 && bag.gold == 28, "Failed batch left partial items/currency");
Item badRemoval = new() { name = "Removal hook fails", removed = _ => throw new Exception("Injected removal error") };
bag.TryAddItem(badRemoval);
Reject(() => InventoryService.Remove(p, bag, badRemoval), "Removal hook failure ignored");
Check(bag.ContainsItem(badRemoval), "Failed removal lost item");

string thirty = string.Join(" ", Enumerable.Repeat("word", 30));
GeneratedItemText.Validate(new string('A', 300), thirty);
Check(GeneratedItemText.LimitDescription(thirty + " extra") == thirty, "30-word cap failed");
Check(GeneratedItemText.Filter("One\ntwo\tthree", false) == "One two three", "Pasted whitespace joined words");
foreach (string name in new[] { " ", "<b>Bad</b>", "A|B", "Sword: Fire", "Bladé", "A\nB" })
    Reject(() => InventoryService.Customize(p, bag, badRemoval, name, "Fine."), "Invalid name accepted: " + name);
foreach (string description in new[] { "Has 2 edges.", "A\nB", thirty + " extra", "<b>Blade</b>" })
    Reject(() => InventoryService.Customize(p, bag, badRemoval, "Fine", description), "Invalid description accepted");

p = Fresh(); bag = p.worldInventory.inventory;
var burdenBlade = sword.DeepClone(); burdenBlade.spriteName = "custom-carry";
burdenBlade.components.Add(new ItemComponent_Enchantment { enchantmentType = ItemComponent_Enchantment.EnchantmentType.Effortlessness });
SilverpineMods.CustomItemLoader.Plugin.AttributeModifiers[burdenBlade.spriteName] = new() { carryCapacity = 25 };
bag.TryAddItem(burdenBlade); p.EquipItem(burdenBlade);
p.statusEffectsTarget.statusEffects.Add(new StatusEffect_LiftBurden()); bag.maxBulk += 1000;
Check(CharacterValues.CapacityBonus(p) == 1035 && CharacterValues.BaseCapacity(p, bag) == 50, "Base capacity did not subtract equipment/spell/custom bonuses");
CharacterValues.SetBaseCapacity(p, bag, 120);
Check(bag.maxBulk == 1155 && CharacterValues.BaseCapacity(p, bag) == 120, "Base capacity update erased bonuses");
p.UnequipItem(burdenBlade);
p.statusEffectsTarget.statusEffects.Clear(); bag.maxBulk -= 1000;
Check(bag.maxBulk == 120, "Base capacity changed when temporary bonuses expired");
CharacterValues.SetGold(p, bag, 123456);
Check(bag.gold == 123456 && bag.notifications > 0, "Gold did not refresh inventory");
foreach (int gold in new[] { -1, int.MaxValue }) Reject(() => CharacterValues.SetGold(p, bag, gold), "Invalid gold accepted");
foreach (int limit in new[] { 0, -1, int.MaxValue }) Reject(() => CharacterValues.SetBaseCapacity(p, bag, limit), "Invalid capacity accepted");
Check(bag.gold == 123456 && bag.maxBulk == 120, "Rejected stat edits changed values");
bag.allowEncumbered = true; bag.TryAddItem(new Item { name = "Heavy", bulk = 150 });
CharacterValues.SetBaseCapacity(p, bag, 200); Check(!bag.encumbered, "Raising limit did not clear encumbrance");
CharacterValues.SetBaseCapacity(p, bag, 100); Check(bag.encumbered, "Lowering limit did not update encumbrance");
Player.Instance = new Player();
Reject(() => CharacterValues.SetGold(p, bag, 1), "Stale character accepted");
Reject(() => receipt.Restore(), "Undo crossed character/save boundary");

// Condition edits preserve the actual object and use deterministic serialized values.
p = Fresh(); bag = p.worldInventory.inventory;
Item timedSword = new() { name = "Timed blade", bulk = 2, slot = EquipmentSlotType.Weapon,
    components = new() { new ItemComponent_Durability(), new ItemComponent_Expirable(60), new ItemComponent_Edible(10, 5, 7, true),
        new ItemComponent_Enchantment(ItemComponent_Enchantment.EnchantmentType.Effortlessness) } };
ItemLibrary.Items.Add(timedSword);
var working = InventoryService.Add(p, bag, timedSword, 1, false, false, "", "").Single();
p.EquipItem(working);
Check(bag.maxBulk == 60, "Initial enchantment bonus missing");
var draft = new ItemEditDraft(working);
Check(draft.HasDurability && draft.HasSpoilage && draft.CanEnchant && draft.Minutes == "60" && !draft.HasChanges, "Draft failed to read native condition");
draft.Durability = "37.5"; draft.Minutes = "1440.5"; draft.Enchantment = "Vitality";
ItemEditing.ApplyToInventory(p, bag, working, draft.Build(), true, "Edited blade", "A tested blade.");
Check(working.GetItemComponent<ItemComponent_Durability>()!.Durability == 37.5f, "Exact durability not assigned");
Check(ItemEditing.GetSpoilageTurns(working) == 2881, "Minutes did not convert to half-minute ticks");
Check(bag.maxBulk == 50 && p.IsItemEquipped(working), "Old enchantment bonus remained or equipped state lost");
Check(working.GetItemComponent<ItemComponent_Enchantment>()!.enchantmentType == ItemComponent_Enchantment.EnchantmentType.Vitality && working.name == "Edited blade", "Condition/text edit missing");
Check(timedSword.GetItemComponent<ItemComponent_Durability>()!.Durability == 100 && ItemEditing.GetSpoilageTurns(timedSword) == 120, "Condition edit changed template");
ItemEditing.ApplyToInventory(p, bag, working, new() { Enchantment = "Effortlessness" }, false, "", "");
Check(bag.maxBulk == 60 && p.IsItemEquipped(working), "New enchantment bonus not applied");
ItemEditing.ApplyToInventory(p, bag, working, new() { Enchantment = "Effortlessness" }, false, "", "");
Check(bag.maxBulk == 60, "Repeated edit stacked bonus");
ItemEditing.ApplyToInventory(p, bag, working, new() { Enchantment = "None" }, false, "", "");
Check(bag.maxBulk == 50 && working.GetItemComponent<ItemComponent_Enchantment>() == null, "Removing enchantment left a bonus/component");
var pristineDraft = new ItemEditDraft(working);
working.GetItemComponent<ItemComponent_Expirable>()!.Tick();
ItemEditing.ApplyToInventory(p, bag, working, pristineDraft.Build(), true, "Renamed", "");
Check(ItemEditing.GetSpoilageTurns(working) == 2880, "Unedited timer was reset by a text edit");
ItemEditing.ApplyToInventory(p, bag, working, new() { Durability = 0 }, false, "", "");
Check(bag.ContainsItem(working) && working.GetItemComponent<ItemComponent_Durability>()!.Durability == 0, "Zero durability invoked destruction callbacks");
var alteredCopies = InventoryService.Add(p, bag, working, 2, true, false, "", "", new() { Durability = 99, SpoilageTurns = 10, Enchantment = "Effortlessness" });
Check(alteredCopies.All(i => i.GetItemComponent<ItemComponent_Durability>()!.Durability == 99 && ItemEditing.GetSpoilageTurns(i) == 10), "Duplicate edits were not applied");
Check(working.GetItemComponent<ItemComponent_Durability>()!.Durability == 0 && bag.maxBulk == 50, "Duplicate mutated source or granted unequipped bonus");
foreach (float invalid in new[] { -1f, 101f, float.NaN, float.PositiveInfinity })
    Reject(() => ItemEditing.ApplyToInventory(p, bag, working, new() { Durability = invalid }, false, "", ""), "Invalid durability accepted");
foreach (string invalid in new[] { "0", "-1", "0.1", "1440.25", "10000001", "NaN", "1,440" })
{
    var badDraft = new ItemEditDraft(working) { Minutes = invalid };
    Reject(() => badDraft.Build(), "Invalid timer accepted: " + invalid);
}
foreach (string invalid in new[] { "999", "0", "vitality", "Typo" })
    Reject(() => ItemEditing.ApplyToInventory(p, bag, working, new() { Enchantment = invalid }, false, "", ""), "Invalid enchantment accepted");
Item plain = new() { name = "Rock" }; bag.TryAddItem(plain);
foreach (var invalid in new[] { new ItemEditOptions { Durability = 1 }, new ItemEditOptions { SpoilageTurns = 2 }, new ItemEditOptions { Enchantment = "Vitality" } })
    Reject(() => ItemEditing.ApplyToInventory(p, bag, plain, invalid, false, "", ""), "Unsupported component edit accepted");
ItemEditing.ApplyToInventory(p, bag, working, new() { Enchantment = "Effortlessness" }, false, "", "");
int componentCalls = 0;
working.addingComponent = _ => { if (++componentCalls == 1) throw new Exception("Injected enchantment installation failure"); };
Reject(() => ItemEditing.ApplyToInventory(p, bag, working, new() { Durability = 75, SpoilageTurns = 120, Enchantment = "Vitality", HungerRestored = 44, ThirstRestored = 22 }, true, "Failed name", "Failed description."), "Failed edit did not report failure");
Check(working.name == "Renamed" && working.GetItemComponent<ItemComponent_Durability>()!.Durability == 0 && ItemEditing.GetSpoilageTurns(working) == 2880, "Failed edit did not restore text and condition");
Check(bag.maxBulk == 60 && p.IsItemEquipped(working) && working.GetItemComponent<ItemComponent_Enchantment>()!.enchantmentType == ItemComponent_Enchantment.EnchantmentType.Effortlessness, "Failed edit did not restore equipment bonus");
Check(working.GetItemComponent<ItemComponent_Edible>()!.Effects == (10, 5), "Failed combined edit left food/water changes behind");
working.addingComponent = null;
InventoryService.Remove(p, bag, working);
Reject(() => ItemEditing.ApplyToInventory(p, bag, working, new() { Durability = 25 }, false, "", ""), "Stale item condition edit accepted");

// Food/drink values belong to the edible component, independent of item category.
p = Fresh(); bag = p.worldInventory.inventory;
Item soup = new() { name = "Soup", bulk = 1, components = new() { new ItemComponent_Edible(20, 10, 7, true), new ItemComponent_Expirable(1440) } };
ItemLibrary.Items.Add(soup);
var meals = InventoryService.Add(p, bag, soup, 2, false, false, "", "");
var meal = meals[0];
var foodDraft = new ItemEditDraft(meal);
Check(foodDraft.IsEdible && foodDraft.Hunger == "20" && foodDraft.Thirst == "10" && !foodDraft.HasChanges, "Food values were not populated from the item");
foodDraft.Hunger = "45"; foodDraft.Thirst = "30";
Check(foodDraft.HasChanges && foodDraft.Build().HasChanges, "Food-only edits did not enable Apply");
ItemEditing.ApplyToInventory(p, bag, meal, foodDraft.Build(), false, "", "");
Check(meal.GetItemComponent<ItemComponent_Edible>()!.Effects == (45, 30), "Food and water edits did not reach the consumption fields");
Check(meals[1].GetItemComponent<ItemComponent_Edible>()!.Effects == (20, 10) && soup.GetItemComponent<ItemComponent_Edible>()!.Effects == (20, 10), "Food edits changed another instance or template");
Check(meal.GetItemComponent<ItemComponent_Edible>()!.sanityGain == 7 && meal.GetItemComponent<ItemComponent_Edible>()!.wellFed && ItemEditing.GetSpoilageTurns(meal) == 2880, "Food edit changed sanity, well-fed or expiry");
var foodCopies = InventoryService.Add(p, bag, meal, 2, true, false, "", "", new() { HungerRestored = 0, ThirstRestored = -5 });
Check(foodCopies.All(i => i.GetItemComponent<ItemComponent_Edible>()!.Effects == (0, -5)) && meal.GetItemComponent<ItemComponent_Edible>()!.Effects == (45, 30), "Duplicate edits did not retain zero/negative values independently");
var customizedMeal = InventoryService.Add(p, bag, soup, 1, false, true, "Trail soup", "A hearty meal.", new() { HungerRestored = 75, ThirstRestored = 50 }).Single();
Check(customizedMeal.name == "Trail soup" && customizedMeal.GetItemComponent<ItemComponent_Edible>()!.Effects == (75, 50), "New custom item did not receive food edits");
var singleFieldDraft = new ItemEditDraft(meal) { Thirst = "25" };
ItemEditing.Apply(meal, new() { HungerRestored = 60 });
ItemEditing.ApplyToInventory(p, bag, meal, singleFieldDraft.Build(), false, "", "");
Check(meal.GetItemComponent<ItemComponent_Edible>()!.Effects == (60, 25), "Water-only edit overwrote a newer food value");
Item drink = new() { name = "Water", components = new() { new ItemComponent_Edible(0, 30) } }; bag.TryAddItem(drink);
Check(new ItemEditDraft(drink).IsEdible, "Beverage without a spoilage timer was excluded");
ItemEditing.ApplyToInventory(p, bag, drink, new() { ThirstRestored = 100 }, false, "", "");
Check(drink.GetItemComponent<ItemComponent_Edible>()!.Effects == (0, 100), "Beverage restoration edit failed");
foreach (string invalid in new[] { "", "1.5", "NaN", "1,000", "2147483648" })
{
    Reject(() => new ItemEditDraft(meal) { Hunger = invalid }.Build(), "Invalid food number accepted");
    Reject(() => new ItemEditDraft(meal) { Thirst = invalid }.Build(), "Invalid water number accepted");
}
foreach (int invalid in new[] { -100001, 100001, int.MinValue, int.MaxValue })
{
    Reject(() => ItemEditing.ApplyToInventory(p, bag, meal, new() { HungerRestored = invalid, ThirstRestored = 99 }, false, "", ""), "Out-of-range food accepted");
    Reject(() => ItemEditing.ApplyToInventory(p, bag, meal, new() { HungerRestored = 99, ThirstRestored = invalid }, false, "", ""), "Out-of-range water accepted");
}
Check(meal.GetItemComponent<ItemComponent_Edible>()!.Effects == (60, 25), "Rejected edit partially changed food values");
ItemEditing.ApplyToInventory(p, bag, meal, new() { HungerRestored = -100000, ThirstRestored = 100000 }, false, "", "");
Check(meal.GetItemComponent<ItemComponent_Edible>()!.Effects == (-100000, 100000), "Valid editor boundary rejected");
Item rockFood = new() { name = "Rock" }; bag.TryAddItem(rockFood);
Check(!new ItemEditDraft(rockFood).IsEdible, "Non-edible item exposed food controls");
Reject(() => ItemEditing.ApplyToInventory(p, bag, rockFood, new() { HungerRestored = 10 }, false, "", ""), "Added food effects to a non-edible item");
Reject(() => ItemEditing.ApplyToInventory(p, bag, rockFood, new() { ThirstRestored = 10 }, false, "", ""), "Added water effects to a non-edible item");
InventoryService.Remove(p, bag, meal);
Reject(() => ItemEditing.ApplyToInventory(p, bag, meal, new() { HungerRestored = 10 }, false, "", ""), "Removed item accepted food edits");

// Inspect the actual installed assemblies for API, save format, bonus and grammar assumptions.
string gameRoot = Path.GetFullPath(args[0]);
using var game = AssemblyDefinition.ReadAssembly(Path.Combine(gameRoot, "Silverpine_Data/Managed/Assembly-CSharp.dll"));
IEnumerable<TypeDefinition> Nested(TypeDefinition type) => new[] { type }.Concat(type.NestedTypes.SelectMany(Nested));
List<string> Strings(TypeDefinition type) => Nested(type).SelectMany(t => t.Methods).Where(m => m.HasBody)
    .SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<string>().ToList();
TypeDefinition Type(string name) => game.MainModule.Types.Single(t => t.Name == name);
var nativeText = Strings(Type("NPCFunction_GiveItem"));
Check(nativeText.Contains("root ::= [A-Za-z0-9 .,!?'-]*") && nativeText.Contains("root ::= [A-Za-z .,!?'-]*") && nativeText.Any(s => s.Contains("no more than 30 words")), "Native text grammar changed");
var serialize = Type("Inventory").Methods.Single(m => m.Name == "Serialize");
var saved = serialize.Body.Instructions.Select(i => i.Operand).OfType<string>().ToList();
Check(new[] { "gold", "maxBulk", "items", "allowEncumbered" }.All(saved.Contains), "Native persistence contract changed");
Check(new[] { "name", "description" }.All(Strings(Type("Item")).Contains), "Item text persistence contract changed");
Check(Type("ItemComponent_Durability").Fields.Any(f => f.Name == "durability" && f.IsPrivate && f.FieldType.FullName == "System.Single"), "Durability field contract changed");
Check(Type("ItemComponent_Expirable").Fields.Any(f => f.Name == "turnsToExpire" && f.IsPrivate && f.FieldType.FullName == "System.Int32"), "Spoilage field contract changed");
Check(Strings(Type("ItemComponent_Durability")).Contains("durability") && Strings(Type("ItemComponent_Expirable")).Contains("turnsToExpire"), "Condition serialization changed");
foreach (string fieldName in new[] { "hungerLoss", "thirstLoss" })
{
    var edibleType = Type("ItemComponent_Edible");
    Check(edibleType.Fields.Any(f => f.Name == fieldName && f.IsPrivate && f.FieldType.FullName == "System.Int32"), "Native food/water field contract changed");
    Check(new[] { "Serialize", "Deserialize" }.All(method => edibleType.Methods.Single(m => m.Name == method).Body.Instructions.Any(i => i.Operand is string text && text == fieldName)), "Food/water value is not saved and restored");
    Check(edibleType.Methods.Where(m => m.Name.Contains("<OnUse>")).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is FieldReference f && f.Name == fieldName), "Consumption no longer uses the edited field");
}
var expiryConstructor = Type("ItemComponent_Expirable").Methods.Single(m => m.IsConstructor && m.Parameters.Count == 1);
Check(expiryConstructor.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_2) && expiryConstructor.Body.Instructions.Any(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Mul), "Spoilage ticks-per-minute changed");
Check(Strings(Type("NPCFunction_ServeFood")).Any(s => s.Contains("Which one of these sprites")), "Generated-food appearance selection changed");
var unequip = Type("Inventory").Methods.Single(m => m.Name == "RemoveItem").Body.Instructions.Select(i => i.Operand).OfType<MethodReference>();
Check(unequip.Any(m => m.Name == "UnequipItem"), "Native removal stopped unequipping");
foreach (string method in new[] { "OnApply", "OnExpire" })
    Check(Type("StatusEffect_LiftBurden").Methods.Single(m => m.Name == method).Body.Instructions.Any(i => i.Operand is int n && n == 1000), "Lift Burden amount changed");
using var loader = AssemblyDefinition.ReadAssembly(Path.Combine(gameRoot, "BepInEx/plugins/CustomItemLoader/CustomItemLoader.dll"));
Check(loader.MainModule.Types.Single(t => t.FullName == "SilverpineMods.CustomItemLoader.Plugin").Fields.Any(f => f.Name == "AttributeModifiers"), "Custom modifier registry moved");
Check(loader.MainModule.Types.Single(t => t.Name == "AttributeModifiersDefinition").Fields.Any(f => f.Name == "carryCapacity" && f.FieldType.FullName == "System.Int32"), "Custom carrying modifier changed");
string pluginPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../bin/Release/netstandard2.1/InventoryEditor.dll"));
using var plugin = AssemblyDefinition.ReadAssembly(pluginPath);
var types = plugin.MainModule.Types.SelectMany(Nested).ToList();
var callsInPlugin = types.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<MethodReference>().ToList();
Check(callsInPlugin.Any(m => m.DeclaringType.Name == "InventoryModTools" && m.Name == "RegisterSession"), "Framework menu registration missing");
Check(types.Any(t => t.BaseType?.Name == "ModToolBehaviour"), "Framework session lifetime missing");
Check(!callsInPlugin.Any(m => m.Name.StartsWith("Unpatch") || m.DeclaringType.Name == "ModdingToolsMenu"), "Unexpected bootstrap cleanup or main-menu UI");
Console.WriteLine($"PASS: {checks} inventory, text, currency, capacity, rollback and installed-assembly contract checks.");
