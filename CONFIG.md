# Time Control settings

Settings live in `BepInEx\config\com.dev.survivallog.timecontrol.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, or edit the file while the game is closed. Changed on the Mods screen, every setting takes effect at once except `SlowSpeeds`, `FastSpeeds`, `FastForwardSleep`, `PauseToggleKey`, `CenterCameraKey` and `TimeControlOnExpeditions`, which take effect at the next game start. Delete the file to get the defaults back. Each setting in the file is described in English and in Simplified Chinese. Without Exiled's Toolkit installed, changes made on the Mods screen take effect at the next game start instead.

## SlowButton

| Setting | Default | What it does |
| --- | --- | --- |
| `SlowSpeeds` | `0.5,0.25` | Extra slower speeds for the Slow button and the `` ` `` (`~`) key. The first press is the game's own slow; pressing slow again moves on to these speeds, and after the last back to the game's own slow. |

A comma-separated list. Each speed is kept between 0.05 and 0.95; a number outside that range is moved to the nearest end, and an entry that is not a number is skipped. Leave it empty to keep only the game's own slow.

## FastButton

| Setting | Default | What it does |
| --- | --- | --- |
| `FastSpeeds` | `2` | Extra speeds above the game's Fastest speed, as multiples of it. Pressing the Fastest button or the `3` key while already at Fastest moves to the next one, and after the last back to Fastest. |

A comma-separated list, for example `2,4`. Each multiple is kept between 1.1 and 10; an entry that is not a number is skipped. Speeds are capped at 40x: the game moves the world one step per frame, and past 40x each step at 30 frames a second would be longer than the game allows. Leave it empty to keep the game's own Fastest button.

## Clock

| Setting | Default | What it does |
| --- | --- | --- |
| `IdleFreeze` | `true` | Pause the game while your character is idle with nothing queued, and resume it on your next order. Works at home, before the disaster and on expeditions. Pick a speed or press the pause key (`Space` by default) while it is paused to let time run until your next order. |
| `IdleFreezeDelaySeconds` | `3` | How many real seconds your character stands idle before the game pauses, so a short gap between two orders does not pause it. `0` pauses as soon as nothing is queued. |
| `PauseOnFocusLoss` | `true` | Pause the game when its window goes to the background, for example after Alt+Tab, and bring back your speed when you switch back. Works at home, before the disaster, on expeditions (with `TimeControlOnExpeditions` on) and during sleep fast-forward. A running Wait until carries on when you come back. |
| `PauseInBag` | `false` | Pause the game while your bag is open, and bring back your speed when you close it. An order given while it is open runs the game until it is done, then it pauses again. |
| `PauseAtWorkbench` | `true` | Pause the game while the workbench window is open, and bring back your speed when you close it. An order or a craft started while it is open runs the game until it is done, then it pauses again; the next craft of a batch or queue starting within a second keeps it running. |
| `PauseInPhone` | `false` | Pause the game while the phone is open, and bring back your speed when you close it. An order given while it is open runs the game until it is done, then it pauses again. |
| `PauseInTrade` | `false` | Pause the game while the trade window is open, and bring back your speed when you close it. An order given while it is open runs the game until it is done, then it pauses again. |
| `StartPaused` | `false` | Start paused when a save is loaded or a new game begins, at home and before the disaster. The pause key starts time at the speed the game would have run at. A guided start or a story scene is left running. |

If you change the speed yourself while the game is away-paused or a menu is open, your choice is kept and nothing is brought back.

`IdleFreezeDelaySeconds` below 0 counts as 0.

## Sleep

| Setting | Default | What it does |
| --- | --- | --- |
| `FastForwardSleep` | `true` | Run sleep and naps at the fastest `FastSpeeds` speed (or at Fastest if `FastSpeeds` is empty), instead of the game's own sleep speed-up. A speed you pick while asleep is kept until you wake. `false` keeps the game's own sleep speed-up. |
| `PauseOnWake` | `true` | Pause the game when your character wakes from sleep or a nap, however it ends. Your next order or the pause key brings back the speed you had before the sleep. |
| `WakeAtHour` | empty | A time of day on a 24-hour clock, such as `6` or `6:30`, at which your character wakes from sleep if the game has not woken them yet. It only ends a sleep early, never makes it longer. Naps are never cut short. Empty lets the game decide. |
| `WakeOnAlert` | `false` | Wake your character from sleep when an alert switched on under Alerts pauses the game, and keep the game paused once they are up. `false` lets a sleeping character sleep through alerts. Naps are never cut short. |

A time that cannot be read is logged once and counts as empty.

## Alarm

