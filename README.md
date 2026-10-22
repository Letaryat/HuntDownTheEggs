<div align="center">
    <img alt="header_banner" src="/repoAssets/banner.png"/>
    <p>HuntDownTheEggs is a plugin that spawns collectables and allows players to collect them.</p>
    <h2>
        <a href="https://discord.com/invite/mEmdyqM3Um" target="_blank"><img src="https://img.shields.io/badge/Discord%20Server-7289da?style=for-the-badge&logo=discord&logoColor=white" /></a>
        <a href="https://ko-fi.com/letaryat" target="_blank"><img src="https://img.shields.io/badge/Ko--fi-F16061?logo=ko-fi&logoColor=white&style=for-the-badge" /></a>
    </h2>
</div>

## [🛠️] Features
- **Placeable Collectables** – Server owners can place collectables at specific locations across the map.
- **Random Spawning** – Collectables can spawn at random positions using the map's NavMesh.
- **Round-Based Spawning** – Collectables spawn each round, keeping gameplay fresh and engaging.
- **Custom Rewards & Rarities** – Configure different rewards and rarity levels for collecting eggs.
- **Developer API** – Includes a dedicated API for developers to integrate with other plugins.
- **Collection Statistics** – Tracks collected eggs and stores collection data in tables for statistics and further use.
- **Multiple Collection Methods** – Players can collect eggs by shooting prop models or walking into them, depending on the server configuration.

> [!TIP]
> If you notice any problems with plugin please create issue on github. If you have any questions, please ask me on discord server.

