using BepInEx.Logging;

namespace LethalCompany.Doom;

[BepInPlugin(DoomPluginInfo.Identifier, DoomPluginInfo.Name, DoomPluginInfo.Version)]
public sealed partial class DoomPlugin : BaseUnityPlugin, IDisposable
{
    public static DoomPlugin Value { get; private set; } = default!;

    public new ManualLogSource Logger => base.Logger;
    private Harmony? harmony;

    public void Awake()
    {
        Value = this;
        harmony = Harmony.CreateAndPatchAll(
            typeof(DoomPlugin).Assembly,
            DoomPluginInfo.Identifier);
    }

    public void Dispose() => (harmony as IDisposable)?.Dispose();
}
