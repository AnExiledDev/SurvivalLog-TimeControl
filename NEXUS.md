Name: Time Control
Summary: A true slow speed, a super speed, a pause while you stand idle or when a wave or emergency starts, fast sleep with a pause on waking, and a pause key that works before the disaster and on expeditions. 真正的慢速、超高速、空闲或尸潮和危机事件开始时自动暂停、快速睡眠并在醒来时暂停，暂停键在灾前筹备和外出探索时也能用。

Description (BBCode):
The game's slow button only slows once you stand idle, and there is no way to pause before the disaster or on an expedition. Time Control gives you a real slow speed, a faster speed, and a pause that waits while your character has nothing to do.

[size=5]Main features[/size]
[list]
[*][b]True slow speed[/b]: the slow button holds 0.5x, then 0.25x on the next press.[/*]
[*][b]Super speed[/b]: press the fast button or key 3 again at high speed to run twice as fast.[/*]
[*][b]Idle pause[/b]: the game pauses after 3 seconds with nothing queued, and your next order resumes it.[/*]
[*][b]Pause on alerts[/b]: the game pauses once when a zombie wave starts or an emergency fires. A pause when a trade drone comes home or your order queue runs out can be switched on too. On expeditions alerts only pause when TimeControlOnExpeditions is true.[/*]
[*][b]Fast sleep[/b]: sleep and naps run at super speed, and the game pauses when you wake.[/*]
[*][b]Slower days[/b]: make the day clock slower without slowing walking or actions.[/*]
[*][b]Pause key[/b]: Space pauses and resumes, at home, before the disaster and on expeditions.[/*]
[*][b]Expeditions[/b]: pause, speed keys, slow and super speed and the idle pause all work on expeditions.[/*]
[*][b]Speed buttons everywhere[/b]: slow, normal, fast, fastest and pause buttons in the countdown before the disaster and on expeditions, where the game shows none. They work like the game's own buttons at home.[/*]
[*][b]Pause badge[/b]: a small "Paused" or "Paused: idle" chip beside the speed buttons shows why the game stands still. It is see-through and clicks go straight through it.[/*]
[*][b]See-through paused banner[/b]: the game's own paused banner is drawn half see-through, so the phone's reply choices under it stay readable. PauseOverlayOpacity in the config sets how much.[/*]
[*][b]Hover highlights[/b]: objects still highlight under the mouse while paused or slowed.[/*]
[/list]

[size=5]Hotkeys[/size]
[list]
[*][b]Space[/b]: pause, press again to resume.[/*]
[*][b]G[/b]: center the camera on your character (it was on Space).[/*]
[*][b]1, 2, 3[/b]: the game's own speeds. Press 3 again at high speed for super speed.[/*]
[/list]
All keys can be changed in the config. A key you set in the game's own key settings always wins.

Time Control adds no data of its own to your save and does not edit game files. It does change how fast game time runs, and that is saved with your game. You can remove it at any time.

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*]Back up your save before trying it for the first time.[/*]
[*]Game time is finicky. Events, sleep, zombie waves and expedition timers all run on it, so anything that happens at an odd time is worth reporting.[/*]
[*]Expedition support and the pause badge are new in 0.4.0 and have not been played through in the game yet. Each can be turned off in the config.[/*]
[*]Pause on alerts is new in 0.5.0 and has not been played through in the game yet. Each alert can be turned off in the config.[/*]
[*]The speed buttons before the disaster and on expeditions are new in 0.6.0 and have not been played through in the game yet. Turn them off with SpeedButtons in the config if they get in the way.[/*]
[*]On an expedition the exposure alarm keeps sounding while paused. The meter itself stops.[/*]
[*]The badge can blink out for up to half a second when the game redraws its speed buttons.[/*]
[*]The badge's place beside the speed buttons and the see-through paused banner are new in 0.6.1 and have not been played through in the game yet. Set PauseOverlayOpacity to 1 to keep the game's banner as it is.[/*]
[/list]
If you run into anything strange, please tell me (see Feedback below).

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.18902. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Put [font=Courier New]TimeControl.dll[/font] in the BepInEx&#92;plugins folder inside the game folder. Full path example:
[code]C:&#92;Program Files (x86)&#92;Steam&#92;steamapps&#92;common&#92;Survival Log&#92;BepInEx&#92;plugins&#92;TimeControl.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]TimeControl.dll[/font] from the [font=Courier New]BepInEx&#92;plugins[/font] folder. If centering the camera is still on G afterwards, set it back in the game's key settings.

[size=5]Configuration[/size]
To change settings, edit [font=Courier New]BepInEx&#92;config&#92;com.dev.survivallog.timecontrol.cfg[/font] while the game is closed (see [url=https://github.com/AnExiledDev/SurvivalLog-TimeControl/blob/main/CONFIG.md]CONFIG.md[/url] for every setting). Changes apply at the next game start.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or leave a comment here. For bugs, please include [font=Courier New]BepInEx&#92;LogOutput.log[/font] and [font=Courier New]BepInEx&#92;time-control.log[/font].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-TimeControl]github.com/AnExiledDev/SurvivalLog-TimeControl[/url]

[line]
[size=5]简体中文[/size]

游戏的慢速按钮只有在角色空闲时才会变慢，而且在灾前筹备和外出探索时都没法暂停。Time Control 提供真正的慢速、更快的速度，以及在角色无事可做时自动等待的暂停。

[size=5]主要功能[/size]
[list]
[*][b]真正的慢速[/b]：慢速按钮保持 0.5 倍速，再按一次变为 0.25 倍速。[/*]
[*][b]超高速[/b]：在高速时再按一次快进按钮或 3 键，速度再翻一倍。[/*]
[*][b]空闲暂停[/b]：队列里没有指令 3 秒后游戏会暂停，下一个指令会让游戏继续。[/*]
[*][b]事件暂停[/b]：尸潮开始或危机事件发生时，游戏暂停一次。交易无人机归来或指令队列做完时的暂停也可以打开。外出探索时，只有 TimeControlOnExpeditions 为 true 才会因事件暂停。[/*]
[*][b]快速睡眠[/b]：睡觉和小睡以超高速进行，醒来时游戏暂停。[/*]
[*][b]更慢的白天[/b]：让白天的时钟走得更慢，而走路和行动的速度不变。[/*]
[*][b]暂停键[/b]：按 Space 暂停或继续，在安全屋、灾前筹备和外出探索时都能用。[/*]
[*][b]外出探索[/b]：暂停、速度键、慢速、超高速和空闲暂停在外出探索时都能用。[/*]
[*][b]到处都有速度按钮[/b]：在灾前筹备和外出探索时，游戏本身不显示速度按钮，本模组会补上慢速、正常、快速、极速和暂停按钮，用法和安全屋里游戏自带的按钮一样。[/*]
[*][b]暂停标签[/b]：速度按钮旁的小标签「已暂停」或「已暂停：空闲」说明游戏为什么停着。它是半透明的，点击会直接穿过它。[/*]
[*][b]半透明的暂停横幅[/b]：游戏自己的暂停横幅改为半透明，下面手机里的回复选项依然看得清。配置中的 PauseOverlayOpacity 可调节透明度。[/*]
[*][b]悬停高亮[/b]：暂停或慢速时，鼠标下的物体依然会高亮。[/*]
[/list]

[size=5]快捷键[/size]
[list]
[*][b]Space[/b]：暂停，再按一次继续。[/*]
[*][b]G[/b]：镜头对准你的角色（原本在 Space 上）。[/*]
[*][b]1、2、3[/b]：游戏自己的速度。在高速时再按一次 3 进入超高速。[/*]
[/list]
所有按键都可以在配置中修改。你在游戏按键设置里设定的按键始终优先。

Time Control 不会往存档里添加自己的数据，也不会修改游戏文件。它确实会改变游戏时间的流速，而这会随游戏一起保存。随时可以移除。

支持英文和中文。

[size=5]已知问题[/size]
[list]
[*]第一次使用前请先备份存档。[/*]
[*]游戏时间比较敏感。事件、睡眠、尸潮和外出探索的计时都依赖它，所以出现任何时机不对的情况都值得反馈。[/*]
[*]外出探索支持和暂停标签是 0.4.0 新增的，还没有在游戏中完整玩过。每项都可以在配置中关闭。[/*]
[*]事件暂停是 0.5.0 新增的，还没有在游戏中完整玩过。每种事件都可以在配置中关闭。[/*]
[*]灾前筹备和外出探索时的速度按钮是 0.6.0 新增的，还没有在游戏中完整玩过。如果碍事，可以在配置中用 SpeedButtons 关闭。[/*]
[*]外出探索时，暂停期间暴露值的警报声还会继续响，但暴露值本身会停止上涨。[/*]
[*]游戏重绘速度按钮时，暂停标签可能会消失最多半秒。[/*]
[*]暂停标签移到速度按钮旁边以及半透明的暂停横幅是 0.6.1 新增的，还没有在游戏中完整玩过。把 PauseOverlayOpacity 设为 1 可保持游戏原本的横幅。[/*]
[/list]
如果遇到任何奇怪的情况，欢迎告诉我（见下方「反馈」）。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.18902。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]把 [font=Courier New]TimeControl.dll[/font] 放进游戏目录下的 BepInEx&#92;plugins 文件夹。完整路径示例：
[code]C:&#92;Program Files (x86)&#92;Steam&#92;steamapps&#92;common&#92;Survival Log&#92;BepInEx&#92;plugins&#92;TimeControl.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx&#92;plugins[/font] 文件夹中删除 [font=Courier New]TimeControl.dll[/font]。如果之后镜头对准角色的按键还在 G 上，请在游戏的按键设置里改回来。

[size=5]配置[/size]
如需修改设置，请在游戏关闭时编辑 [font=Courier New]BepInEx&#92;config&#92;com.dev.survivallog.timecontrol.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-TimeControl/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。修改会在下次启动游戏时生效。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx&#92;LogOutput.log[/font] 和 [font=Courier New]BepInEx&#92;time-control.log[/font]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-TimeControl]github.com/AnExiledDev/SurvivalLog-TimeControl[/url]