## [📌] Dependiencies
- [CounterStrikeSharp](https://swiftlys2.net/)

## [🛠️] Base Setup
- Download latest [release](https://github.com/letiPoorPlugins/HuntDownTheEggs_SW/releases)
- Drag files to /addons/counterstrikesharp/,
- Restart your server,
- Config file should be created in configs/plugins/HuntDownTheEggs,
- Configure to your liking,
- Restart server

## [⚙️] Commands

### Admin Commands
| Command | Description |
|---|---|
| `!eggplace` | Places an egg at the selected location. |
| `!tpegg` | Teleports the admin to an egg. |
| `!removeegg` | Removes an egg. |
| `!reloadeggs` | Reloads the eggs for the current map. |
| `!placingmode` | Toggles egg placement mode on or off. |

### Player Commands
| Command | Description |
|---|---|
| `!myeggs` | Displays the eggs collected by the player. |
| `!topplaced` | Shows the leaderboard for collected placed eggs on the current map. |
| `!toprandom` | Shows the leaderboard for collected randomly spawned eggs on the current map. |
| `!topkills` | Shows the leaderboard for collected kill eggs on the current map. |
| `!topeggsmap` | Shows the leaderboard for all collected egg types on the current map. |
| `!topplacedall` | Shows the leaderboard for collected placed eggs across all maps. |
| `!toprandomall` | Shows the leaderboard for collected randomly spawned eggs across all maps. |
| `!topkillsall` | Shows the leaderboard for collected kill eggs across all maps. |
| `!topeggsall` | Shows the leaderboard for all collected egg types across all maps. |


## [🚨] Config setup

> [!CAUTION]
> In theory plugin should work with all types of databases, but only MySQL was tested.

```json
{
  "DBSetup": {
    "DBHost": "localhost", # Database host,
    "DBPort": 3306, # Database port,
    "DBUsername": "root", # Database username,
    "DBName": "db_", # Database name,
    "DBPassword": "123" # Database password
  },
  "EggSetup": {
    "EggRootPermission": "@egg/root", # Permission required to use root/admin commands,
    "EggModel": "models/huntdowntheeggs/pumpkin.vmdl", # VMDL model used for collectables,
    "TriggerModel": "models/huntdowntheeggs/pumpkin.vmdl", # VMDL model used for collectable triggers,
    "EggAnimation": "huntdowntheEggs_rotateAction", # Idle animation for models that support it,
    "EggCollectAnimation": "huntdowntheEggs_collect", # Collect animation for models that support it,
    "EggModelHeight": 0, # Vertical offset applied to the model,
    "EggModelScale": 1, # Model scale,
    "Glowing": true, # Whether collectables should glow,
    "GlowingColor": "Red", # Collectable glow color,
    "GlowingRange": 1500, # Maximum glow visibility range,
    "HidePickedEggsForPlayer": false, # Hide admin-placed eggs from players who have already collected them,
    "KillEggOnlyForKiller": false # Allow only the killer to collect kill eggs
  },
  "ModesSetup": {
    "SpawnEggsOnWarmup": false, # Whether eggs should spawn during warmup,
    "DeathMode": true, # Whether eggs should spawn after a player dies,
    "SearchMode": true, # Whether admin-placed eggs defined in map JSON files should spawn,
    "PickUpMode": true, # Allow players to collect eggs by walking into them,
    "ShootEggMode": true, # Allow players to collect eggs by shooting them,
    "ShootEggHealth": 100, # Health of shootable eggs,
    "SpawnDeathEggOnVictim": true, # Spawn kill eggs at the victim's position instead of the attacker's,
    "ChanceToSpawn": 100, # Percentage chance for a kill egg to spawn after a player dies,
    "RemoveOnFind": false, # Remove admin-placed eggs after they have been collected,
    "SpawnPlacedEggsOnce": false, # Requires RemoveOnFind: true; prevents collected placed eggs from spawning again during the same match,
    "SpawnRandomEggs": true, # Enable random egg spawning using the map's NavMesh,
    "SpawnRandomEggsOnWarmup": true, # Allow random eggs to spawn during warmup,
    "MinPlayersToSpawnRandomEggs": 1, # Minimum number of online players required to spawn random eggs,
    "NumberOfRandomEggs": 50, # Number of random eggs to spawn each round,
    "SpawnRandomEggsByPercentageOfPlayers": false # Scale the number of random eggs according to the number of online players
  },
  "PrizeSetup": {
    "ReceivePrize": true, # Enable or disable rewards for collecting eggs,
    "PresentTypes": {
      "common": { # Reward rarity name,
        "Chance": 30, # Chance of receiving this rarity,
        "Color": "green", # Chat color for this rarity,
        "Rewards": { # Rewards defined as console commands,
          "Example reward": "say Common prize for {NAME}" # Reward name and command; {NAME} is replaced with the player's name
        }
      },
      "rare": {
        "Chance": 70,
        "Color": "red",
        "Rewards": {
          "Rare example reward": "say Rare prize for {NAME}"
        }
      }
    }
  },
  "CommandsSetup": {
    "EnableCommands": true, # Enable or disable plugin commands,
    "PlaceEggCommands": [ # Admin command to place eggs,
      "eggplace"
    ],
    "TeleportToEggCommands": [ # Admin command to teleport to an egg,
      "tpegg"
    ],
    "RemoveEggCommands": [ # Admin command to remove an egg,
      "removeegg"
    ],
    "ReloadEggsCommands": [ # Admin command to reload eggs,
      "reloadeggs"
    ],
    "PlacingModeCommands": [ # Admin command to toggle egg placement mode,
      "placingmode"
    ],
    "ShowMyEggsCommands": [ # Player command to display collected egg statistics,
      "myeggs"
    ],
    "ShowTopPlacedEggsPerMapCommands": [ # Player leaderboard for placed eggs collected on the current map,
      "topplaced"
    ],
    "ShowTopRandomEggsPerMapCommands": [ # Player leaderboard for randomly spawned eggs collected on the current map,
      "toprandom"
    ],
    "ShowTopKillEggsPerMapCommands": [ # Player leaderboard for kill eggs collected on the current map,
      "topkills"
    ],
    "ShowTopAllEggsPerMapCommands": [ # Player leaderboard for all eggs collected on the current map,
      "topeggsmap"
    ],
    "ShowTopPlacedEggsCommands": [ # Player leaderboard for placed eggs collected across all maps,
      "topplacedall"
    ],
    "ShowTopRandomEggsCommands": [ # Player leaderboard for randomly spawned eggs collected across all maps,
      "toprandomall"
    ],
    "ShowTopKillEggsCommands": [ # Player leaderboard for kill eggs collected across all maps,
      "topkillsall"
    ],
    "ShowTopAllEggsCommands": [ # Player leaderboard for all eggs collected across all maps,
      "topeggsall"
    ]
  },
  "DebugNavMesh": true, # Enable NavMesh debug logs,
  "Debug": true, # Enable plugin debug logs,
  "ConfigVersion": 1 # Configuration version; do not change manually
}
```

> [!TIP]
> Reward descriptions support dynamic translations. To add a custom description, add a new entry to lang/*language*.json using the key format recievedDesc.Reward_Name. Replace spaces in the reward name with underscores (_). Example: For a reward named Punkty sklepu, add the following entry:
```
  "hdte.received_desc.VIP": "[gold]★ [white]Enjoy your VIP benefits!",
```

> [!NOTE]
> This plugin also provides SharedAPI for developers. 
- [ApiExamples](/ApiExamples/)
- [Modules](/Modules/)

## [💥] Model
[HuntDownTheEggs plugin now also comes in with few models depending on holiday.](https://github.com/letiPoorPlugins/HuntDownTheEggs_Models)  

## [💖] Special thanks to:
- [Exkludera Gift Packages](https://github.com/exkludera/cs2-gift-packages) - Basically yoinked the whole idea of gifts and how to make presents/triggers/hookentityoutput,
- [Mesharsky](https://github.com/Mesharsky/) - Complete plugin structure rework and for fixing issues.
- [CS2-Ranks](https://github.com/partiusfabaa/cs2-ranks), [SimpleAdmin](github.com/daffyyyy/CS2-SimpleAdmin) used as an examples of Dapper and MySQLConnector, 
- [Dliix66](https://discord.com/channels/1160907911501991946/1311638450881167523/1311973008122052618) - Method for glow effect,
- [21Joakim](https://gist.github.com/21Joakim/6a264623e4ac8ae217e0eb15fc43e3e5#file-navmesh-cs) - Navmesh,
- [oqyh / GoldKingZ](https://github.com/oqyh/cs2-Prop-Health-GoldKingZ) - OnTakeDamage function for props,
- CounterStrikeSharp Discord,

## [🚨] 
Plugin is written by me. That means it might be poorly written and have some issues. Sometimes I have no idea what am I doing but when tested it works.
It was written using a lot of research and also a chatgpt when I was stuck. Any help and or advice on the code is welcome. Thanks!
