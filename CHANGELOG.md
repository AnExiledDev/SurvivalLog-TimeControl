# Changelog

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
- After a game update, a hook that can no longer be placed is logged as an error, so the Mod Manager can mark the mod as maybe needing an update.
- Made for Survival Log 1.1.19131.

## 0.8.0

- The Mods screen links this changelog.
- On the Mods screen, settings that only matter while another setting is on stay hidden until it is.
- Settings changed on the Mod Manager's Mods screen take effect at once, except SlowSpeeds, FastSpeeds, FastForwardSleep, PauseToggleKey, CenterCameraKey and TimeControlOnExpeditions, which take effect at the next game start.
- The Mods screen shows the mod's name, description, category and links.
- Exiled's Toolkit 0.4.0 or newer is optional. With it, settings changed on the Mods screen take effect without a restart; without it, at the next game start.

## 0.7.0

- Removed the `ClockRate` setting. For longer days, use the Day Length mod. An old `ClockRate` line left in the config file does nothing.

## 0.6.3

First public release. A true slow speed, a super speed, a pause while you stand idle or when a wave or emergency starts, fast sleep with a pause on waking, and a pause key that works before the disaster and on expeditions.
