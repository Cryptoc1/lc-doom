# LC-DOOM

Play DOOM on the ship's terminal in Lethal Company.

## Contributing

I recommend using [Thunderstore](https://thunderstore.io):
- Create a profile for testing (e.g. `lc-doom-dev`)
- Add the `BepInExPack` mod to the profile

Then,

- Fork this repository
- Create a `src\src\LethalCompany.Doom.csproj.user`
- Define required properties:
```xml
<PropertyGroup>
  <PluginStagingProfile>{PROFILE}</PluginStagingProfile>
</PropertyGroup>
```

- Run `dotnet publish`, or "Default Build Task" in VS Code
- Launch Lethal Company using the Thunderstore `{PROFILE}`

## Bug Report & Feature Request

If you've encountered an error, or have a feature request, please open an issue on [GitHub](https://github.com/cryptoc1/lc-doom/issues/new).

When reporting an error, please include as most information as possible, such as any errors or logs from BepInEx, or the version of the game or mod.

## Credits

- [idSoftware](https://www.idsoftware.com)
- [ManagedDoom](https://github.com/sinshu/managed-doom)
- [DoomInUnityInspector](https://github.com/xabblll/DoomInUnityInspector)