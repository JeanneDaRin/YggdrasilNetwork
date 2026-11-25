# Rune Vault

Mod de Valheim (BepInEx + Jötunn) que une cofres en una red de almacenamiento.
Diseño: ver el documento "Rune Vault — propuesta de mod para Valheim".

## Compilar

```
dotnet build -c Release
```

El DLL se copia solo a `%APPDATA%\Thunderstore Mod Manager\DataFolder\Valheim\profiles\nibba\BepInEx\plugins\RuneVault`.
Después abre Valheim desde Thunderstore Mod Manager (perfil `nibba`) y busca "Rune Vault" en `BepInEx\LogOutput.log`.

Si cambia la ruta de Valheim o el perfil:

```
dotnet build -c Release -p:ValheimDir="D:\otra\ruta\Valheim" -p:ProfileDir="C:\...\profiles\otro"
```

## Mods del perfil a tener en cuenta

- **StashLink** (crafteo desde cofres cercanos): si está, Rune Vault no descuenta materiales por su cuenta.
- **MultiUserChest**: mover ítems entre cofres de la red debe pasar por su sistema para no desincronizar.
- **Path of Valheiman**: trae su propio crafteo/construcción desde cofres (maestría de conveniencia, radio con Shift+rueda en el banco), "Chest Feed" que alimenta fundiciones desde el cofre más cercano, y filas extra en la mochila. Rune Vault no descuenta materiales si está presente, no debe vaciar el cofre que alimenta una fundición sin que el jugador lo pida, y debe respetar el tamaño de inventario que dé el mod.
- **Quick Stack Store Sort Trash Restock**: se solapa con "Depositar todo"; no deben pelear por el mismo cofre.
