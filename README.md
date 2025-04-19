# HuntDownTheEggs
<p align="center">
    <img src="img/huntdown.jpg" width="200">
</p>
A simple Easter egg hunt plugin for Counter-Strike 2 servers using CounterStrikeSharp. <br>
This plugin allows server owners to place custom easter eggs around the map (which are spawned on round start), drop them when a player is killed, or both. Players can collect these eggs, with all data stored in a MySQL database.<br>
Additionally, server owners can configure the eggs to grant random rewards by executing custom commands when an egg is picked up. Rewards can be assigned different rarity levels, each with its own drop chance.<br>

## [📌] Dependiencies
- [Metamod](https://www.sourcemm.net/)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)  

## [📌] Setup
- Install all dependiencies listed upwards,
- Download latest [release](https://github.com/Letaryat/HuntDownTheEggs/releases)
- Drag files to /plugins/
- Restart your server
- Config file should be created in configs/plugins/HuntDownTheEggs,
- After setting up the database restart your server again

If you have any more problems or would like to know more, [please visit wiki.](https://github.com/Letaryat/HuntDownTheEggs/wiki)

## [💖] Special thanks to:
- [Exkludera Gift Packages](https://github.com/exkludera/cs2-gift-packages) - Basically yoinked the whole idea of gifts and how to make presents/triggers/hookentityoutput,
- [CS2-Ranks](https://github.com/partiusfabaa/cs2-ranks), [SimpleAdmin](github.com/daffyyyy/CS2-SimpleAdmin) used as an examples of Dapper and MySQLConnector, 
- CounterStrikeSharp Discord,

### [💥] Model
There is a basic easter egg model that you can use in your addons by using MultiAddonManager.

## [🚨] 
Plugin is written by me. That means it might be poorly written and have some issues. Sometimes I have no idea what am I doing but when tested it works.
It was written using a lot of research and also a chatgpt when I was stuck. Any help and or advice on the code is welcome. Thanks!