using System.Collections.Generic;
using UnityEngine;
using static YggdrasilNetwork.I18n.Texts;

namespace YggdrasilNetwork.Pieces
{
    /// <summary>
    /// Pilar rúnico: extiende la red. Se conecta si está dentro del radio del núcleo o de otro pilar
    /// ya conectado (se pueden encadenar), y suma su propio radio a la red.
    /// </summary>
    public class RunePillar : MonoBehaviour, Hoverable
    {
        /// <summary>Pilares construidos y cargados en el mundo.</summary>
        internal static readonly List<RunePillar> All = new List<RunePillar>();

        private void Start()
        {
            var nview = GetComponent<ZNetView>();
            if (nview && nview.IsValid()) All.Add(this);   // el fantasma del martillo no cuenta
        }

        private void OnDestroy() => All.Remove(this);

        public string GetHoverName() => T("rv_pillar_name");

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            bool connected = StorageCore.FindFor(transform.position);
            string radius = YggdrasilPlugin.PillarRadius.Value.ToString("0");
            return $"{GetHoverName()}\n" + (connected
                ? $"<color=#9cf09c>{T("rv_pillar_connected", radius)}</color>"
                : $"<color=red>{T("rv_pillar_disconnected")}</color>");
        }
    }
}
