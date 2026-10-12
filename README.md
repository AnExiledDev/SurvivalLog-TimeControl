# Time Control

A BepInEx 6 (IL2CPP) plugin for Survival Log that gives finer control over time:

- **A slow button that really slows.** The game's slow button runs at full speed until you stand idle, then drops to 0.3x. With this mod it holds a true slow speed from the moment you press it: 0.5x on the first press, 0.25x on the next, then back to 0.5x. The button's tooltip shows the speed in use.
- **A super speed.** Press the fast button (or key 3) again while at the game's high speed and it goes to twice that speed; once more goes back to high. The fast button stays lit and keeps the game's own tooltip. Pausing and resuming, by Space or idle freeze, comes back to super speed; keys 1 and 2 and the slow button leave it.
- **Idle freeze.** At home and in the countdown before the disaster, the game pauses for real once your character has stood idle with nothing queued for 3 seconds: the clock, zombies, plants, trade countdowns and the drone all stop. Your next order resumes it at the speed you had. Press play (or the pause key) while it is paused and time keeps running until your next order; a pause you made yourself is never undone by the mod.
- **Fast sleep, and a pause on waking.** Sleep and naps run at super speed in place of the game's own sleep speed-up, and the game pauses when your character wakes, however the sleep ended. Your next order, play or Space resumes the speed you had before the sleep. Pick another speed during a sleep and it is kept until you wake.
- **A slower day.** `ClockRate` slows (or speeds) only the day clock, so a day lasts longer while walking and actions keep their speed.
- **A pause toggle key.** Space pauses; pressing it again resumes the speed you had. It works in the countdown before the disaster too, where the mod also answers the game's own speed keys. The game had Space on centering the camera on your character, so that moves to G; a key you set in the game's own key settings still wins, and the pause key stays out of the way of any key the game uses.
- **Hover highlights while paused.** Objects highlight under the mouse while the game is paused or slowed, as they do at normal speed.

## Config

`BepInEx/config/com.dev.survivallog.timecontrol.cfg`, written on the first launch:

- `SlowButton/SlowSpeeds` (default `0.5,0.25`): speeds the slow button holds, each between 0.05 and 0.95. Empty leaves the game's slow button alone.
- `Clock/ClockRate` (default `1`): day clock pace, 0.05 to 4. `0.5` makes each day last twice as long.
- `FastButton/FastSpeeds` (default `2`): faster speeds above the game's high speed, as multiples of it, each between 1.1 and 10. The speed held never goes past 40x, since the game moves its world one step per frame and past that each step at 30 frames a second is longer than its own speeds allow. Empty leaves the game's fast button alone.
- `Clock/IdleFreeze` (default `true`) and `Clock/IdleFreezeDelaySeconds` (default `3`): pause the game after this many real seconds of idling.
- `Sleep/FastForwardSleep` (default `true`): run sleep and naps at the fastest `FastSpeeds` speed. Off leaves the game's own sleep speed-up as it is.
- `Sleep/PauseOnWake` (default `true`): pause the game when your character wakes.
- `Keys/PauseToggleKey` (default `Space`): any Unity key name, or `None`.
- `Keys/CenterCameraKey` (default `G`): the game's default key for centering the camera on your character. `None` leaves it with no key.

What it did is logged to `BepInEx/time-control.log`. A line reading `speed change reached N time managers in one frame (expected 1)` means leftover game worlds are answering the game's events; one trade or one order can then happen several times. Restart the game if you see it. The line counts only the game's own speed changes and only time managers, so its absence is a good sign rather than proof; `speed change reached 1 time manager, as it should` after your first use of the speed buttons is the healthy reading.

## Known limits

- Idle freeze acts at home and in the countdown before the disaster, and `ClockRate` at home only; exploration keeps the game's pace.
- Super speed moves the world in bigger steps per frame, so fights and walking are coarser than at the game's own speeds, and a low frame rate caps how fast it really runs.
- While `FastForwardSleep` is on, the game's own sleep speed-up never starts, so anything the game shows only during its speed-up does not show.
- An order you queued before waking does not resume the game after the wake pause; give a new order, press play or press Space.
- `ClockRate` slows only the day clock. Idle freeze is a real pause, the same as the game's pause button.
- "Idle" is the game's own test (no order from you in your character's queue), the same one the game's idle slowdown uses. Something your character does on its own does not count as an order, so the game can pause during it.
- Pressing the game's own pause button while idle freeze holds the game does not make the pause yours: your next order still resumes. Press play first, then pause, to keep it paused.

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
