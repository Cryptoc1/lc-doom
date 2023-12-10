using BepInEx;
using BepInEx.Logging;

namespace LethalCompany.Doom;

[BepInPlugin(DoomPluginInfo.Identifier, DoomPluginInfo.Name, DoomPluginInfo.Version)]
public sealed class DoomPlugin : BaseUnityPlugin, IDisposable
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

public static class DoomPluginInfo
{
    public const string Identifier = "cryptoc1.lethalcompany.doom";
    public const string Name = "LC-DOOM";
    public const string Version = "0.0.0";
}
