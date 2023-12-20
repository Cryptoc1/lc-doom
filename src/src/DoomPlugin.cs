using BepInEx.Logging;

namespace LethalCompany.Doom;

[BepInPlugin(GeneratedPluginInfo.Identifier, GeneratedPluginInfo.Name, GeneratedPluginInfo.Version)]
public sealed class DoomPlugin : BaseUnityPlugin
{
    public static DoomPlugin Value { get; private set; } = default!;

    public new ManualLogSource Logger => base.Logger;

    public void Awake()
    {
        Value = this;
        _ = Harmony.CreateAndPatchAll(
            typeof(DoomPlugin).Assembly,
            GeneratedPluginInfo.Identifier);
    }
}