| Setting | Default | What it does |
| --- | --- | --- |
| `AlarmTime` | empty | A time of day on a 24-hour clock, such as `7` or `21:30`, at which the game pauses once, at home and before the disaster. The pause key carries on at the speed you had. It stays quiet while your character sleeps and rings once they wake. Empty means no alarm. |
| `AlarmDaily` | `true` | Pause at `AlarmTime` every day. `false` pauses only once, and again only after `AlarmTime` changes or the game restarts. |

With `WaitButton` on, the Wait until button (below) can also set a one-time alarm a few hours ahead.

## Alerts

| Setting | Default | What it does |
| --- | --- | --- |
| `PauseOnWave` | `true` | Pause the game once when a zombie wave starts. |
| `PauseOnEmergency` | `true` | Pause the game once when an emergency starts. |
| `PauseOnDroneArrival` | `false` | Pause the game once when a trade drone comes home from a trip. |
| `OnQueueEmpty` | `Off` | What happens when your character finishes the last order in their queue. `Off` does nothing. `Slow` drops the game to `QueueEmptySlowSpeed` if it runs faster, and brings your speed back on your next order unless you change the speed yourself in between. `Pause` pauses the game once; unlike `IdleFreeze`, it pauses right away and stays paused until you pick a speed or press the pause key. |
| `QueueEmptySlowSpeed` | `Normal` | The speed `OnQueueEmpty` = `Slow` drops the game to: `Slow` (the slow speed you last used), `Normal` or `Fast`. It only slows the game down. |
| `PauseOnHouseHit` | `false` | Pause the game once when zombies first damage the house, a door or a window during a wave. |
| `PauseOnTrapHit` | `false` | Pause the game once when zombies first damage a trap during a wave. |
| `PauseOnPowerOut` | `false` | Pause the game once when the power goes out. |
| `PauseOnLowNeed` | `false` | Pause the game once when your character's satiety, morale, stamina, fitness or life falls to `LowNeedPercent` of its maximum or below. It pauses again for the same need only after it has risen a little above that. |
| `LowNeedPercent` | `20` | How low a need has to fall, in percent of its maximum, for `PauseOnLowNeed`. From 1 to 90. |
| `PauseOnSick` | `false` | Pause the game once when your character falls ill. |
| `PauseOnPhoneMessage` | `false` | Pause the game once when a new text message arrives on the phone. Trade messages have their own setting. |
| `PauseOnTradeMessage` | `false` | Pause the game once when a new trade message arrives on the phone. |
| `PauseOnCropRipe` | `false` | Pause the game once when a crop is ready to harvest. Several ripening at once pause it once. |
| `PauseOnDusk` | `false` | Pause the game once at 19:00 game time. |
| `PauseOnDawn` | `false` | Pause the game once at 7:00 game time. Sleeping through it does not pause the game. |

`OnQueueEmpty` replaces `PauseOnQueueEmpty`. An old `PauseOnQueueEmpty = true` becomes `OnQueueEmpty = Pause` the first time the game starts with this version.

Pick a speed or press the pause key to carry on. The same alert never pauses the game twice. Alerts pause only at home, in the countdown before the disaster and on expeditions, and not while your character sleeps. On expeditions alerts only pause when `TimeControlOnExpeditions` is `true`. An alert that starts while the game is already paused is let go rather than saved for later.

## Keys

| Setting | Default | What it does |
| --- | --- | --- |
| `PauseToggleKey` | `Space` | The key that pauses the game and, on the next press, brings back the speed you had. Works at home, before the disaster and on expeditions. The game uses `Space` for camera follow (centering the camera on your character), so while this is `Space` the mod moves camera follow to `CenterCameraKey`. `None` turns it off. |
| `CenterCameraKey` | `G` | The key for camera follow (centering the camera on your character) when `PauseToggleKey` is `Space`, which is the game's own key for camera follow. By default the game does not use `G` for anything else. `None` leaves camera follow with no key. |
| `HoldFastKey` | `X` | Hold this key to run the game at the fastest speed the mod has (the fastest `FastSpeeds` speed, or Fastest if `FastSpeeds` is empty). Letting go brings back the speed you had, and if the game was paused it pauses again. Not while your character sleeps. `None` turns it off. |
| `HoldSlowKey` | `K` | Hold this key to run the game at the slowest `SlowSpeeds` speed. Letting go brings back the speed you had, pause included. Does nothing while `SlowSpeeds` is empty. While both hold keys are down, `HoldFastKey` wins. `None` turns it off. |
| `GameHoldKeySpeed` | `1` | How fast the game runs while you hold the game's own hold-to-speed-up key (`Shift` by default), as a multiple of the game's Fastest speed, from 1 to 10. `1` keeps the game's own speed-up, which runs at Fastest. Holding the key does nothing while the game already runs at that speed or faster. Held while paused, it runs time and pauses again when you let go. Above `1`, the Fastest button shows the speed while the key is held. Speeds are capped at 40x. |
| `SpeedKeys5To9` | empty | Custom speeds for the keys `5` to `9`, separated by commas in that order, each from 0.05 to 40 (for example `0.75,1.5,6`). Leave a place empty to skip that key. Pressing the key runs the game at that speed and also resumes a pause. The game keeps 5 to 9 for its quick actions, which it has switched off for now; if a game update turns them back on, a key set here does both. Empty turns them all off. |
| `SpeedWheel` | `true` | Turn the mouse wheel over the speed buttons to step through every speed, slowest to fastest; wheel up is faster. The camera does not zoom while the pointer is over the buttons. Does nothing while paused. |

