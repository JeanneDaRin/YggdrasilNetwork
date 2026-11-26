# Changelog

## 1.0.0 — first release

- Storage Core with 3 radius levels (stays upgraded if rebuilt) and one core per world.
- Access Terminal inside the inventory: search, categories, sorting, take any amount, store by Ctrl+click or drag & drop.
- Rune Pillars that chain to extend the network.
- Linked Chests (3, or 10 after the Queen) and the Abyssal Chest, always part of the network.
- Access Rune (2500 m) and Void Rune (unlimited), with the Void Crystal intermediate.
- Server-side access to unloaded chests; requests routed to the chest owner when loaded by another player.
- Wards and private chests respected; world-generated chests excluded.
- Server-synced gameplay settings.
- Quick Stack - Store - Sort - Trash - Restock integration; MultiUserChest support.
- Custom models built in code with the game's materials: core, terminal, rune pillar, runes and crystal;
  linked and abyssal chests decorated on top of the black metal chest.
- Runes and the Void Crystal are listed under *Other* (also in Path of Valheiman's crafting window).
- Pieces built with a trophy return it when deconstructed.
- Runes can't be stored while the network is open with a rune.
- Quick stack next to a terminal reaches far linked chests too.
- Server keeps an index of network pieces (no full-world scan per request); far chests reload only when changed.
- Chest limits count every player's chests; the server takes over far chests left assigned to absent players.
- Icons embedded in the DLL.
- English and Spanish.
