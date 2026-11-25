# Yggdrasil Network — notas de desarrollo

> El proyecto se llamaba *Rune Vault*. Los identificadores internos (prefabs `piece_runevault_*`, claves `runevault_*`
> y mensajes `RuneVault_*`) se mantienen a propósito: cambiarlos borraría las piezas ya construidas en los mundos.

## Compilar

```
dotnet build -c Release
```

El DLL y la carpeta `Assets` se copian solos al perfil de Thunderstore Mod Manager
(`%APPDATA%\Thunderstore Mod Manager\DataFolder\Valheim\profiles\nibba\BepInEx\plugins\YggdrasilNetwork`),
y se genera el paquete listo para subir en `bin\Release\YggdrasilNetwork-<versión>.zip`.

Si cambia la ruta de Valheim o el perfil:

```
dotnet build -c Release -p:ValheimDir="D:\otra\ruta\Valheim" -p:ProfileDir="C:\...\profiles\otro"
```

## Estructura

- `src/Pieces` — núcleo, terminal, pilar, cofres vinculado y abisal, círculos de alcance.
- `src/Network` — red de cofres, mensajes al dueño de cada cofre (ChestRpc), vista del servidor para cofres lejanos (LinkedChests), compatibilidad con Quick Stack.
- `src/UI` — panel de la terminal (uGUI, dentro del inventario).
- `src/Items` — Runa de acceso, Cristal y Runa del Vacío.
- `src/I18n` — textos en inglés y español.
- `Assets` — íconos PNG que viajan con el DLL. `Art` — fuentes SVG de los íconos (placeholder).
- `Thunderstore` — `manifest.json` e `icon.png` del paquete.

## Mods del perfil a tener en cuenta

- **Path of Valheiman**: trae crafteo desde cofres, Chest Feed para fundiciones y filas extra en la mochila. Rune Vault no craftea.
- **StashLink**: crafteo desde cofres (en este perfil no funciona con la versión actual del juego).
- **MultiUserChest**: Rune Vault le pide los cambios al dueño de cada cofre, igual que ese mod.
- **Quick Stack Store Sort Trash Restock**: integración opcional (el quick stack también reparte en la red).

`src/Network/InventoryDropPatch.cs` y `src/RuneVaultPlugin.cs` ya no se usan y se pueden borrar.
