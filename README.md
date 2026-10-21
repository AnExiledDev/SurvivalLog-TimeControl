# Time Control

A true slow speed, a super speed, a pause that waits for you, and fast sleep.

Survival Log's slow button only slows the game once you stand idle, the clock keeps running while you think, and there is no way to pause before the disaster or on an expedition. Time Control adds a real slow speed and a faster one, pauses the game while your character has nothing to do, fast-forwards sleep, and gives you a pause key that works everywhere you play.

## Features

- **A slow button that really slows**: the game's slow button runs at full speed until you stand idle, then drops to 0.3x. With this mod it holds a true slow speed from the moment you press it: 0.5x on the first press, 0.25x on the next, then back to 0.5x. The button's tooltip, and a small chip on its top right corner, show the speed in use.
- **Super speed**: press the Fastest button (or key `3`) again while at Fastest and the game runs at twice that speed. Press it once more to go back to Fastest. A small chip on the top right corner of the button shows the speed the game really runs at, such as 6x. Pausing and resuming comes back to super speed; keys `1` and `2` and the slow button leave it.
- **Idle pause**: once your character has stood idle with nothing queued for 3 seconds, the game pauses for real. The clock, zombies, plants, trade countdowns and the drone all stop. Your next order resumes the speed you had. Press play (or the pause key) while it is paused and time keeps running until your next order. A pause you made yourself is never undone by the mod.
- **Fast sleep, and a pause on waking**: sleep and naps run at super speed in place of the game's own sleep speed-up. When your character wakes, however the sleep ended, the game pauses. Your next order, play or the pause key resumes the speed you had before the sleep. Pick another speed during a sleep and it is kept until you wake.
- **Pause on alerts**: the game pauses once when a zombie wave starts or an emergency fires, so neither slips past at super speed. Pausing when a trade drone comes home, or when your character finishes the last order in their queue, can be switched on as well. Play or the pause key resumes the speed you had, and the same alert never pauses the game twice, so once you resume, time keeps running.
- **A pause key**: `Space` pauses, and pressing it again resumes the speed you had. It works at home, in the countdown before the disaster and on expeditions. The game had `Space` on centering the camera on your character, so that moves to `G`.
- **Pausing before the disaster**: the game ignores its own speed keys in the countdown before the disaster. The mod answers them there, so you can pause, slow down and speed up while you prepare. The idle pause works there too.
- **Time control on expeditions**: the game also ignores its speed keys on an expedition. With this mod the pause key, the game's speed keys, the slow and super speeds and the idle pause all work there. The day clock stands still while you are away and the exposure meter fills on game time, so pausing stops the meter and super speed fills it faster.
- **Speed buttons before the disaster and on expeditions**: the game shows no speed buttons in the countdown before the disaster or on an expedition. The mod adds its own there, in the game's style: slow, normal, fast, fastest and pause. They work like the game's buttons at home, with the mod's slow and super speeds. In the countdown they sit to the right of the buttons in the bottom left corner; on an expedition they sit where the game's own buttons sit at home. On expeditions they only show when `TimeControlOnExpeditions` is `true`. Set `SpeedButtons` to `false` to hide them.
- **A pause badge**: while the idle pause holds the game, a small "Paused: idle" chip sits just to the right of the speed buttons, in the game's own chip style. It sits beside the buttons, so it no longer covers the suggested actions above them. It is see-through and clicks go straight through it. While a pause you made with the pause key holds it (or with the game's own pause key, before the disaster and on expeditions), the chip says "Paused". The game's own pause at home after the disaster gets no chip; the game lights its pause button there. It goes away the moment time runs again. Where the screen shows no speed buttons, the chip sits at the top centre, except in the countdown before the disaster, where it only shows beside the mod's speed buttons.
- **A see-through "paused" banner**: the game's own paused banner across the lower middle of the screen is drawn half see-through, so the phone's reply choices under it stay readable. Clicks go through it to whatever is underneath. Set `PauseOverlayOpacity` to `1` to keep the game's banner as it is, or to `0` to hide it.
- **Hover highlights while paused**: objects highlight under the mouse while the game is paused or slowed, as they do at normal speed.
- **A time log**: what the mod did (idle pauses, alert pauses, speed changes, sleep and wake) is written to `BepInEx\time-control.log`.

