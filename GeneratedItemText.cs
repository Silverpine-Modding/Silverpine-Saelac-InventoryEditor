using System;
using System.IO;
using System.Text;

namespace SilverpineMods.InventoryEditor;

internal static class GeneratedItemText
{
    // Silverpine 1.7.3 NPCFunction_GiveItem.Invoke: the name grammar has no
    // length quantifier; the description prompt says "no more than 30 words".
    // NPCFunction_ServeFood.ModelFoodOutput repeats the same description limit.
    internal const int DescriptionWordLimit = 30;

    internal static bool Allowed(char c, bool name) =>
        c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' ||
        name && c >= '0' && c <= '9' || " .,!?'-".IndexOf(c) >= 0;

    internal static string Filter(string text, bool name)
    {
        var result = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            // Both native generation grammars are single-line; pasted whitespace
            // becomes spaces instead of joining words together.
            if (char.IsWhiteSpace(c)) result.Append(' ');
            else if (Allowed(c, name)) result.Append(c);
        }
        return result.ToString();
    }

    internal static int WordCount(string text)
    {
        int count = 0;
        bool inWord = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) inWord = false;
            else if (!inWord) { count++; inWord = true; }
        }
        return count;
    }

    internal static string LimitDescription(string text)
    {
        string filtered = Filter(text, name: false);
        int count = 0;
        bool inWord = false;
        for (int i = 0; i < filtered.Length; i++)
        {
            if (char.IsWhiteSpace(filtered[i])) inWord = false;
            else if (!inWord)
            {
                if (++count > DescriptionWordLimit) return filtered.Substring(0, i).TrimEnd();
                inWord = true;
            }
        }
        return filtered;
    }

    internal static void Validate(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Custom names cannot be empty.");
        foreach (char c in name)
            if (!Allowed(c, name: true))
                throw new InvalidDataException("Custom names allow A-Z, a-z, 0-9, spaces and . , ! ? ' - only, matching the game's item-name grammar.");
        if (WordCount(description) > DescriptionWordLimit)
            throw new InvalidDataException($"Custom descriptions allow no more than {DescriptionWordLimit} words, matching the game's item-generation prompt.");
        foreach (char c in description)
            if (!Allowed(c, name: false))
                throw new InvalidDataException("Custom descriptions allow A-Z, a-z, spaces and . , ! ? ' - only, matching the game's item-description grammar.");
    }
}
