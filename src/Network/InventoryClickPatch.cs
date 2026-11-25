using HarmonyLib;
using YggdrasilNetwork.UI;

namespace YggdrasilNetwork.Network
{
    /// <summary>
    /// Con la terminal abierta, Ctrl+clic en un objeto de tu inventario lo guarda en la red
    /// (igual que mover a un cofre abierto). El resto de los clics siguen siendo los del juego.
    /// Parámetros por posición: OnSelectedItem(InventoryGrid grid, ItemData item, Vector2i pos, Modifier mod).
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    internal static class InventoryClickPatch
    {
        private static bool Prefix(InventoryGui __instance, InventoryGrid __0, ItemDrop.ItemData __1, InventoryGrid.Modifier __3)
        {
            if (!TerminalWindow.IsOpen || __1 == null) return true;
            if (__0 != __instance.m_playerGrid) return true;
            if (__3 != InventoryGrid.Modifier.Move && __3 != InventoryGrid.Modifier.Drop) return true;

            TerminalWindow.StoreFromInventory(__1);
            return false;
        }
    }
}
