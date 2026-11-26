# Yggdrasil Network

**One terminal for every chest you own.** Yggdrasil Network links your chests into a rune network that grows
with your progression: start with a small storage room in the Meadows and end with a vault you can reach from
anywhere in the world.

> Version **1.0.0**. Bug reports are very welcome — see [Reporting issues](#reporting-issues).
> ¿Hablas español? Ver [la sección en español](#español).

---

## Contents

- [Features](#features)
- [Installation](#installation)
- [How to use](#how-to-use)
- [Progression](#progression)
- [Multiplayer](#multiplayer)
- [Compatibility](#compatibility)
- [Configuration](#configuration)
- [Known limitations](#known-limitations)
- [Building from source](#building-from-source)
- [Reporting issues](#reporting-issues)
- [Credits](#credits)
- [License](#license)
- [Español](#español)

## Features

- **Storage Core** — links every chest inside its radius. One per world; upgrade it to grow the radius.
- **Access Terminal** — a panel inside your inventory (where a chest would open) showing the whole network:
  search, 7 categories, sort by amount / name / type, take any amount, store from your inventory.
- **Rune Pillars** — extend the network to other parts of your base. Pillars can be chained.
- **Linked Chests** — always part of the network, no matter where they are built.
- **Access Rune / Void Rune** — open the network away from home (2500 m / unlimited).
- **Abyssal Chest** — a huge 8×8 chest that is always part of the network.
- **Range circles** while building: blue for the core, purple for pillars.
- **Custom look** built in code, using the game's own materials: a pedestal with a floating crystal for the core,
  a rune tablet for the terminal, a spiked obelisk for the pillars, network traces and a hovering crystal on
  linked chests, and an imposing Abyssal Chest with horns and a rotating rune ring.
- **Server-authoritative**: chests are only modified by whoever owns them, so several players can use the
  network at the same time without duplicating or losing items.
- English and Spanish.

## Installation

### With a mod manager (recommended)

Install **Yggdrasil Network** from Thunderstore with r2modman, Gale or the Thunderstore Mod Manager.
Dependencies are installed automatically.

### Manual

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and
   [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Extract the package into `BepInEx/plugins/YggdrasilNetwork/` (keep the `Assets` folder next to the DLL).

On a server, the mod must be installed on the **server and every client**.

## How to use

1. Build a **Storage Core** near your chests (hammer → *Yggdrasil* tab).
2. Build an **Access Terminal** within the core's radius and press **E**.

| Action | Input |
|---|---|
| Choose how many to take | Click an item |
| Take one stack | Ctrl + click |
| Take one | Right click |
| Store an item from your inventory | Ctrl + click it, or drag & drop it on the panel |
| Store everything the network already holds | *Store what the network already has* |
| Upgrade the core | Look at it and press Shift + E with the materials |

Items go to chests that already hold the same item first; new items go to the first chest with a free slot.

## Progression

| Biome | Unlocks | Cost |
|---|---|---|
| Meadows / Black Forest | Storage Core, Access Terminal | 10 Wood + 1 Surtling core each |
| Swamp | Core radius 8 → 14 m | 20 Iron + Bonemass trophy |
| Swamp | Linked Chest (max 3) | 30 Iron + 20 Fine wood + Draugr trophy |
| Mountain | Core radius 14 → 20 m | 20 Silver + Moder trophy |
| Plains | Rune Pillar (+10 m each) | 20 Black metal + Goblin shaman trophy |
| Mistlands | Linked Chest limit 3 → 10 | Defeat the Queen |
| Ashlands | Access Rune (2500 m) | 10 Flametal + Fader trophy · Galdr table |
| Deep North | Void Crystal → Void Rune (unlimited) | 4 Deep North gemstones → crystal + 20 Bloodgold · Galdr table |
| Deep North | Abyssal Chest (max 1) | 30 Frostwood + Hexen trophy |

Trophies spent on core upgrades and runes are consumed — want the boss power too? Fight it again. Pieces built
with a trophy (pillar, linked and abyssal chests) give it back when deconstructed.

## Multiplayer

- The version is checked on connect: everyone needs the same version.
- Chests nobody has loaded are read and written by the server; chests loaded by a player are changed by that
  player's game. Requests are routed automatically.
- Private chests and other players' **wards** are respected, exactly as when opening a chest by hand.
- Gameplay settings (radii, upgrade costs, pillar range) are **synced from the server**.
- If a chest is still assigned to a player who walked or teleported away, the server takes it over, so far
  requests never get lost.
- Chest limits (linked chests, one Abyssal Chest, one core) count everyone's chests, not only the ones you can see.
- The server keeps its own list of network pieces instead of scanning the whole world on every request, and
  far chests are only reloaded when their contents change.

## Compatibility

| Mod | Status |
|---|---|
| MultiUserChest | Supported — chests open by other players can still be used. |
| Quick Stack - Store - Sort - Trash - Restock | Integrated — quick stacking next to a terminal also fills network chests, including far linked chests. Favorites, hotbar and equipped items are respected. |
| Crafting-from-chests mods (Path of Valheiman, AzuCraftyBoxes, Craft From Containers…) | Compatible. Yggdrasil Network does **not** craft from chests, so nothing is consumed twice. |
| Path of Valheiman | Tested together. Its rune chest is never added to the network. |

World-generated chests (dungeons, ruins, villages) are never part of the network.

## Configuration

`BepInEx/config/jeanne.valheim.yggdrasilnetwork.cfg`, or in game with the Configuration Manager (F1).

| Section | Setting | Default | Synced |
|---|---|---|---|
| Network | Core radius | 8 m | ✔ |
| Network | Chests that join (`ConPermiso` / `SoloPropios`) | ConPermiso | per player |
| Progression | Radius level 1 / level 2 | 14 m / 20 m | ✔ |
| Progression | Cost level 1 / level 2 | `Iron:20,TrophyBonemass:1` / `Silver:20,TrophyDragonQueen:1` | ✔ |
| Progression | Rune pillar radius | 10 m | ✔ |
| Debug | Detailed log | off | per player |

## Known limitations

- Linked and Abyssal Chests keep the vanilla black metal chest model (so the lid still opens) with extra
  decoration on top.
- With **Path of Valheiman**, the runes and the Void Crystal appear under *Other* at the Galdr table.
- A far chest loaded by another player is handled by that player's game: if they disconnect at that exact
  moment, the request is lost (no items are lost — nothing moves).
- While the network is open with a rune, runes can't be stored (so you never lock your way home in a chest).

## Building from source

Requirements: Windows, Valheim with BepInEx + Jötunn (a mod manager profile works), and the .NET SDK.

```
git clone https://github.com/JeanneDaRin/yggdrasilnetwork.git
cd yggdrasilnetwork
dotnet build -c Release
```

The build copies the plugin into your profile and creates `bin/Release/YggdrasilNetwork-<version>.zip`,
ready to upload to Thunderstore. Paths can be changed with `-p:ValheimDir=...` and `-p:ProfileDir=...`.
See [DEVELOPMENT.md](DEVELOPMENT.md) for the project layout.

## Reporting issues

Open an issue on [GitHub](https://github.com/JeanneDaRin/yggdrasilnetwork/issues) with:

1. What you did and what happened.
2. Single player, hosted game or dedicated server.
3. Your `BepInEx/LogOutput.log` (enable *Debug → Detailed log* first if it is about far chests or runes).

## Credits

- Built with [BepInEx](https://github.com/BepInEx/BepInEx), [Harmony](https://github.com/pardeike/Harmony)
  and [Jötunn](https://github.com/Valheim-Modding/Jotunn).
- Author: JeanneDaRin.

## License

[MIT](LICENSE) © 2026 JeanneDaRin

---

## Español

**Una terminal para todos tus cofres.** Yggdrasil Network une tus cofres en una red rúnica que crece con la
progresión: empieza con una bodega pequeña en la Pradera y termina con una bóveda a la que llegas desde
cualquier parte del mundo. El juego incluye todos los textos en español.

### Qué trae

- **Núcleo de almacenamiento:** enlaza los cofres dentro de su radio. Uno por mundo; se mejora para crecer.
- **Terminal de acceso:** un panel dentro del inventario (donde se abre un cofre) con toda la red: buscador,
  7 categorías, orden por cantidad, nombre o tipo, sacar la cantidad que quieras y guardar desde tu inventario.
- **Pilares rúnicos:** extienden la red a otras partes de la base y se pueden encadenar.
- **Cofres vinculados** y **Cofre abisal** (8×8): siempre forman parte de la red, estén donde estén.
- **Runa de acceso** (2.500 m) y **Runa del Vacío** (sin límite): abren la red lejos de casa.
- Círculos de alcance al construir (azul el núcleo, morado los pilares).
- **Aspecto propio**, hecho por código con los materiales del juego: pedestal con cristal flotante (núcleo),
  losa rúnica (terminal), obelisco con púas (pilar), trazas de red y cristal flotante en los cofres vinculados,
  y un cofre abisal imponente con cuernos y un anillo de runas que gira.
- Multijugador seguro: cada cofre lo modifica solo su dueño, así que varios jugadores pueden usar la red a la
  vez sin duplicar ni perder ítems. Respeta guardas y cofres privados.

### Cómo se usa

| Acción | Control |
|---|---|
| Elegir cuánto sacar | Clic en un ítem |
| Sacar un stack | Ctrl + clic |
| Sacar uno | Clic derecho |
| Guardar un objeto del inventario | Ctrl + clic, o arrastrarlo y soltarlo sobre el panel |
| Guardar todo lo que ya existe en la red | *Guardar lo que ya hay en la red* |
| Mejorar el núcleo | Mirarlo y Shift + E con los materiales |

### Progresión

| Bioma | Desbloquea | Costo |
|---|---|---|
| Pradera / Selva negra | Núcleo y Terminal | 10 madera + 1 núcleo de surtling cada uno |
| Pantano | Radio 8 → 14 m · Cofre vinculado (máx. 3) | 20 hierro + trofeo de Bonemass · 30 hierro + 20 madera fina + trofeo de draugr |
| Montaña | Radio 14 → 20 m | 20 plata + trofeo de Moder |
| Llanuras | Pilar rúnico (+10 m cada uno) | 20 metal negro + trofeo de chamán goblin |
| Tierras brumosas | Límite de cofres vinculados 3 → 10 | Derrotar a la Reina |
| Ashlands | Runa de acceso | 10 flametal + trofeo de Fader (banco de magia) |
| Deep North | Cristal y Runa del Vacío · Cofre abisal | 4 gemas + 20 oro de sangre · 30 madera escarchada + trofeo de la Hexen |

Los trofeos de las mejoras del núcleo y de las runas se gastan: si quieres el poder del jefe, toca enfrentarlo
otra vez. Las piezas que llevan trofeo (pilar, cofres vinculado y abisal) lo devuelven al desarmarlas.

Para instalación, compatibilidad, configuración y reportes de errores, ver las secciones en inglés más arriba.
