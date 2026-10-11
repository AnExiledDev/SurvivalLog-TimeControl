# Time Control

A BepInEx 6 (IL2CPP) plugin for Survival Log that gives finer control over time:

- **A slow button that really slows.** The game's slow button runs at full speed until you stand idle, then drops to 0.3x. With this mod it holds a true slow speed from the moment you press it: 0.5x on the first press, 0.25x on the next, then back to 0.5x. The button's tooltip shows the speed in use.
- **Idle freeze.** At home, the game pauses for real while your character stands idle with nothing queued: the clock, zombies, plants, trade countdowns and the drone all stop. Your next order resumes it at the speed you had. Press play (or the pause key) while it is paused and time keeps running until your next order; a pause you made yourself is never undone by the mod.
- **A slower day.** `ClockRate` slows (or speeds) only the day clock, so a day lasts longer while walking and actions keep their speed.
- **A pause toggle key.** Space pauses; pressing it again resumes the speed you had. It stays out of the way if the game already uses the key.

## Config

`BepInEx/config/com.dev.survivallog.timecontrol.cfg`, written on the first launch:

- `SlowButton/SlowSpeeds` (default `0.5,0.25`): speeds the slow button holds, each between 0.05 and 0.95. Empty leaves the game's slow button alone.
- `Clock/ClockRate` (default `1`): day clock pace, 0.05 to 4. `0.5` makes each day last twice as long.
- `Clock/IdleFreeze` (default `true`) and `Clock/IdleFreezeDelaySeconds` (default `1`): pause the game after this many real seconds of idling.
- `Keys/PauseToggleKey` (default `Space`): any Unity key name, or `None`.

What it did is logged to `BepInEx/time-control.log`. A line reading `speed change reached N time managers in one frame (expected 1)` means leftover game worlds are answering the game's events; one trade or one order can then happen several times. Restart the game if you see it.

## Known limits

- Idle freeze and `ClockRate` act at home only; exploration keeps the game's pace.
- `ClockRate` slows only the day clock. Idle freeze is a real pause, the same as the game's pause button.
- "Idle" is the game's own test (no order from you in your character's queue), the same one the game's idle slowdown uses. Something your character does on its own does not count as an order, so the game can pause during it.

## Install

Download `TimeControl.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-TimeControl/releases/latest) and copy it into `BepInEx/plugins`. Needs BepInEx 6 (IL2CPP build).

## Build and test

```sh
dotnet build src/TimeControl -c Release -p:GameDir="D:\path\to\Survival Log"
dotnet test tests/TimeControl.Tests
```

The build references BepInEx and the game's interop assemblies under `GameDir`, so BepInEx must be installed and the game launched once. The tests compile only the pure `Core` code and need nothing from the game.

## License

MIT, see [LICENSE](LICENSE).
