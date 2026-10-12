# Time Control settings

Settings live in `BepInEx\config\com.dev.survivallog.timecontrol.cfg`, created the first time the game starts with the mod. Edit the file while the game is closed; changes apply at the next game start. Delete the file to get the defaults back.

## SlowButton

| Setting | Default | What it does |
| --- | --- | --- |
| `SlowSpeeds` | `0.5,0.25` | Speeds the slow button holds. Pressing slow again while slow moves to the next one, and after the last back to the first. |

A comma-separated list. Each speed is kept between 0.05 and 0.95; a number outside that range is moved to the nearest end, and an entry that is not a number is skipped. Empty leaves the game's own slow button alone.

## FastButton

| Setting | Default | What it does |
| --- | --- | --- |
| `FastSpeeds` | `2` | Super speeds above the game's high speed, as multiples of it. Pressing the fast button or key `3` while already at high speed moves to the next one, and after the last back to the game's high speed. |

A comma-separated list, for example `2,4`. Each multiple is kept between 1.1 and 10; an entry that is not a number is skipped. The speed held never goes past 40x, whatever the multiple: the game moves its world one step per frame, and past 40x each step at 30 frames a second is longer than the game's own speeds allow. Empty leaves the game's own fast button alone.

## Clock

| Setting | Default | What it does |
| --- | --- | --- |
| `ClockRate` | `1` | How fast the day clock runs. `0.5` makes each day last twice as long; walking and actions keep their speed. `1` is the game's pace. |
| `IdleFreeze` | `true` | Pause the game while your character stands idle with nothing queued, at home, in the countdown before the disaster and on expeditions, and resume it on your next order. Press play or the pause key while it is paused to let time run until your next order. |
| `IdleFreezeDelaySeconds` | `3` | Real seconds your character stands idle before the game pauses, so a moment between two orders does not pause it. `0` pauses as soon as nothing is queued. |

`ClockRate` is kept between 0.05 and 4. It changes only the day clock at home, not the countdown before the disaster or an expedition. `IdleFreezeDelaySeconds` below 0 counts as 0.

## Sleep

| Setting | Default | What it does |
| --- | --- | --- |
| `FastForwardSleep` | `true` | Run sleep and naps at the fastest `FastSpeeds` speed (the game's high speed if `FastSpeeds` is empty), in place of the game's own sleep speed-up. A speed you pick during the sleep is kept until you wake. `false` leaves the game's own sleep speed-up as it is. |
| `PauseOnWake` | `true` | Pause the game when your character wakes from a sleep or nap, however it ends. Your next order, play or the pause key resumes the speed you had before the sleep. |

## Alerts

| Setting | Default | What it does |
| --- | --- | --- |
| `PauseOnWave` | `true` | Pause the game once when a zombie wave starts. |
| `PauseOnEmergency` | `true` | Pause the game once when an emergency fires. |
| `PauseOnDroneArrival` | `false` | Pause the game once when a trade drone comes home from a trip. |
| `PauseOnQueueEmpty` | `false` | Pause the game once when your character finishes the last order in their queue. Unlike `IdleFreeze` it pauses at once and stays paused until you resume it. |

Play or the pause key resumes the speed you had, and the same alert never pauses the game twice. Alerts pause only at home, in the countdown before the disaster and on expeditions, and not while your character sleeps. On expeditions alerts only pause when `TimeControlOnExpeditions` is `true`. An alert that starts while the game is already paused is let go rather than saved for later.

## Keys

| Setting | Default | What it does |
| --- | --- | --- |
| `PauseToggleKey` | `Space` | A key that pauses, and on the next press resumes the speed you had, at home, in the countdown before the disaster and on expeditions. `None` turns it off. |
| `CenterCameraKey` | `G` | The game's default key for centering the camera on your character, moved off `Space` so `Space` can pause. `None` leaves centering the camera with no key. |

Both take a Unity key name, such as `Space`, `P`, `F5`, `Pause` or `Keypad0`. Upper and lower case do not matter. A name that is not a Unity key turns the pause key off, or puts centering the camera on `G`, and the log says so.

If the game uses the pause key for one of its own actions, the pause key stands aside and does nothing; rebind that action in the game's key settings or pick another key. A key you set for centering the camera in the game's own key settings always wins over `CenterCameraKey`. Centering the camera only moves when the pause key is the game's default for it.

## Expedition

| Setting | Default | What it does |
| --- | --- | --- |
| `TimeControlOnExpeditions` | `true` | Let the pause key, the game's speed keys (1, 2, 3 and its pause and slow keys), the slow and fast buttons' speeds and the idle pause work on expeditions, where pausing also stops the exposure meter. `false` leaves expeditions at the game's own pace, as before 0.4.0, and hides the mod's speed buttons there. |

## Display

| Setting | Default | What it does |
| --- | --- | --- |
| `IdleBadge` | `true` | Show a small "Paused: idle" badge above the speed buttons while the idle pause holds the game. |
| `PauseKeyBadge` | `true` | Show a small "Paused" badge above the speed buttons while a pause you made with a key holds the game: the pause key anywhere, or the game's own pause key in the countdown before the disaster and on expeditions. |
| `AlertBadge` | `true` | Show a small badge naming the alert, such as "Paused: wave", above the speed buttons while a pause on an alert holds the game. |
| `SpeedButtons` | `true` | Show speed buttons (slow, normal, fast, fastest and pause) in the countdown before the disaster and on expeditions, where the game shows none. They work like the game's own buttons at home, with this mod's slow and super speeds. `false` hides them; the keys still work. |

Each badge goes away the moment time runs again. Where the screen shows no speed buttons, the badge sits at the top centre instead.

`SpeedButtons` only adds buttons where the mod controls the speed: on expeditions only when `TimeControlOnExpeditions` is `true`. At home after the disaster the game shows its own buttons, and the mod adds none.
