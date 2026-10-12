using BepInEx;
using BepInEx.Logging;
using Silverpine.ModdingTools;

namespace SilverpineMods.InventoryEditor;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(Silverpine.ModdingTools.Plugin.PluginGuid, "1.10.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "Saelac.Silverpine.InventoryEditor";
    public const string PluginName = "Inventory Editor";
    public const string PluginVersion = "1.2.0";
    internal static ManualLogSource Log = null!;

    private void Awake()
    {
        Log = Logger;
        InventoryModTools.RegisterSession(PluginGuid + ".editor", PluginName,
            (_, session) => InventoryEditorWindow.Open(session), order: 320);
        Log.LogInfo($"{PluginName} {PluginVersion}: open Inventory > Mods > Inventory Editor.");
    }

    // The static registration survives the game's destruction of the bootstrap plugin host.
}
