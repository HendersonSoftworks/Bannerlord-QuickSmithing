# QuickSmithing

A mod for Mount & Blade II: Bannerlord that speeds up refining at the smithy.

- Click the refine button normally to refine once.
- Hold **Shift** while clicking to refine up to 10 at once.
- Hold **Ctrl** while clicking to refine all available materials at once.

The mod uses the same materials, stamina, skill XP, and events as normal refining, so bulk refining behaves like repeating the action.

## Requirements

- Mount & Blade II: Bannerlord (built against v1.3.4)
- [Bannerlord.Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006)

## Building

Update `DriveLetter` and `GameFolder` in `QuickSmithing.csproj` if Bannerlord is installed somewhere else, then run:

```sh
dotnet build -c Release
```

The compiled module is copied to `QuickSmithing/`, which can be dropped into the game's `Modules` folder.

If the module is not copied to `Modules`, manually copy and paste the `Quicksmithing` folder over into `Modules`, the same way you install any other mod.

*Made with OpenCode*
