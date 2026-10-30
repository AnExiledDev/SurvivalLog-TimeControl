# Changelog

## 0.10.3

- Typing a space or a number in another mod's search box or text box no longer pauses the game or changes its speed. This needs Exiled's Toolkit 0.12.0 or newer.
- Made for Survival Log 1.1.20151.

## 0.10.2

- Fixed: typing in the item search on Survival Log 1.1.20151 no longer pauses the game or changes its speed when you type a space or a number.
- Made for Survival Log 1.1.20151.

## 0.10.1

- Fixed: Time Control no longer puts an error and a list of warnings from Il2CppInterop in the game log when the game starts.
- Made for Survival Log 1.1.19611.

## 0.10.0

- Every menu now opens paused: the bag, phone and trade window as well as the workbench, and with the new setting `PauseInOtherMenus` also cooking, brewing, the heater, the machines, shops, the drones, the codex, the map and the rest. Your speed comes back when you close it. `PauseInBag`, `PauseInPhone` and `PauseInTrade` are now on by default; a saved setting keeps its value.
- Starting a machine from its menu, such as cooking, brewing, dyeing, shredding or compressing, runs the game until you close the menu. An order or a craft started in a menu still runs until it is done, then the game pauses again.
- Made for Survival Log 1.1.19611.

## 0.9.6

- Fixed: waking your character with `WakeAtHour` or `WakeOnAlert` cost you Vim and Vigor. Your character now wakes the way the game wakes them after a full sleep, and gets Vim and Vigor if they slept at least 3 hours.
- Made for Survival Log 1.1.19611.

## 0.9.5

- Smoother play: Time Control now does far less work each frame, so it no longer causes small stutters.
- Made for Survival Log 1.1.19611.

## 0.9.4

- The speed keys and buttons now work in every menu: the workbench, bag, phone, trade, shops, stove, heater and the rest. The game stops time on its own when one of these opens (and when a craft finishes at the workbench), and ignores the speed keys until the menu closes; the mod now lifts that stop, so time runs at your speed. Use the PauseIn... and PauseAtWorkbench settings to have a menu open paused. Story, ending, settlement, event choice and settings screens still stop time.
- `PauseAtWorkbench` is now on by default, so the workbench still opens paused, now with the speed under your control. A saved setting keeps its value.
- A craft at the workbench now counts as work: with a menu pause on, starting one runs the game until it is done, then the game pauses again, and the next craft of a batch or queue starting within a second keeps it running. Idle freeze no longer pauses the game while a workbench is crafting.
- Idle freeze and the queue slow-down no longer kick in while you hold the game's hold-to-speed-up key.
- While you hold the game's hold-to-speed-up key, only danger alerts pause the game: a zombie wave, an emergency, zombies damaging the house or a trap, and low life. Turn this off with the new setting `DangerPausesWhileHolding`.
- Holding the game's hold-to-speed-up key while paused now runs time, and letting go pauses again.
- With `GameHoldKeySpeed` above 1, the fastest speed button shows the speed you run at while the key is held.
- New setting `GameHoldKeySpeed` sets how fast the game's own hold-to-speed-up key (`Shift` by default) runs, as a multiple of Fastest. The default, 1, keeps the game's own speed.
- Made for Survival Log 1.1.19611.

## 0.9.3

- With a menu pause on, an order given in that menu now runs the game only until it is done, then the game pauses again. Each new order there runs it again, even one queued behind an order that was already running when the menu opened.
- The Wait until button is hidden unless `WaitButton` (under Display) is set to `true`.
- Made for Survival Log 1.1.19611.

## 0.9.2

- The Slow button keeps the game's own slow on its first press, idle slowdown included. Pressing it again goes on to the slower speeds in `SlowSpeeds` (0.5x, then 0.25x by default), then back to the game's own slow.
- Holding the game's own hold-to-speed-up key now works while one of the mod's slow speeds or super speed is in use; before, the mod put its speed straight back.
- With a menu pause on (`PauseAtWorkbench` and the others), giving an order in that menu, such as a craft at the workbench, brings your speed back at once.
- Made for Survival Log 1.1.19611.

## 0.9.1

- Works with Survival Log 1.1.19611, which changed how the game checks whether a key belongs to one of its own actions.
- Made for Survival Log 1.1.19611.

## 0.9.0

- Pause when the game window goes to the background (on by default), and optional pauses while the bag, workbench, phone or trade window is open. Your speed comes back when you return.
- `OnQueueEmpty` replaces `PauseOnQueueEmpty` with Off, Slow or Pause, so the game can slow down instead of pausing when the queue runs out. An old `PauseOnQueueEmpty = true` becomes Pause.
- New optional pause alerts: house or trap damage in a wave, power out, low needs, illness, phone and trade messages, ripe crops, dusk and dawn.
- Hold `X` for the fastest speed and `K` for the slowest slow speed; letting go brings back the speed you had, pause included.
- Keys `5` to `9` can hold speeds of your own, and the mouse wheel over the speed buttons steps through every speed.
- At home the clock shows when the current order is done and how long until night and the next wave.
- A Wait until button beside the speed buttons: fast-forward to morning, the alarm time, a brew or the queue being done, or a few hours on, then pause. The same menu sets one-time "Pause in" alarms.
- An alarm clock (`AlarmTime`, `AlarmDaily`), `StartPaused`, and `WakeAtHour` and `WakeOnAlert` to end a sleep early. Naps are never cut short.
- Made for Survival Log 1.1.19131.

## 0.8.1

- A small chip on the top right corner of the slow or fastest speed button shows the speed the game really runs at while the mod holds a slow speed or a speed above Fastest, such as 0.25x or 6x. It never takes a click.
- After a game update, Mod Manager can tell you if this mod may need an update.
- Made for Survival Log 1.1.19131.

## 0.8.0

- Mod Manager's Mods screen links to this changelog.
- On the Mods screen, a setting that only matters while another one is on stays hidden until that one is on.
- Settings changed on the Mod Manager's Mods screen take effect at once, except SlowSpeeds, FastSpeeds, FastForwardSleep, PauseToggleKey, CenterCameraKey and TimeControlOnExpeditions, which take effect at the next game start.
- Mod Manager's Mods screen shows the mod's name, description, category and links.
- Exiled's Toolkit 0.4.0 or newer is optional. With it, settings you change on the Mods screen take effect right away. Without it, they take effect the next time you start the game.

## 0.7.0

- Removed the `ClockRate` setting. For longer days, use the Day Length mod. An old `ClockRate` line left in the config file does nothing.

## 0.6.3

- First release.
- A true slow speed and a super speed.
- The game can pause while you stand idle, or when a wave or emergency starts.
- Fast sleep, with a pause on waking.
- A pause key that works before the disaster and on expeditions.
