using UnityEngine;

namespace RuneVault.Pieces
{
    /// <summary>
    /// Círculo en el suelo que muestra el alcance del núcleo o de un pilar, igual al del banco de trabajo.
    /// Se ve mientras el jugador tiene el martillo en modo construcción (cerca), y siempre en la pieza
    /// fantasma que se está colocando. Así se ve si un pilar o un cofre queda dentro de la red.
    /// </summary>
    public class RangeMarker : MonoBehaviour
    {
        private const float ShowDistance = 80f;

        private ZNetView _nview;
        private StorageCore _core;
        private GameObject _marker;
        private CircleProjector _projector;
        private int _tintedSegments = -1;

        // Núcleo azul, pilares morado: se distinguen de un vistazo (y del círculo naranjo del banco).
        private static readonly Color CoreColor = new Color(0.30f, 0.65f, 1.00f, 1f);
        private static readonly Color PillarColor = new Color(0.72f, 0.38f, 1.00f, 1f);

        private void Start()
        {
            _nview = GetComponent<ZNetView>();
            _core = GetComponent<StorageCore>();

            // Copia del círculo del banco de trabajo del juego.
            var bench = ZNetScene.instance ? ZNetScene.instance.GetPrefab("piece_workbench") : null;
            var station = bench ? bench.GetComponent<CraftingStation>() : null;
            if (!station || !station.m_areaMarker) return;

            _marker = Instantiate(station.m_areaMarker, transform);
            _marker.transform.localPosition = Vector3.zero;
            _marker.transform.localRotation = Quaternion.identity;
            _projector = _marker.GetComponentInChildren<CircleProjector>(true);
            if (_projector)
            {
                _projector.m_radius = CurrentRadius();
                _projector.m_nrOfSegments = Mathf.Clamp(Mathf.CeilToInt(_projector.m_radius * 4f), 20, 200);
            }
            _marker.SetActive(false);
        }

        private float CurrentRadius()
        {
            if (_core) return _core.Radius;
            return RuneVaultPlugin.PillarRadius.Value;
        }

        private void Update()
        {
            if (!_marker) return;

            bool ghost = !_nview || !_nview.IsValid();             // pieza que se está colocando
            var player = Player.m_localPlayer;
            bool show = ghost || (player && player.InPlaceMode() &&
                                  Vector3.Distance(player.transform.position, transform.position) < ShowDistance);

            if (_marker.activeSelf != show) _marker.SetActive(show);
            if (show && _projector) _projector.m_radius = CurrentRadius();
            if (show) Tint();
        }

        /// <summary>Pinta los segmentos del círculo (se crean de a poco, así que se repite si cambia la cantidad).</summary>
        private void Tint()
        {
            var renderers = _marker.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == _tintedSegments) return;
            _tintedSegments = renderers.Length;

            var color = _core ? CoreColor : PillarColor;
            foreach (var r in renderers)
            {
                var m = r.material;                       // copia propia: no afecta al banco de trabajo
                if (m.HasProperty("_Color")) m.SetColor("_Color", color);
                if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", color);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color);
            }
        }
    }
}
