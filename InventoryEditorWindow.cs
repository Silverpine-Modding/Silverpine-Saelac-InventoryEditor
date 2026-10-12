using Silverpine.ModdingTools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SilverpineMods.InventoryEditor;

internal sealed class InventoryEditorWindow : ModToolBehaviour
{
    private const float Width = 1440, Height = 900, RowHeight = 72;
    private static InventoryEditorWindow? instance;
    private Player player = null!;
    private Inventory inventory = null!;
    private EditorTheme? theme;
    private List<Item> catalog = new(), catalogMatches = new(), owned = new(), ownedMatches = new();
    private readonly Dictionary<Item, Sprite?> icons = new();
    private readonly Dictionary<Item, string> rowNames = new();
    private readonly HashSet<Item> equipped = new();
    private Item? selected;
    private ItemEditDraft? itemDraft;
    private int detailTab;
    private bool showEnchantments;
    private bool selectedOwned, custom, dirtyInventory = true, goldDirty, capacityDirty, showCategories;
    private string search = "", inventorySearch = "", category = "All items", quantity = "1";
    private string customName = "", customDescription = "", detailText = "", goldText = "", capacityText = "", capacityError = "";
    private string status = "Changes apply immediately. Save your game normally to keep them.";
    private bool statusError;
    private int bonus, baseCapacity;
    private float carriedBulk, nextRefresh;
    private Vector2 catalogScroll, inventoryScroll, detailScroll;
    private Action? pending;
    private InventoryService.RemovedItem? removed;
    private static readonly string[] Categories = new[] { "All items" }.Concat(Enum.GetNames(typeof(ItemCategory))).ToArray();

    internal static void Open(ModToolSession session)
    {
        try
        {
            if (instance != null) instance.Close();
            Player current = Player.Instance;
            if (current == null || current.worldInventory == null)
                throw new InvalidOperationException("Load a character before opening Inventory Editor.");
            instance = new GameObject("Inventory Editor").AddComponent<InventoryEditorWindow>();
            instance.AttachSession(session);
            instance.player = current;
            instance.inventory = current.worldInventory.inventory;
            instance.inventory.OnInventoryChanged += instance.OnInventoryChanged;
            instance.RefreshCatalog();
            instance.RefreshInventory();
        }
        catch
        {
            session.Close();
            if (instance != null) Destroy(instance.gameObject);
            throw;
        }
    }

    private void Update()
    {
        if (player == null || player != Player.Instance || player.worldInventory == null ||
            !ReferenceEquals(player.worldInventory.inventory, inventory)) { Close(); return; }
        Action? action = pending;
        pending = null;
        if (action != null)
        {
            try { action(); }
            catch (Exception ex) { SetStatus(ex.Message, true); Plugin.Log.LogWarning(ex); }
            dirtyInventory = true;
        }
        if (dirtyInventory || Time.unscaledTime >= nextRefresh)
        {
            try { RefreshInventory(); }
            catch (Exception ex) { SetStatus("Could not refresh inventory: " + ex.Message, true); }
            nextRefresh = Time.unscaledTime + 0.5f;
            dirtyInventory = false;
        }
    }

    private void OnInventoryChanged() => dirtyInventory = true;

