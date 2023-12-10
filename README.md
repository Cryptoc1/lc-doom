# LC-DOOM

Play DOOM on the ship's terminal in Lethal Company.

## Contributing

I recommend using [Thunderstore](https://thunderstore.io):
- Create a profile for testing (e.g. 'lc-doom-dev')
- Add the `BepInExPack` mod

Then,

- Clone this repository
- Create `src\src\LethalCompany.Doom.csproj.user`
- Define required properties:
```xml
<PropertyGroup>
    <GameManagedDir>{PATH TO 'Lethal Company\Lethal Company_Data\Managed'}</GameManagedDir>
    <PluginPublishDir>{PATH TO 'BepInEx\plugins\LC-DOOM'}</PluginPublishDir>
</PropertyGroup>
```

- Run `dotnet publish -p:PublishPlugin=true`, or "Default Build Task" in VS Code
- Launch Lethal Company via testing profile in Thunderstore

## Credits

- [ManagedDoom](https://github.com/sinshu/managed-doom)
- [DoomInUnityInspector](https://github.com/xabblll/DoomInUnityInspector)