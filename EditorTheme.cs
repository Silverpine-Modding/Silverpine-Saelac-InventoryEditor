using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilverpineMods.InventoryEditor;

// Owned by the temporary editor, never applied to another mod's GUI.
internal sealed class EditorTheme : IDisposable
{
    internal static readonly Color Background = Hex(0x121212);
    internal static readonly Color Accent = Hex(0x7BD8C0);
    private static readonly Color TextColor = Hex(0xE4EAF0);
    private static readonly Color MutedColor = Hex(0x94A3B3);
    internal GUISkin Skin { get; }
    internal GUIStyle Title, Eyebrow, Heading, Muted, Badge, Card, SelectedCard, Primary, RowButton, SelectedButton, Status, Danger, RowTitle;
    private readonly List<Texture2D> textures = new();

    internal EditorTheme(GUISkin source)
    {
        Skin = UnityEngine.Object.Instantiate(source);
        Skin.label = new GUIStyle(source.label) { fontSize = 16, wordWrap = true,
            margin = new RectOffset(0, 0, 4, 4), padding = new RectOffset(0, 0, 2, 2) };
        Skin.label.normal.textColor = TextColor;
        Skin.label.richText = false;
        Skin.box = Surface(source.box, Hex(0x1A1A1A), Hex(0x343434), 16);
        Skin.button = Button(source.button, Hex(0x292929), Hex(0x414141), TextColor);
        Skin.textField = Surface(source.textField, Hex(0x131313), Hex(0x414141), 10);
        Skin.textField.fontSize = 16;
        Skin.textField.fixedHeight = 36;
        Skin.textField.alignment = TextAnchor.MiddleLeft;
        Skin.textField.focused.background = Rounded(Hex(0x10251F), Accent);
        Skin.textField.focused.textColor = TextColor;
        Skin.textArea = new GUIStyle(Skin.textField) { fixedHeight = 0, wordWrap = true,
            alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10) };
        Skin.toggle = Button(source.button, Hex(0x222222), Hex(0x414141), MutedColor);
        Skin.toggle.alignment = TextAnchor.MiddleLeft;
        Skin.toggle.onNormal.background = Rounded(Hex(0x18352E), Hex(0x416F61));
        Skin.toggle.onNormal.textColor = Accent;
        Skin.toggle.onHover.background = Rounded(Hex(0x204A3F), Accent);
        Skin.toggle.onHover.textColor = Accent;
        Skin.toggle.onActive = Skin.toggle.onHover;
        Skin.toggle.onFocused = Skin.toggle.onHover;
        Skin.settings.cursorColor = Accent;
        Skin.settings.selectionColor = Hex(0x31594D);
        Skin.verticalScrollbar = Surface(source.verticalScrollbar, Hex(0x181818), Hex(0x181818), 0);
        Skin.verticalScrollbar.fixedWidth = 10;
        Skin.verticalScrollbar.margin = new RectOffset(8, 0, 0, 0);
        Skin.verticalScrollbarThumb = Surface(source.verticalScrollbarThumb, Hex(0x4B4B4B), Hex(0x4B4B4B), 0);
        Skin.verticalScrollbarThumb.hover.background = Rounded(Hex(0x527466), Hex(0x527466));
        Skin.verticalScrollbarUpButton.fixedHeight = Skin.verticalScrollbarDownButton.fixedHeight = 0;
        Skin.horizontalScrollbar.fixedHeight = 0;

        Title = new GUIStyle(Skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
        Eyebrow = new GUIStyle(Skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
        Eyebrow.normal.textColor = Accent;
        Heading = new GUIStyle(Skin.label) { fontSize = 21, fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 0, 8) };
        Muted = new GUIStyle(Skin.label) { fontSize = 14 };
        Muted.normal.textColor = MutedColor;
        Badge = new GUIStyle(Muted) { alignment = TextAnchor.MiddleRight, fixedHeight = 36 };
        Card = Surface(source.box, Hex(0x232323), Hex(0x323232), 10);
        Card.margin = new RectOffset(0, 0, 0, 8);
        SelectedCard = Surface(source.box, Hex(0x142F2A), Hex(0x4B8D7B), 8);
        SelectedCard.margin = Card.margin;
        Primary = Button(source.button, Hex(0x85D8BD), Hex(0x85D8BD), Hex(0x0C241C));
        Primary.hover.background = Rounded(Hex(0xA5EDD5), Hex(0xA5EDD5));
        Primary.hover.textColor = Hex(0x0C241C);
        Primary.active.background = Rounded(Hex(0x69BDA1), Hex(0x69BDA1));
        Primary.active.textColor = Primary.focused.textColor = Hex(0x0C241C);
        RowButton = new GUIStyle(Skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = true,
            fixedHeight = 0, padding = new RectOffset(10, 8, 10, 10) };
        RowButton.normal.background = null;
        SelectedButton = new GUIStyle(RowButton);
        SelectedButton.normal.textColor = Accent;
        // Use the same nine-slice border as the panels. A label's zero border
        // stretches the entire rounded texture into an oval on wide screens.
        Status = Surface(Muted, Hex(0x1A1A1A), Hex(0x343434), 16);
        Status.padding = new RectOffset(16, 16, 12, 12);
        Status.alignment = TextAnchor.MiddleLeft;
        Status.normal.textColor = MutedColor;
        Danger = Button(source.button, Hex(0x3B2525), Hex(0x744545), Hex(0xF0B0AA));
        RowTitle = new GUIStyle(Skin.label) { fontSize = 15, fontStyle = FontStyle.Bold,
            wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
    }

    private GUIStyle Surface(GUIStyle source, Color fill, Color border, int padding)
    {
        var style = new GUIStyle(source) { border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(padding, padding, padding, padding), margin = new RectOffset(0, 0, 3, 3), richText = false };
        style.normal.background = Rounded(fill, border);
        style.normal.textColor = TextColor;
        return style;
    }

    private GUIStyle Button(GUIStyle source, Color fill, Color border, Color text)
    {
        GUIStyle style = Surface(source, fill, border, 8);
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.fixedHeight = 36;
        style.normal.textColor = text;
        style.hover.background = Rounded(fill * 1.25f, Accent * 0.7f);
        style.hover.textColor = TextColor;
        style.active.background = Rounded(Hex(0x234C40), Accent);
        style.active.textColor = Accent;
        style.focused.background = Rounded(fill, Accent);
        style.focused.textColor = text;
        return style;
    }

    private Texture2D Rounded(Color fill, Color border)
    {
        const int size = 24;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Math.Max(Math.Abs(x - 11.5f) - 5.5f, 0);
            float dy = Math.Max(Math.Abs(y - 11.5f) - 5.5f, 0);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            Color color = distance > 5 ? border : fill;
            color.a = Mathf.Clamp01(6.5f - distance);
            pixels[y * size + x] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        textures.Add(texture);
        return texture;
    }

    private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    public void Dispose()
    {
        foreach (Texture2D texture in textures) UnityEngine.Object.Destroy(texture);
        UnityEngine.Object.Destroy(Skin);
    }
}
