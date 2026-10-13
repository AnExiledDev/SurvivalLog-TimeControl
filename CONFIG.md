# Time Control settings

Settings live in `BepInEx\config\com.dev.survivallog.timecontrol.cfg`, created the first time the game starts with the mod. Edit the file while the game is closed; changes apply at the next game start. Delete the file to get the defaults back. Each setting in the file is described in English and in Simplified Chinese.

## SlowButton

| Setting | Default | What it does |
| --- | --- | --- |
| `SlowSpeeds` | `0.5,0.25` | Speeds for the Slow button and the `` ` `` (`~`) key. Pressing slow again while slow moves to the next speed, and after the last back to the first. |

A comma-separated list. Each speed is kept between 0.05 and 0.95; a number outside that range is moved to the nearest end, and an entry that is not a number is skipped. Leave it empty to keep the game's own slow button.

## FastButton

| Setting | Default | What it does |
| --- | --- | --- |
| `FastSpeeds` | `2` | Extra speeds above the game's Fastest speed, as multiples of it. Pressing the Fastest button or the `3` key while already at Fastest moves to the next one, and after the last back to Fastest. |

A comma-separated list, for example `2,4`. Each multiple is kept between 1.1 and 10; an entry that is not a number is skipped. Speeds are capped at 40x: the game moves the world one step per frame, and past 40x each step at 30 frames a second would be longer than the game allows. Leave it empty to keep the game's own Fastest button.

## Clock

| Setting | Default | What it does |
| --- | --- | --- |
| `ClockRate` | `1` | How fast the day clock runs. At `0.5` each day lasts twice as long, while walking and actions keep their speed. `1` is the game's own pace. |
| `IdleFreeze` | `true` | Pause the game while your character is idle with nothing queued, and resume it on your next order. Works at home, before the disaster and on expeditions. Pick a speed or press the pause key (`Space` by default) while it is paused to let time run until your next order. |
| `IdleFreezeDelaySeconds` | `3` | How many real seconds your character stands idle before the game pauses, so a short gap between two orders does not pause it. `0` pauses as soon as nothing is queued. |

`ClockRate` is kept between 0.05 and 4. It changes only the day clock at home, not the countdown before the disaster or an expedition. `IdleFreezeDelaySeconds` below 0 counts as 0.

## Sleep

| Setting | Default | What it does |
| --- | --- | --- |
| `FastForwardSleep` | `true` | Run sleep and naps at the fastest `FastSpeeds` speed (or at Fastest if `FastSpeeds` is empty), instead of the game's own sleep speed-up. A speed you pick while asleep is kept until you wake. `false` keeps the game's own sleep speed-up. |
| `PauseOnWake` | `true` | Pause the game when your character wakes from sleep or a nap, however it ends. Your next order or the pause key brings back the speed you had before the sleep. |

## Alerts

| Setting | Default | What it does |
| --- | --- | --- |
| `PauseOnWave` | `true` | Pause the game once when a zombie wave starts. |
| `PauseOnEmergency` | `true` | Pause the game once when an emergency starts. |
| `PauseOnDroneArrival` | `false` | Pause the game once when a trade drone comes home from a trip. |
| `PauseOnQueueEmpty` | `false` | Pause the game once when your character finishes the last order in their queue. Unlike `IdleFreeze`, it pauses right away and stays paused until you pick a speed or press the pause key. |

Pick a speed or press the pause key to carry on. The same alert never pauses the game twice. Alerts pause only at home, in the countdown before the disaster and on expeditions, and not while your character sleeps. On expeditions alerts only pause when `TimeControlOnExpeditions` is `true`. An alert that starts while the game is already paused is let go rather than saved for later.

## Keys

| Setting | Default | What it does |
| --- | --- | --- |
| `PauseToggleKey` | `Space` | The key that pauses the game and, on the next press, brings back the speed you had. Works at home, before the disaster and on expeditions. The game uses `Space` for camera follow (centering the camera on your character), so while this is `Space` the mod moves camera follow to `CenterCameraKey`. `None` turns it off. |
| `CenterCameraKey` | `G` | The key for camera follow (centering the camera on your character) when `PauseToggleKey` is `Space`, which is the game's own key for camera follow. By default the game does not use `G` for anything else. `None` leaves camera follow with no key. |

Both take a Unity key name, such as `Space`, `P`, `F5`, `Pause` or `Keypad0`. Upper and lower case do not matter. A name that is not a Unity key turns the pause key off, or puts camera follow on `G`, and the log says so.

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
| `PauseOverlayOpacity` | `0.5` | How visible the game's own paused banner across the lower middle of the screen is, from `0` (hidden) to `1` (as the game draws it). Lower values let you read the phone's reply choices under it. Clicks always pass through the banner. |

Each badge goes away the moment time runs again. A badge is see-through and clicks go straight through it. Where the screen shows no speed buttons, the badge sits at the top centre instead. The one exception is the countdown before the disaster: there a badge only shows beside the mod's speed buttons, so with `SpeedButtons` set to `false`, or where the mod adds no buttons, no badge shows in the countdown.

`SpeedButtons` only adds buttons where the mod controls the speed: on expeditions only when `TimeControlOnExpeditions` is `true`. At home after the disaster the game shows its own buttons, and the mod adds none.
