# Time Control

Slower and faster speeds, a pause key that works everywhere, and a game that pauses itself while your character has nothing to do.

Survival Log's slow button only slows the game once you stand idle, the clock keeps running while you think, and there is no way to pause before the disaster or on an expedition. Time Control adds slower speeds and a faster one, pauses the game while your character has nothing to do, fast-forwards sleep, and gives you a pause key that works everywhere you play.

## Features

- **Slower speeds on the slow button**: the game's slow speed runs at full speed until you stand idle, then drops to 0.3x. The first press of the slow button still gives you the game's slow speed. Press it again for a true 0.5x, again for 0.25x, and once more to go back to the game's slow speed. While one of the mod's slow speeds is on, the button's tooltip and a small chip on its top right corner show the speed.
- **Super speed**: press the Fastest button (or key `3`) again while at Fastest and the game runs at twice that speed. Press it once more to go back to Fastest. A small chip on the top right corner of the button shows the speed the game really runs at, such as 6x. If you pause and resume, you're back at super speed. Keys `1` and `2` and the slow button take you out of it.
- **Idle pause**: once your character has stood idle with nothing queued for 3 seconds, the game pauses for real. The clock, zombies, plants, trade countdowns and the drone all stop. Your next order resumes the speed you had. Press play (or the pause key) while it is paused and time keeps running until your next order. The mod never undoes a pause you made yourself.
- **Fast sleep, and a pause on waking**: sleep and naps run at super speed in place of the game's sleep speed-up. When your character wakes, however the sleep ended, the game pauses. Your next order, play or the pause key resumes the speed you had before the sleep. If you pick another speed during a sleep, it stays until you wake.
- **Pause on alerts**: the game pauses once when a zombie wave starts or an emergency fires, so neither slips past at super speed. You can switch on more: a trade drone coming home, zombies damaging the house or a trap in a wave, the power going out, a need running low, falling ill, a phone or trade message, a crop ripening, and dusk (19:00) or dawn (7:00). Play or the pause key resumes the speed you had. The same alert never pauses the game twice, so once you resume, time keeps running.
- **When the queue runs out** (off by default): `OnQueueEmpty` can slow the game down (to normal speed by default) or pause it when your character finishes their last order. If it slowed the game down, your next order brings your speed back.
- **Pause while you are away**: the game pauses when you Alt+Tab out and brings your speed back when you return. It also pauses while a menu is open: the bag, workbench, phone, trade, cooking, a shop, the map and the rest, each with its own setting. An order or a craft started in a menu runs until it is done, then the game pauses again. Starting a machine from a menu, such as cooking or brewing, runs the game until you close the menu.
- **Speed control in every menu**: the game stops time by itself while the workbench, bag, a shop or most other menus are open, and ignores the speed keys there. The mod removes that stop, so you control the speed in any menu. Story, ending and settlement screens still stop time.
- **Hold to speed up or slow down**: hold `X` for the fastest speed or `K` for the slowest slow speed. Let go and the speed you had comes back, pause included. Keys `5` to `9` can each hold a speed of your own (`SpeedKeys5To9`), and the mouse wheel over the speed buttons steps through every speed.
- **Wait until** (off by default, `WaitButton`): a clock button beside the speed buttons runs the game at top speed until morning, your alarm time, a brew being done, the queue being done or a few hours later, then pauses. The same menu sets a one-time "Pause in" alarm.
- **An alarm clock**: `AlarmTime` pauses the game at a time of day, every day or once.
- **Sleep until**: `WakeAtHour` wakes your character from sleep at a time you pick, and `WakeOnAlert` wakes them when an alert pauses the game. Naps are never cut short. If your character slept at least 3 hours, they still get Vim and Vigor when they wake.
- **Start paused**: `StartPaused` holds the game paused when a save loads.
- **Clock notes**: at home, the clock shows when your current order will be done and how long until night and the next wave.
- **A pause key**: `Space` pauses, and pressing it again resumes the speed you had. It works at home, in the countdown before the disaster and on expeditions. The game uses `Space` to center the camera on your character, so the mod moves that to `G`.
- **Pausing before the disaster**: the game ignores its speed keys in the countdown before the disaster. The mod makes them work there, so you can pause, slow down and speed up while you prepare. The idle pause works there too.
- **Time control on expeditions**: the game also ignores its speed keys on an expedition. With this mod the pause key, the game's speed keys, the slow and super speeds and the idle pause all work there. The day clock stands still while you are away and the exposure meter fills on game time, so pausing stops the meter and super speed fills it faster.
- **Speed buttons before the disaster and on expeditions**: the game shows no speed buttons in the countdown before the disaster or on an expedition. The mod adds its own there, in the game's style: slow, normal, fast, fastest and pause. They work like the game's buttons at home, with the mod's slow and super speeds. In the countdown they sit to the right of the buttons in the bottom left corner. On an expedition they sit where the game's buttons sit at home, and only show when `TimeControlOnExpeditions` is `true`. Set `SpeedButtons` to `false` to hide them.
- **A pause badge**: while the idle pause holds the game, a small "Paused: idle" chip sits just to the right of the speed buttons, in the game's chip style, so it doesn't cover the suggested actions above them. It is see-through and clicks go straight through it. While a pause you made with the pause key holds the game (or with the game's pause key, before the disaster and on expeditions), the chip says "Paused". A pause made with the game's pause button or key at home after the disaster gets no chip, because the game already lights its pause button there. The chip goes away the moment time runs again. Where the screen shows no speed buttons, the chip sits at the top centre. The one exception is the countdown before the disaster, where it only shows beside the mod's speed buttons.
- **A see-through "paused" banner**: the game's paused banner across the lower middle of the screen is drawn half see-through, so the phone's reply choices under it stay readable. Clicks go through it to whatever is underneath. Set `PauseOverlayOpacity` to `1` to keep the game's banner as it is, or to `0` to hide it.
- **Hover highlights while paused**: objects highlight under the mouse while the game is paused or slowed, as they do at normal speed.
- **A time log**: what the mod did (idle pauses, alert pauses, speed changes, sleep and wake) is written to `BepInEx\time-control.log`.

Time Control adds no data of its own to your save and does not edit game files. It does change how fast game time runs, and that time is saved with your game, so a day played slower or paused is still a normal day to the save. You can remove the mod at any time, and the game goes back to its normal speeds and keys at the next start. If centering the camera is still on `G` after you remove it, set it back in the game's key settings.

| Key | What it does |
| --- | --- |
| `Space` | Pause. Press again to resume the speed you had. Works at home, in the countdown before the disaster and on expeditions. The game uses `Space` for camera follow, so the mod moves that to `G`. Change it with `PauseToggleKey`. |
| `G` | Camera follow: centers the camera on your character. The game has this on `Space`, so the mod moves it to `G`, which the game does not use by default. Change it with `CenterCameraKey`. |
| `1`, `2`, `3` | The game's speed keys. Press `3` again at Fastest for super speed. The mod makes them work in the countdown before the disaster and on expeditions, where the game ignores them. |
| `X` | Hold for the fastest speed. Let go to get your speed back. Change it with `HoldFastKey`. |
| `Shift` | The game's hold to speed up. Set how fast it runs with `GameHoldKeySpeed` (the game's Fastest by default). |
| `K` | Hold for the slowest slow speed. Let go to get your speed back. Change it with `HoldSlowKey`. |
| `5` to `9` | Off by default. Each runs at a speed you set in `SpeedKeys5To9`. |
| Mouse wheel | Over the speed buttons, steps through every speed. Turn it off with `SpeedWheel`. |
| `4`, `` ` `` (`~`) | The game's pause and slow keys (or whatever you bound them to in the game's key settings). The mod makes them work in the countdown before the disaster and on expeditions. |

A key you set in the game's key settings always wins. If the game uses your pause key for one of its actions, the pause key does nothing and the log says so. The pause key also does nothing while you type in a text box or rebind a key.

## How it works

**Idle** means there's no order from you in your character's queue, the same check the game uses for its idle slowdown. Something your character does on their own isn't an order, so the game can pause during it.

**Super speed** is a multiple of the game's Fastest speed (2x by default, set by `FastSpeeds`). Speeds are capped at 40x, because the game moves its world one step per frame, and past 40x each step at 30 frames a second is longer than the game's speeds allow.

**The pause badge** shows only for pauses the mod knows the reason for:

- The idle pause shows "Paused: idle".
- A pause on an alert names the alert, such as "Paused: wave", "Paused: emergency" or "Paused: queue empty".
- A pause while you are away shows that the window is in the background or a menu is open.
- A pause made with `PauseToggleKey` anywhere shows "Paused".
- A pause made with the game's pause key shows "Paused" in the countdown before the disaster and on expeditions, where the mod handles that key.
- The game's pause button gets no badge. Neither does the game's pause key at home after the disaster, because the game handles it there and lights its pause button.

**Alerts** each pause the game once, at the moment they start:

- A zombie wave pauses the game when it starts, and only once per wave. The next wave pauses it again.
- An emergency pauses the game when it fires. If the game shows a running emergency again, it doesn't pause again.
- A trade drone pauses the game when its trip ends and it is back home, whether or not its cargo could be unloaded.
- The order queue (with `OnQueueEmpty` set to `Pause`) pauses the game when your character finishes their last order. It uses the same check as the idle pause, but pauses right away and doesn't resume on your next order. Cancelling your last order counts too.

Alerts pause only where the idle pause can: at home, in the countdown before the disaster and on expeditions, and not while your character sleeps or the game fast-forwards. An alert that starts while the game is already paused, or where alerts can't pause, is skipped and won't pause the game later. On expeditions alerts only pause when `TimeControlOnExpeditions` is `true`. Loading a save in the middle of a wave doesn't pause the game, and nothing in the first second after loading does either. If two alerts start together, the game pauses once and the badge names the most urgent one: a wave, then an emergency, then a drone, then the queue.

**Expeditions**: the day clock stands still while you are away and the exposure meter does the counting. The mod controls the expedition's game time, so the meter stops while paused and fills faster at super speed. Set `TimeControlOnExpeditions` to `false` to leave expeditions at the game's pace.

## Known issues and limits

- On an expedition the game's exposure alarm keeps sounding while the game is paused, because the game plays it on real time. The meter itself stops.
- The badge can blink out for up to half a second when the game redraws its speed buttons, then comes back.
- Where the screen shows no speed buttons, the badge sits at the top centre and may cover part of the game's display there.
- In the countdown before the disaster the badge only shows beside the mod's speed buttons. With `SpeedButtons` set to `false` there is no badge in the countdown.
- Super speed moves the world in bigger steps per frame, so fights and walking are coarser than at the game's speeds. A low frame rate caps how fast it really runs.
- While `FastForwardSleep` is on, the game's sleep speed-up never starts, so anything the game shows only during its speed-up does not show.
- An order you queued before waking does not resume the game after the wake pause. Give a new order, press play or press the pause key.
- Pressing the game's pause button while the idle pause holds the game does not make the pause yours, so your next order still resumes. Press play first, then pause, to keep it paused.
- The idle pause works at home, in the countdown before the disaster and on expeditions. Any other timer keeps the game's pace.
- A click on the mod's speed buttons can take up to a tenth of a second to change the speed.
- When a pause starts, the paused banner can show at full strength for up to half a second before it turns see-through.
- The countdown before the disaster has no text for the speed buttons, so the mod names them itself, in English or Chinese to match the game, without the hotkey. The mod's buttons hide while the planning view is open, along with the game's buttons in the bottom left corner.
- An alert that starts while your character sleeps does not pause the game, unless `WakeOnAlert` is on. `PauseOnWake` pauses it when they wake.
- After sleep, a speed from `SpeedKeys5To9` comes back as the nearest speed button's speed. Speeds from those keys show no chip on the buttons.
- The mouse wheel does nothing while paused.
- If you use a hold key during an alert pause, the game pauses again when you let go, but the alert's badge doesn't come back.
- The order finish time covers the current order only. The disaster countdown is not repeated under the clock, since the game already shows it.
- Brew done in the Wait until menu works at home only.
- A line in the log reading `speed change reached N time managers in one frame (expected 1)` means old copies of the game world are still reacting to the game's events, so one trade or one order can happen several times. Restart the game if you see it. `speed change reached 1 time manager, as it should` after your first use of the speed buttons is normal.

Works in English and Chinese. The pause badge and the speed buttons follow the game's language and switch when you switch it, and every config setting is explained in English and Simplified Chinese. The log stays in English.

## Requirements

- Survival Log 1.1.20719. A game update can break the mod until it is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.20.0 or newer. Time Control does not load without it. It applies settings changed on the Mod Manager's Mods screen without restarting the game, and keeps the speed and pause keys from doing anything while you type in another mod's window.
- Optional: [Mod Manager](https://www.nexusmods.com/survivallog/mods/73). With it, you can change every setting in game on its Mods screen and see what each one does, without opening the config file.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61), and extract it into the game folder (the folder with `SurvivalLog.exe`).
3. Download `TimeControl-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-TimeControl/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/47).
4. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\TimeControl.dll`

## Uninstall

Delete `TimeControl.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.timecontrol.cfg`, `BepInEx\time-control.log` and `BepInEx\time-control.log.1`.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.timecontrol.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Settings you change on the Mods screen take effect right away, except `SlowSpeeds`, `FastSpeeds`, `FastForwardSleep`, `PauseToggleKey`, `CenterCameraKey` and `TimeControlOnExpeditions`, which take effect the next time you start the game. See [CONFIG.md](CONFIG.md) for every setting.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== Time Control 0.10.9 started`. It says whether sleep fast-forward and expeditions are on, and the line after it lists the other features. That line is written again when a setting changes on the Mods screen. The next lines say which game hooks are in place and what the game's pause key is. A line starting `could not hook` names a feature that will not work.

