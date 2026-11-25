using RuneVault.UI;
using UnityEngine;
using static RuneVault.I18n.Texts;

namespace RuneVault.Pieces
{
    /// <summary>Terminal de acceso: abre la ventana de la red del núcleo más cercano.</summary>
    public class AccessTerminal : MonoBehaviour, Hoverable, Interactable
    {
        /// <summary>Terminales construidas y cargadas en el mundo.</summary>
        internal static readonly System.Collections.Generic.List<AccessTerminal> All =
            new System.Collections.Generic.List<AccessTerminal>();

        private void Start()
        {
            var nview = GetComponent<ZNetView>();
            if (nview && nview.IsValid()) All.Add(this);   // el fantasma del martillo no cuenta
        }

        private void OnDestroy() => All.Remove(this);

        public string GetHoverName() => T("rv_terminal_name");

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            if (!StorageCore.FindFor(transform.position))
                return $"{GetHoverName()}\n<color=red>{T("rv_no_core")}</color>";
            return Localization.instance.Localize(
                $"{GetHoverName()}\n[<color=yellow><b>$KEY_Use</b></color>] {T("rv_open")}");
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !(user is Player player)) return false;
            if (TerminalWindow.IsOpen) return true;   // apretar E varias veces no la reabre

            var core = StorageCore.FindFor(transform.position);
            if (!core)
            {
                player.Message(MessageHud.MessageType.Center, T("rv_no_core"));
                return false;
            }
            TerminalWindow.Open(this, core);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    }
}
