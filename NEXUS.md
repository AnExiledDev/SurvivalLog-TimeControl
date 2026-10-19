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
[*]On an expedition the exposure alarm keeps sounding while paused. The meter itself stops.[/*]
[*]The badge can blink out for up to half a second when the game redraws its speed buttons.[/*]
[/list]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.18902. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*]Optional: [url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit]Exiled's Toolkit[/url] 0.4.0 or newer. With it, settings changed on the Mod Manager's Mods screen take effect without restarting the game. Without it, they take effect at the next game start.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Optional, to change settings without a restart: install Exiled's Toolkit the same way. Download its zip and extract it into the game folder.[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\TimeControl.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]TimeControl.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder. If centering the camera is still on G afterwards, set it back in the game's key settings.

[size=5]Configuration[/size]
To change settings, edit [font=Courier New]BepInEx\config\com.dev.survivallog.timecontrol.cfg[/font] while the game is closed, or do it in game on the Mod Manager's Mods screen, where changes take effect at once, all but [font=Courier New]SlowSpeeds[/font], [font=Courier New]FastSpeeds[/font], [font=Courier New]FastForwardSleep[/font], [font=Courier New]PauseToggleKey[/font], [font=Courier New]CenterCameraKey[/font] and [font=Courier New]TimeControlOnExpeditions[/font], which take effect at the next game start (see [url=https://github.com/AnExiledDev/SurvivalLog-TimeControl/blob/main/CONFIG.md]CONFIG.md[/url] for every setting).

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or leave a comment here. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\time-control.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].
Coming soon: a Survival Log wiki with detailed documentation for all of my mods.

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
[*]外出探索时，暂停期间暴露值的警报声还会继续响，但暴露值本身会停止上涨。[/*]
[*]游戏重绘速度按钮时，暂停标签可能会消失最多半秒。[/*]
[/list]

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.18902。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*]可选：[url=https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit]Exiled's Toolkit[/url] 0.4.0 或更新版本。装了它，在 Mod Manager 的「模组」界面改动的设置无需重启游戏即可生效；没装的话，改动在下次启动游戏时生效。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]可选（改动设置无需重启游戏）：用同样的方式安装 Exiled's Toolkit，下载它的压缩包并解压到游戏目录。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\TimeControl.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]TimeControl.dll[/font]。如果之后镜头对准角色的按键还在 G 上，请在游戏的按键设置里改回来。

[size=5]配置[/size]
如需修改设置，可以在游戏内 Mod Manager 的「模组」界面修改（改动立即生效，[font=Courier New]SlowSpeeds[/font]、[font=Courier New]FastSpeeds[/font]、[font=Courier New]FastForwardSleep[/font]、[font=Courier New]PauseToggleKey[/font]、[font=Courier New]CenterCameraKey[/font]、[font=Courier New]TimeControlOnExpeditions[/font] 在下次启动游戏时生效），也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.timecontrol.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-TimeControl/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\time-control.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。
即将推出：Survival Log 维基，收录我所有模组的详细说明。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-TimeControl]github.com/AnExiledDev/SurvivalLog-TimeControl[/url]