The same lines, plus every idle pause, alert pause, sleep and wake, are in `BepInEx\time-control.log`, which survives restarts. Past 1 MB it is moved to `time-control.log.1` and a new one starts.

If `Space` does not pause, look in the log for a line saying it is bound to one of the game's actions, and rebind that action in the game's key settings or pick another `PauseToggleKey`.

## Changelog

- 0.7.0: The `ClockRate` setting is gone. For longer days, use the Day Length mod.
- 0.6.3: The pause badge, and the speed button names in the countdown before the disaster, now show in Chinese when the game is in Chinese.
- 0.6.2: Plainer descriptions for every setting in the config file, each with a Simplified Chinese translation. Nothing else changed.
- 0.6.1: Finishing an expedition no longer freezes the game when it was paused. Any pause (idle pause, pause key, speed buttons or an alert) is lifted for the trip home, and pausing does nothing until you are back.

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also post in the Posts tab of the mod's Nexus Mods page. When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\time-control.log`.

To reach me directly, message `exiled.dev` on Discord. You can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

A Survival Log wiki with detailed documentation for all of my mods is coming soon.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/TimeControl -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. Compiling also needs Exiled's Toolkit 0.20.0 or newer: point `-p:ToolkitDll="C:\path\to\ExiledToolkit.dll"` at its DLL. The output DLL is at `src\TimeControl\bin\Release\TimeControl.dll`.

The unit tests cover the pure timing rules and need nothing from the game:

```
dotnet test tests/TimeControl.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