The hold keys work at home, before the disaster and on expeditions when `TimeControlOnExpeditions` is on. A key the game uses for anything else does nothing. `PauseToggleKey`, `CenterCameraKey` and the hold keys take a Unity key name, such as `Space`, `P`, `F5`, `Pause` or `Keypad0`. Upper and lower case do not matter. A name that is not a Unity key turns the pause key off, or puts camera follow on `G`, and the log says so.

If the game uses the pause key for one of its own actions, the pause key does nothing; rebind that action in the game's key settings or pick another key. A key you set for camera follow in the game's own key settings always wins over `CenterCameraKey`.

The game's own speed keys are `1` (normal), `2` (fast), `3` (fastest), `4` (pause) and `` ` `` (`~`, slow) by default. They keep working alongside the pause key.

## Expedition

| Setting | Default | What it does |
| --- | --- | --- |
| `TimeControlOnExpeditions` | `true` | Let the pause key, the game's speed keys (`1`, `2`, `3`, `4` for pause and `` ` `` (`~`) for slow, by default), the `SlowSpeeds` and `FastSpeeds` speeds, the idle pause and the alert pauses work on expeditions. Pausing there also stops the exposure meter. `false` leaves expeditions at the game's own pace and hides the mod's speed buttons there. |

## Display

| Setting | Default | What it does |
| --- | --- | --- |
| `IdleBadge` | `true` | Show a small "Paused: idle" badge next to the speed buttons while the idle pause holds the game. |
| `PauseKeyBadge` | `true` | Show a small "Paused" badge next to the speed buttons while a pause you made with a key holds the game: the pause key anywhere, or the game's own pause key (`4` by default) before the disaster and on expeditions. Before the disaster the badge needs the mod's speed buttons (see below). |
| `AlertBadge` | `true` | Show a small badge naming the alert, such as "Paused: wave", next to the speed buttons while an alert pause holds the game. |
| `SpeedButtons` | `true` | Show speed buttons (slow, normal, fast, fastest and pause) before the disaster and on expeditions, where the game has none. They work like the game's own buttons at home, using your `SlowSpeeds` and `FastSpeeds`. `false` hides them; the keys still work. |
| `OrderFinishTime` | `true` | Show, under the clock at home, the game time your character's current order will be done. Only the order being carried out counts, not the ones queued after it. |
| `NightCountdown` | `true` | Show, under the clock at home, how much game time is left until night. It shows during the day only. |
| `WaveCountdown` | `true` | Show, under the clock at home, how much game time is left until the next wave. It counts the waves the game has planned for today and the wave a lure rig calls in. |
| `WaitButton` | `false` | Show the Wait until button beside the speed buttons (see below). |
| `PauseOverlayOpacity` | `0.5` | How visible the game's own paused banner across the lower middle of the screen is, from `0` (hidden) to `1` (as the game draws it). Lower values let you read the phone's reply choices under it. Clicks always pass through the banner. |

Each badge goes away the moment time runs again. A badge is see-through and clicks go straight through it. Where the screen shows no speed buttons, the badge sits at the top centre instead. The one exception is the countdown before the disaster: there a badge only shows beside the mod's speed buttons, so with `SpeedButtons` set to `false`, or where the mod adds no buttons, no badge shows in the countdown.

`SpeedButtons` only adds buttons where the mod controls the speed: on expeditions only when `TimeControlOnExpeditions` is `true`. At home after the disaster the game shows its own buttons, and the mod adds none.

## Wait until button

With `WaitButton` set to `true`, a clock button at the end of the speed buttons (at home, and before the disaster when `SpeedButtons` is on) opens a menu:

- **Wait until** morning (07:00), the `AlarmTime`, a brew being done, or the queue being done; or **Wait** 1, 2, 4 or 8 hours. The game runs at the fastest speed and pauses when it gets there; the pause key then brings back the speed from before. Picking a speed yourself ends the wait.
- **Pause in** 1, 2, 4 or 8 hours sets a one-time alarm.
- A running wait or alarm shows in the menu with a Cancel button, and the button lights up.

Waits are greyed out while your character sleeps or on the way back from an expedition. An alert pause ends a wait.