Time Control adds no data of its own to your save and does not edit game files. It does change how fast game time runs, and that time is saved with your game: a day played slower or paused is still a normal day to the save. You can remove the mod at any time; the game goes back to its own speeds and keys at the next start. If centering the camera is still on `G` after you remove it, set it back in the game's key settings.

| Key | What it does |
| --- | --- |
| `Space` | Pause. Press again to resume the speed you had. Works at home, in the countdown before the disaster and on expeditions. The game uses `Space` for camera follow, so that moves to `G`. Change it with `PauseToggleKey`. |
| `G` | Camera follow: centers the camera on your character. The game has this on `Space`, so the mod moves it to `G`, which the game does not use by default. Change it with `CenterCameraKey`. |
| `1`, `2`, `3` | The game's own speed keys. Press `3` again at Fastest for super speed. In the countdown before the disaster and on expeditions the mod answers them, since the game does not. |
| `4`, `` ` `` (`~`) | The game's own pause and slow keys (or whatever you bound them to in the game's key settings). The mod answers them in the countdown before the disaster and on expeditions. |

A key you set in the game's own key settings always wins. If the game uses your pause key for one of its own actions, the pause key stands aside and the log says so. The pause key does nothing while you type in a text box or rebind a key.

## How it works

**Idle** is the game's own test: no order from you in your character's queue, the same test the game's idle slowdown uses. Something your character does on its own does not count as an order, so the game can pause during it.

**Super speed** is a multiple of the game's Fastest speed (2x by default, set by `FastSpeeds`). Speeds are capped at 40x: the game moves its world one step per frame, and past 40x each step at 30 frames a second is longer than the game's own speeds allow.

**The pause badge** shows only for pauses the mod knows the reason for:

- The idle pause shows "Paused: idle".
- A pause on an alert names the alert: "Paused: wave", "Paused: emergency", "Paused: drone back" or "Paused: queue empty".
- A pause made with `PauseToggleKey` anywhere shows "Paused".
- A pause made with the game's own pause key shows "Paused" in the countdown before the disaster and on expeditions, where the mod answers that key.
- The game's own pause button gets no badge, and neither does the game's pause key at home after the disaster, where the game answers it itself and lights its own pause button.

**Alerts** each pause the game once, at the moment they start:

- A zombie wave pauses when it starts, not again while it lasts. The next wave pauses again.
- An emergency pauses when it fires. One that is already running does not pause again when the game shows it again.
- A trade drone pauses when its trip ends and it is back home, whether or not its cargo could be unloaded.
- The order queue pauses when your character finishes their last order, the same test as the idle pause, but at once and without resuming on your next order. Cancelling your last order counts too.

Alerts pause only where the idle pause can: at home, in the countdown before the disaster and on expeditions, and not while your character sleeps or the game fast-forwards. An alert that starts while the game is already paused, or where alerts do not pause, is let go and does not pause the game later. On expeditions alerts only pause when `TimeControlOnExpeditions` is `true`. Loading a save in the middle of a wave does not pause it, and nothing that happens in the first second after loading does either. If two alerts start together, the game pauses once and the badge names the most pressing one: a wave, then an emergency, then a drone, then the queue.

**Expeditions**: the day clock stands still while you are away and the exposure meter does the counting. The mod controls the expedition's game time, so the meter stops while paused and fills faster at super speed. Set `TimeControlOnExpeditions` to `false` to leave expeditions at the game's own pace.

## Known issues and limits

- On an expedition the game's exposure alarm keeps sounding while the game is paused, because the game plays it on real time. The meter itself stops.
- The badge can blink out for up to half a second when the game redraws its speed buttons, then comes back.
- Where the screen shows no speed buttons, the badge sits at the top centre and may cover part of the game's display there.
- In the countdown before the disaster the badge only shows beside the mod's speed buttons. With `SpeedButtons` set to `false` there is no badge in the countdown.
- Super speed moves the world in bigger steps per frame, so fights and walking are coarser than at the game's own speeds. A low frame rate caps how fast it really runs.
- While `FastForwardSleep` is on, the game's own sleep speed-up never starts, so anything the game shows only during its speed-up does not show.
- An order you queued before waking does not resume the game after the wake pause. Give a new order, press play or press the pause key.
- Pressing the game's own pause button while the idle pause holds the game does not make the pause yours: your next order still resumes. Press play first, then pause, to keep it paused.
- The idle pause works at home, in the countdown before the disaster and on expeditions. Any other timer keeps the game's pace.
- A click on those speed buttons can take up to a tenth of a second to change the speed.
- When a pause starts, the paused banner can show at full strength for up to half a second before it turns see-through.
- In the countdown before the disaster that screen has no text for the speed buttons, so the mod names them itself, in English or Chinese to match the game, without the hotkey. They hide while the planning view is open, along with the game's buttons in the bottom left corner.
- An alert that starts while your character sleeps does not pause the game. `PauseOnWake` pauses it when they wake.
- A line in the log reading `speed change reached N time managers in one frame (expected 1)` means leftover game worlds are answering the game's events, so one trade or one order can happen several times. Restart the game if you see it. `speed change reached 1 time manager, as it should` after your first use of the speed buttons is the healthy reading.

Works in English and Chinese: the pause badge and the speed buttons follow the game's language, switching when you switch it, and every config setting is explained in English and Simplified Chinese. The log stays in English.

## Requirements

- Survival Log 1.1.19131. Built for this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Optional: [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game. Without it, they take effect at the next game start.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Optional, to change settings without a restart: install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from Nexus Mods, and extract it into the game folder (the folder with `SurvivalLog.exe`).
3. Download `TimeControl-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-TimeControl/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/47).
4. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\TimeControl.dll`

## Uninstall

Delete `TimeControl.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.timecontrol.cfg` and `BepInEx\time-control.log`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.timecontrol.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Changed on the Mods screen, every setting takes effect at once except `SlowSpeeds`, `FastSpeeds`, `FastForwardSleep`, `PauseToggleKey`, `CenterCameraKey` and `TimeControlOnExpeditions`, which take effect at the next game start. See [CONFIG.md](CONFIG.md) for every setting. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== Time Control 0.8.1 started`. It says whether sleep fast-forward and expeditions are on, and the line after it lists the other features; that line is written again when a setting changes on the Mods screen. The next lines say which game hooks are in place and what the game's own pause key is; a line starting `could not hook` names a feature that will not work.

The same lines, plus every idle pause, alert pause, sleep and wake, are in `BepInEx\time-control.log`, which survives restarts and trims itself past 1 MB.

If `Space` does not pause, look in the log for a line saying it is bound to one of the game's own actions, and rebind that action in the game's key settings or pick another `PauseToggleKey`.

## Changelog

- 0.7.0: The `ClockRate` setting is gone. For longer days, use the Day Length mod.
- 0.6.3: The pause badge, and the speed button names in the countdown before the disaster, now show in Chinese when the game is in Chinese.
- 0.6.2: Plainer descriptions for every setting in the config file, each with a Simplified Chinese translation. Nothing else changed.
- 0.6.1: Finishing an expedition no longer freezes the game when it was paused. Any pause (idle pause, pause key, speed buttons or an alert) is lifted for the trip home, and pausing does nothing until you are back.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also post in the Posts tab of the mod's Nexus Mods page. When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\time-control.log`.

To reach me directly, message `exiled.dev` on Discord; you can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

A Survival Log wiki with detailed documentation for all of my mods is coming soon.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/TimeControl -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\TimeControl\bin\Release\TimeControl.dll`.

The unit tests cover the pure timing rules and need nothing from the game:

```
dotnet test tests/TimeControl.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