    private void RefreshCatalog()
    {
        // Include every loaded template, including internal HIDDEN_ entries; do not collapse same-name variants.
        catalog = ItemLibrary.Items.Where(i => i != null).OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase).ToList();
        icons.Clear();
        FilterCatalog();
    }

    private void FilterCatalog()
    {
        catalogMatches = catalog.Where(i => (category == "All items" || i.itemCategory.ToString() == category) && Matches(i, search)).ToList();
        catalogScroll = Vector2.zero;
    }

    private void FilterOwned()
    {
        ownedMatches = owned.Where(i => Matches(i, inventorySearch)).ToList();
        inventoryScroll.y = Mathf.Min(inventoryScroll.y, Mathf.Max(0, ownedMatches.Count * RowHeight - 405));
    }

    private static bool Matches(Item item, string query) => string.IsNullOrWhiteSpace(query) ||
        ((item.name ?? "") + " " + (item.description ?? "") + " " + item.itemCategory)
        .IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

    private void RefreshInventory()
    {
        owned = inventory.GetAllItemsShallowListCopy().Where(i => i != null).ToList();
        equipped.Clear();
        foreach (Item item in player.equippedItems.Values) if (item != null) equipped.Add(item);
        owned = owned.OrderByDescending(equipped.Contains).ThenBy(i => i.name, StringComparer.OrdinalIgnoreCase).ToList();
        rowNames.Clear();
        foreach (Item item in owned)
        {
            try { rowNames[item] = Plain(item.GetFinalName()); }
            catch { rowNames[item] = item.name ?? "Unnamed item"; }
        }
        FilterOwned();
        carriedBulk = inventory.GetBulk();
        if (!goldDirty) goldText = inventory.gold.ToString();
        try
        {
            bonus = CharacterValues.CapacityBonus(player);
            baseCapacity = checked(inventory.maxBulk - bonus);
            capacityError = "";
            if (!capacityDirty) capacityText = baseCapacity.ToString();
        }
        catch (Exception ex) { capacityError = ex.Message; }
        if (selectedOwned && selected != null && !inventory.ContainsItem(selected))
        { selected = null; custom = false; detailText = ""; }
    }

    private void Select(Item item, bool fromInventory)
    {
        selected = item;
        selectedOwned = fromInventory;
        custom = false;
        itemDraft = new ItemEditDraft(item);
        customName = GeneratedItemText.Filter(item.name ?? "", true);
        customDescription = GeneratedItemText.LimitDescription(item.description ?? "");
        detailScroll = Vector2.zero;
        try { detailText = Plain(item.GetFinalDescription()); }
        catch { detailText = Plain(item.description ?? ""); }
        GUI.FocusControl(null);
    }

    private static string Plain(string text) => Regex.Replace(text, "<[^>]*>", "");
    private void SetStatus(string message, bool error = false) { status = message; statusError = error; }

    private void Close()
    {
        pending = null;
        ReleaseSession();
        Destroy(gameObject);
    }

    protected override void OnDisable()
    {
        pending = null;
        if (inventory != null) inventory.OnInventoryChanged -= OnInventoryChanged;
        base.OnDisable();
        Destroy(gameObject);
    }

    protected override void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= OnInventoryChanged;
        base.OnDestroy();
        theme?.Dispose();
        icons.Clear();
        if (instance == this) instance = null;
    }

    private void OnGUI()
    {
        if (inventory == null || player == null || FrameworkSession == null || FrameworkSession.IsClosed) return;
        using ModGuiScope scope = ModGui.BeginScaled(Width, Height);
        GUISkin previous = GUI.skin;
        theme ??= new EditorTheme(previous);
        try
        {
            GUI.skin = theme.Skin;
            GUI.enabled = true;
            GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
            GUI.depth = -1000;
            Fill(new Rect(-10000, -10000, 20000, 20000), EditorTheme.Background);
            Fill(new Rect(28, 22, 40, 3), EditorTheme.Accent);
            GUI.Label(new Rect(28, 34, 520, 20), "CHARACTER TOOLS  /  LIVE INVENTORY", theme.Eyebrow);
            GUI.Label(new Rect(28, 54, 580, 44), "Inventory Editor", theme.Title);
            GUI.Label(new Rect(725, 44, 400, 44), new GUIContent(player.playerName + "  /  changes apply live", player.playerName), theme.Badge);
            if (GUI.Button(new Rect(1140, 48, 136, 38), "Refresh library")) pending = () =>
            { RefreshCatalog(); SetStatus($"Item library refreshed. {catalog.Count} templates available."); };
            if (GUI.Button(new Rect(1288, 48, 124, 38), "Close  /  Esc")) Close();

            bool bodyEnabled = !showCategories && !showEnchantments;
            GUI.enabled = bodyEnabled;
            DrawCharacterValues();
            DrawCatalog();
            DrawInventory();
            DrawDetails();
            GUI.enabled = true;
            Color before = GUI.contentColor;
            if (statusError) GUI.contentColor = new Color(1f, 0.68f, 0.64f);
            GUI.Label(new Rect(28, 833, 1384, 56), status, theme.Status);
            GUI.contentColor = before;
            if (showCategories) DrawCategoryPicker();
            if (showEnchantments) DrawEnchantmentPicker();
        }
        finally { GUI.skin = previous; }
    }

    private void DrawCharacterValues()
    {
        GUI.Box(new Rect(28, 108, 1384, 94), GUIContent.none);
        GUI.Label(new Rect(46, 121, 245, 21), "GOLD  /  " + inventory.gold.ToString("N0"), theme!.Eyebrow);
        GUI.Label(new Rect(375, 121, 385, 21), "BASE CARRYING CAPACITY", theme.Eyebrow);
        string gold = GUI.TextField(new Rect(46, 150, 190, 36), goldText, 10);
        if (gold != goldText) { goldText = gold; goldDirty = true; }
        if (GUI.Button(new Rect(244, 150, 94, 36), "Set gold"))
        {
            string input = goldText;
            pending = () =>
            {
                if (!int.TryParse(input, out int amount)) throw new InvalidOperationException("Enter a whole number for gold.");
                CharacterValues.SetGold(player, inventory, amount);
                goldDirty = false;
                SetStatus("Gold set to " + amount.ToString("N0") + ". Save your game to keep the change.");
            };
        }
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && capacityError.Length == 0;
        string capacity = GUI.TextField(new Rect(375, 150, 170, 36), capacityText, 8);
        if (capacity != capacityText) { capacityText = capacity; capacityDirty = true; }
        if (GUI.Button(new Rect(553, 150, 128, 36), "Set base limit"))
        {
            string input = capacityText;
            pending = () =>
            {
                if (!int.TryParse(input, out int amount)) throw new InvalidOperationException("Enter a whole number for base carrying capacity.");
                CharacterValues.SetBaseCapacity(player, inventory, amount);
                capacityDirty = false;
                SetStatus($"Base capacity set to {amount:N0} bulk. Equipment and spell bonuses are preserved.");
            };
        }
        GUI.enabled = enabled;
        GUI.Label(new Rect(704, 119, 684, 26), $"CARRIED  {carriedBulk:0.##}   /   TOTAL LIMIT  {inventory.maxBulk:N0}   /   {owned.Count} ITEMS", theme.RowTitle);
        GUI.Label(new Rect(704, 149, 684, 40), capacityError.Length > 0 ? capacityError :
            $"{baseCapacity:N0} base  +  {bonus:N0} bonuses     |     " + (inventory.encumbered ? "Encumbered" : "Within carrying capacity"), theme.Muted);
    }

    private void Panel(Rect rect, string eyebrow, string title)
    {
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 16, rect.y + 14, rect.width - 32, 20), eyebrow, theme!.Eyebrow);
        GUI.Label(new Rect(rect.x + 16, rect.y + 40, rect.width - 32, 32), title, theme.Heading);
    }

    private void DrawCatalog()
    {
        Panel(new Rect(28, 218, 424, 606), "01  /  DISCOVER", "Item library");
        GUI.Label(new Rect(44, 294, 390, 21), "Search names, descriptions, or categories", theme!.Muted);
        string query = GUI.TextField(new Rect(44, 320, 392, 36), search, 160);
        if (query != search) { search = query; FilterCatalog(); }
        if (GUI.Button(new Rect(44, 366, 392, 36), category + "   /   Choose category")) showCategories = true;
        GUI.Label(new Rect(44, 410, 390, 24), $"{catalogMatches.Count} results  /  {catalog.Count} loaded templates", theme.Muted);
        DrawRows(new Rect(44, 444, 392, 320), catalogMatches, ref catalogScroll, false);
        GUI.Label(new Rect(44, 777, 392, 34), "Includes game items, loaded packs and internal templates.", theme.Muted);
    }

    private void DrawInventory()
    {
        Panel(new Rect(468, 218, 416, 606), "02  /  CARRY", "Current inventory");
        GUI.Label(new Rect(484, 294, 380, 21), "Search your items  /  equipped items appear first", theme!.Muted);
        string query = GUI.TextField(new Rect(484, 320, 384, 36), inventorySearch, 160);
        if (query != inventorySearch) { inventorySearch = query; inventoryScroll = Vector2.zero; FilterOwned(); }
        GUI.Label(new Rect(484, 364, 380, 25), $"{ownedMatches.Count} shown  /  {equipped.Count} equipped", theme.Muted);
        DrawRows(new Rect(484, 401, 384, 363), ownedMatches, ref inventoryScroll, true);
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && removed != null;
        if (GUI.Button(new Rect(484, 778, 384, 32), "Undo last removal")) pending = () =>
        {
            InventoryService.RemovedItem? receipt = removed;
            if (receipt == null) return;
            receipt.Restore();
            removed = null;
            Select(receipt.Item, true);
            SetStatus("Restored " + receipt.Item.name + ".");
        };
        GUI.enabled = enabled;
    }

    private void DrawRows(Rect viewport, List<Item> items, ref Vector2 scroll, bool fromInventory)
    {
        if (items.Count == 0)
        {
            GUI.Label(new Rect(viewport.x + 12, viewport.y + 28, viewport.width - 24, 100),
                fromInventory ? "No items to show.\nChoose a template from the library to add something." : "No matching items.\nTry another search or category.", theme!.Muted);
            return;
        }
        scroll.y = Mathf.Clamp(scroll.y, 0, Mathf.Max(0, items.Count * RowHeight - viewport.height));
        float width = viewport.width - 18;
        scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, width, items.Count * RowHeight), false, false);
        int first = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
        int last = Mathf.Min(items.Count, Mathf.CeilToInt((scroll.y + viewport.height) / RowHeight) + 1);
        for (int i = first; i < last; i++)
        {
            Item item = items[i];
            float y = i * RowHeight;
            bool active = ReferenceEquals(item, selected) && selectedOwned == fromInventory;
            Rect row = new(0, y, width, 65);
            GUI.Box(row, GUIContent.none, active ? theme!.SelectedCard : theme!.Card);
            // The selection hit area must not consume the quick-add button's mouse event.
            if (GUI.Button(new Rect(0, y, fromInventory ? width : width - 48, 65), GUIContent.none, GUIStyle.none)) Select(item, fromInventory);
            DrawIcon(new Rect(9, y + 11, 42, 42), item);
            string name = fromInventory && rowNames.TryGetValue(item, out string finalName) ? finalName : item.name ?? "Unnamed item";
            GUI.Label(new Rect(62, y + 7, width - (fromInventory ? 74 : 112), 25), new GUIContent(name, name), theme.RowTitle);
            GUI.Label(new Rect(62, y + 33, width - 68, 23),
                (fromInventory && equipped.Contains(item) ? "EQUIPPED  /  " : "") + item.itemCategory + "  /  " + item.bulk.ToString("0.##") + " bulk", theme.Muted);
            if (!fromInventory && GUI.Button(new Rect(width - 43, y + 7, 34, 27), new GUIContent("+", "Add one " + item.name)))
            {
                Item template = item;
                pending = () => Add(template, false, 1, false, "", "");
            }
        }
        GUI.EndScrollView();
    }

    private void DrawDetails()
    {
        Panel(new Rect(900, 218, 512, 606), "03  /  INSPECT & EDIT", selectedOwned ? "Selected inventory item" : "Selected template");
        if (selected == null)
        {
            GUI.Label(new Rect(924, 330, 464, 100), "Select an item to inspect it, add copies, or create a custom version.", theme!.Heading);
            GUI.Label(new Rect(924, 446, 464, 90), "The + button in the library adds one item immediately.\n\nEach inventory row is a separate item, including duplicates.", theme.Muted);
            return;
        }
        Item item = selected;
        DrawIcon(new Rect(924, 304, 60, 60), item);
        GUI.Label(new Rect(998, 303, 388, 57), new GUIContent(item.name, item.name), theme!.Heading);
        GUI.Label(new Rect(924, 375, 464, 27), item.itemCategory + "  /  " + item.bulk.ToString("0.##") + " bulk  /  Base value " + item.value, theme.Muted);
        string[] tabs = { "Details", "Custom text", "Condition", "Food / water" };
        for (int i = 0; i < tabs.Length; i++)
            if (GUI.Button(new Rect(924 + i * 118, 413, 110, 36), tabs[i], detailTab == i ? theme.Primary : GUI.skin.button))
            { detailTab = i; detailScroll = Vector2.zero; }

        if (detailTab == 1) DrawCustomText();
        else if (detailTab == 2) DrawCondition();
        else if (detailTab == 3) DrawFoodWater();
        else
        {
            float textHeight = Mathf.Max(225, theme.Skin.label.CalcHeight(new GUIContent(detailText), 440));
            detailScroll = GUI.BeginScrollView(new Rect(924, 460, 464, 233), detailScroll, new Rect(0, 0, 442, textHeight));
            GUI.Label(new Rect(0, 0, 440, textHeight), string.IsNullOrWhiteSpace(detailText) ? "This item has no description." : detailText);
            GUI.EndScrollView();
        }

        GUI.Label(new Rect(924, 704, 170, 25), "Quantity  /  1-100", theme.Muted);
        quantity = GUI.TextField(new Rect(1096, 699, 74, 36), quantity, 3);
        bool validQuantity = int.TryParse(quantity, out int amount) && amount >= 1 && amount <= InventoryService.MaxBatch;
        bool validText = !custom || IsCustomValid();
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && validQuantity && validText;
        string action = selectedOwned ? (custom ? "Add custom copies" : "Duplicate item") : (custom ? "Add custom item" : "Add to inventory");
        if (GUI.Button(new Rect(1180, 699, 208, 36), action, theme.Primary))
        {
            bool fromInventory = selectedOwned, personalized = custom;
            string name = customName, description = customDescription;
            ItemEditDraft? draft = itemDraft;
            pending = () => Add(item, fromInventory, amount, personalized, name, description, draft?.Build());
        }
        GUI.enabled = enabled && selectedOwned;
        if (GUI.Button(new Rect(924, 749, 148, 38), "Remove item", theme.Danger)) pending = () =>
        {
            removed = InventoryService.Remove(player, inventory, item);
            selected = null;
            SetStatus("Removed " + item.name + ". Use Undo last removal to restore it.");
        };
        GUI.enabled = enabled && selectedOwned && (custom || itemDraft?.HasChanges == true) && validText;
        if (GUI.Button(new Rect(1084, 749, 304, 38), "Apply changes to item"))
        {
            string name = customName, description = customDescription;
            bool personalized = custom;
            ItemEditDraft? draft = itemDraft;
            pending = () =>
            {
                ItemEditing.ApplyToInventory(player, inventory, item, draft?.Build() ?? new ItemEditOptions(), personalized, name, description);
                icons.Remove(item);
                Select(item, true);
                SetStatus("Selected item updated. Its equipped state is preserved. Save your game to keep the changes.");
            };
        }
        GUI.enabled = enabled;
        GUI.Label(new Rect(924, 791, 464, 24), selectedOwned ? "Apply affects this item; Duplicate uses your pending edits." : "Pending edits apply to new copies of this template.", theme.Muted);
    }

    private void DrawCustomText()
    {
        custom = GUI.Toggle(new Rect(924, 459, 464, 32), custom, (custom ? "ON   " : "OFF   ") + "Use custom name & description");
        bool enabled = GUI.enabled;
        GUI.enabled = enabled && custom;
        GUI.Label(new Rect(924, 496, 464, 20), "Custom name", theme!.Muted);
        string name = GUI.TextField(new Rect(924, 520, 464, 32), customName);
        if (name != customName)
        {
            customName = GeneratedItemText.Filter(name, true);
            if (name != customName) SetStatus("Names allow letters, digits, spaces and . , ! ? ' - only.");
        }
        GUI.Label(new Rect(924, 557, 464, 22), $"Description   /   {GeneratedItemText.WordCount(customDescription)} of 30 words", theme.Muted);
        string description = GUI.TextArea(new Rect(924, 583, 464, 66), customDescription);
        if (description != customDescription)
        {
            customDescription = GeneratedItemText.LimitDescription(description);
            if (description != customDescription) SetStatus("Description adjusted to the game's plain-text grammar and 30-word limit.");
        }
        GUI.enabled = enabled;
        GUI.Label(new Rect(924, 651, 464, 44), "Names: letters, digits and . , ! ? ' -  (no native length cap).\nDescriptions: same punctuation, letters only, up to 30 words.", theme.Muted);
    }

    private void DrawCondition()
    {
        if (itemDraft == null) return;
        ItemEditDraft draft = itemDraft;
        bool enabled = GUI.enabled;
        GUI.Label(new Rect(924, 458, 464, 24), draft.HasDurability ? $"Durability  /  maximum {draft.MaxDurability:0.##}" : "Durability  /  not used by this item", theme!.Muted);
        GUI.enabled = enabled && draft.HasDurability;
        draft.Durability = GUI.TextField(new Rect(924, 484, 230, 36), draft.Durability, 16);
        if (GUI.Button(new Rect(1164, 484, 108, 36), "Repair")) draft.Durability = draft.MaxDurability.ToString("R", CultureInfo.InvariantCulture);
        if (GUI.Button(new Rect(1282, 484, 106, 36), "Reset")) draft.Durability = draft.OriginalDurability;
        GUI.enabled = enabled;
        GUI.Label(new Rect(924, 527, 464, 24), draft.HasSpoilage ? "Remaining spoilage time  /  game minutes" : "Spoilage  /  this item has no timer", theme.Muted);
        GUI.enabled = enabled && draft.HasSpoilage;
        draft.Minutes = GUI.TextField(new Rect(924, 553, 230, 36), draft.Minutes, 16);
        if (GUI.Button(new Rect(1164, 553, 108, 36), "1 day")) draft.Minutes = "1440";
        if (GUI.Button(new Rect(1282, 553, 106, 36), "Reset")) draft.Minutes = draft.OriginalMinutes;
        GUI.enabled = enabled;
        GUI.Label(new Rect(924, 597, 464, 24), new GUIContent("Enchantment  /  " + draft.CurrentEnchantment, draft.CurrentEnchantment), theme.Muted);
        GUI.enabled = enabled && draft.CanEnchant;
        if (GUI.Button(new Rect(924, 623, 464, 36), draft.CanEnchant ? draft.Enchantment + "   /   Choose enchantment" : "This item cannot be enchanted")) showEnchantments = true;
        GUI.enabled = enabled;
        GUI.Label(new Rect(924, 662, 464, 34), "Spoilage uses half-minute steps; winter lasts twice as long.\nEdits apply only when you add, duplicate, or Apply changes.", theme.Muted);
    }

    private bool IsCustomValid()
    {
        try { GeneratedItemText.Validate(customName.Trim(), customDescription); return true; }
        catch { return false; }
    }

    private void DrawFoodWater()
    {
        if (itemDraft == null) return;
        ItemEditDraft draft = itemDraft;
        if (!draft.IsEdible)
        {
            GUI.Label(new Rect(924, 470, 464, 60), "This item has no food or drink effects.", theme!.Heading);
            GUI.Label(new Rect(924, 540, 464, 70), "Select food or a beverage to change how much hunger and thirst it restores.", theme.Muted);
            return;
        }
        GUI.Label(new Rect(924, 463, 464, 24), "Food value  /  hunger restored", theme!.Muted);
        draft.Hunger = GUI.TextField(new Rect(924, 491, 346, 36), draft.Hunger, 12);
        if (GUI.Button(new Rect(1282, 491, 106, 36), "Reset")) draft.Hunger = draft.OriginalHunger;
        GUI.Label(new Rect(924, 540, 464, 24), "Water value  /  thirst restored", theme.Muted);
        draft.Thirst = GUI.TextField(new Rect(924, 568, 346, 36), draft.Thirst, 12);
        if (GUI.Button(new Rect(1282, 568, 106, 36), "Reset")) draft.Thirst = draft.OriginalThirst;
        GUI.Label(new Rect(924, 619, 464, 73), "Positive values restore; zero has no effect. Negative values increase hunger or thirst.\nWhole numbers from -100,000 to 100,000.\nUse Apply changes, Add, or Duplicate to commit edits.", theme.Muted);
    }

    private void Add(Item item, bool fromInventory, int amount, bool personalized, string name, string description, ItemEditOptions? edits = null)
    {
        int beforeGold = inventory.gold;
        List<Item> copies = InventoryService.Add(player, inventory, item, amount, fromInventory, personalized, name, description, edits);
        Item? first = copies.FirstOrDefault(inventory.ContainsItem);
        // Keep the catalog selected for rapid repeated additions.
        if (fromInventory && first != null) Select(first, true);
        string gold = beforeGold == inventory.gold ? "" : $" Gold changed by {inventory.gold - beforeGold:+#;-#;0}.";
        SetStatus($"Added {amount} x {(personalized ? name.Trim() : item.name)}." + gold);
    }

    private void DrawCategoryPicker()
    {
        Fill(new Rect(-10000, -10000, 20000, 20000), new Color(0, 0, 0, 0.72f));
        GUI.Box(new Rect(404, 219, 632, 456), GUIContent.none);
        GUI.Label(new Rect(428, 240, 440, 35), "Browse by category", theme!.Heading);
        if (GUI.Button(new Rect(904, 236, 108, 36), "Close")) showCategories = false;
        for (int i = 0; i < Categories.Length; i++)
        {
            string entry = Categories[i];
            if (GUI.Button(new Rect(428 + i % 3 * 198, 296 + i / 3 * 68, 186, 50), entry, entry == category ? theme.Primary : GUI.skin.button))
            { category = entry; showCategories = false; FilterCatalog(); }
        }
    }

    private void DrawEnchantmentPicker()
    {
        Fill(new Rect(-10000, -10000, 20000, 20000), new Color(0, 0, 0, 0.72f));
        GUI.Box(new Rect(404, 219, 632, 456), GUIContent.none);
        GUI.Label(new Rect(428, 240, 440, 35), "Choose enchantment", theme!.Heading);
        if (GUI.Button(new Rect(904, 236, 108, 36), "Close")) showEnchantments = false;
        for (int i = 0; i < ItemEditing.Enchantments.Length; i++)
        {
            string entry = ItemEditing.Enchantments[i];
            if (GUI.Button(new Rect(428 + i % 3 * 198, 296 + i / 3 * 68, 186, 50), entry, entry == itemDraft?.Enchantment ? theme.Primary : GUI.skin.button))
            {
                if (itemDraft != null) itemDraft.Enchantment = entry;
                showEnchantments = false;
            }
        }
    }

    private void DrawIcon(Rect rect, Item item)
    {
        if (!icons.TryGetValue(item, out Sprite? sprite))
        {
            try { sprite = item.Sprite; }
            catch { sprite = null; }
            icons[item] = sprite;
        }
        if (sprite != null)
        {
            try
            {
                Rect source = sprite.textureRect;
                float scale = Mathf.Min(rect.width / source.width, rect.height / source.height);
                Rect target = new(rect.center.x - source.width * scale / 2, rect.center.y - source.height * scale / 2, source.width * scale, source.height * scale);
                Texture2D texture = sprite.texture;
                GUI.DrawTextureWithTexCoords(target, texture, new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height));
                return;
            }
            catch { icons[item] = null; }
        }
        GUI.Box(rect, new GUIContent("?", "No icon available"), theme!.Card);
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
