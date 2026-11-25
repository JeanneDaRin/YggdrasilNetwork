using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using RuneVault.Network;
using RuneVault.Pieces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static RuneVault.I18n.Texts;

namespace RuneVault.UI
{
    /// <summary>
    /// Panel de la terminal de acceso, hecho con la interfaz de Valheim (uGUI).
    /// Vive dentro del inventario del juego, en el lugar donde aparece un cofre abierto: se anima con él,
    /// queda fijo en cualquier resolución y el objeto arrastrado y el cuadro de dividir se ven por encima.
    ///
    /// Sacar: clic = elegir cantidad (si hay 1, directo) · Ctrl+clic = stack · clic derecho = uno.
    /// Guardar: Ctrl+clic en tu inventario, soltar un objeto arrastrado sobre el panel,
    /// o el botón "Guardar lo que ya hay en la red".
    /// </summary>
    internal class TerminalWindow : MonoBehaviour
    {
        internal enum Category { Todo, Materiales, Comida, Armas, Armaduras, Herramientas, Otros }
        private enum SortMode { Cantidad, Nombre, Tipo }

        private const float MaxDistance = 6f;
        private const float RefreshSeconds = 0.5f;
        private const float OpenGraceSeconds = 0.4f;
        private const float Pad = 12f;
        private const float CellSize = 56f;
        private const float ExtraHeight = 150f;   // el panel es más alto que el de un cofre

        private static TerminalWindow _instance;

        private static readonly AccessTools.FieldRef<InventoryGui, ItemDrop.ItemData> DragItem =
            AccessTools.FieldRefAccess<InventoryGui, ItemDrop.ItemData>("m_dragItem");
        private static readonly AccessTools.FieldRef<InventoryGui, Inventory> DragInventory =
            AccessTools.FieldRefAccess<InventoryGui, Inventory>("m_dragInventory");
        private static readonly AccessTools.FieldRef<InventoryGui, int> DragAmount =
            AccessTools.FieldRefAccess<InventoryGui, int>("m_dragAmount");

        // Estado
        private AccessTerminal _terminal;
        private StorageCore _core;
        private bool _remote;
        private bool _open;
        private float _openedAt;
        private float _nextRefresh;
        private StorageNetwork _network;
        private List<NetworkItem> _items = new List<NetworkItem>();
        private Category _category;
        private SortMode _sort;
        private int _blockUntilFrame;
        private bool _inputBlocked;

        // Interfaz
        private GameObject _panel;
        private Text _chestsText;
        private InputField _search;
        private Text _footer;
        private RectTransform _gridContent;
        private readonly List<Text> _categoryLabels = new List<Text>();
        private readonly List<Text> _sortLabels = new List<Text>();
        private readonly List<ItemCell> _cells = new List<ItemCell>();

        // Cuadro "¿cuánto sacar?"
        private GameObject _dialog;
        private Text _dialogTitle;
        private InputField _dialogAmount;
        private Text _dialogTakeLabel;
        private NetworkItem _pending;

        internal static bool IsOpen => _instance && _instance._open;

        internal static void Create(GameObject host)
        {
            if (!_instance) _instance = host.AddComponent<TerminalWindow>();
        }

        // ================= Abrir / cerrar =================

        internal static void Open(AccessTerminal terminal, StorageCore core)
        {
            if (!_instance || !InventoryGui.instance) return;
            var w = _instance;
            w._terminal = terminal;
            w._core = core;
            w._remote = false;
            w.ShowPanel();
        }

        // ---------- Runa de acceso: abrir la red de lejos ----------

        /// <summary>Distancia máxima al núcleo o a un pilar conectado para usar la Runa de acceso.</summary>
        internal const float RemoteRange = 2500f;
        private bool _remotePending;
        private bool _remoteUnlimited;     // Runa del Vacío: sin límite de distancia

        private float CurrentRemoteRange => _remoteUnlimited ? float.PositiveInfinity : RemoteRange;

        /// <summary>
        /// Abre la red desde cualquier lugar a menos de 2.500 m del núcleo o de un pilar.
        /// Primero pide al servidor el estado actualizado; se abre cuando llega la respuesta.
        /// </summary>
        internal static void OpenRemote(bool unlimited = false)
        {
            if (!_instance || !InventoryGui.instance || IsOpen) return;
            _instance._remotePending = true;
            _instance._remoteUnlimited = unlimited;
            LinkedChests.Query(true);
        }

        private void TryFinishRemoteOpen()
        {
            if (!_remotePending) return;
            _remotePending = false;
            var player = Player.m_localPlayer;
            if (!player) return;

            if (!LinkedChests.HasCore)
            {
                player.Message(MessageHud.MessageType.Center, T("rv_no_core_world"));
                return;
            }
            float distance = LinkedChests.DistanceToNetwork(player.transform.position);
            if (distance > CurrentRemoteRange)
            {
                player.Message(MessageHud.MessageType.Center, T("rv_remote_too_far", distance.ToString("0"), RemoteRange.ToString("0")));
                return;
            }

            _terminal = null;
            _core = StorageCore.All.FirstOrDefault(c => c);   // si el núcleo está cargado cerca, se usa
            _remote = true;
            ShowPanel();
        }

        private void ShowPanel()
        {
            var w = this;
            w._open = true;
            w._openedAt = Time.time;
            w._nextRefresh = 0f;

            InventoryGui.instance.Show(null, 1);
            if (!w._panel || !w._dialog)
            {
                try { w.Build(); }
                catch (System.Exception e)
                {
                    RuneVaultPlugin.Log.LogError("No se pudo construir el panel de la terminal: " + e);
                    if (w._panel) Destroy(w._panel);
                    w._panel = null;
                    w._open = false;
                    return;
                }
            }
            w._search.text = "";                 // cada apertura empieza sin búsqueda
            w._category = Category.Todo;
            w.HideDialog();
            w.UpdateTabLabels();
            w._panel.SetActive(true);
        }

        private void Close()
        {
            if (!_open) return;
            _open = false;
            _pending = null;
            _network = null;
            _items.Clear();
            if (_panel) _panel.SetActive(false);
            SetInputBlocked(false);
            if (InventoryGui.IsVisible() && InventoryGui.instance) InventoryGui.instance.Hide();
        }

        internal static void StoreDraggedFromCursor()
        {
            if (IsOpen) _instance.TryStoreDragged();
        }

        internal static void NotifyChanged()
        {
            if (!_instance) return;
            _instance._nextRefresh = 0f;
            _instance.TryFinishRemoteOpen();
        }

        internal static void StoreFromInventory(ItemDrop.ItemData item, int amount = -1)
        {
            if (!IsOpen) return;
            _instance.Store(item, amount <= 0 ? item.m_stack : amount);
        }

        private void Update()
        {
            if (!_open) return;

            var player = Player.m_localPlayer;
            bool inventoryClosed = Time.time - _openedAt > OpenGraceSeconds && !InventoryGui.IsVisible();
            bool lostSource = !player || (_remote
                ? LinkedChests.DistanceToNetwork(player.transform.position) > CurrentRemoteRange
                : !_terminal || !_core || Vector3.Distance(player.transform.position, _terminal.transform.position) > MaxDistance);
            if (!player || player.IsDead() || !_panel || !_dialog || inventoryClosed || lostSource)
            {
                Close();
                return;
            }

            // Cuadro de cantidad: Esc lo cierra (solo a él), Enter confirma.
            if (_dialog.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) HideDialog();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) ConfirmDialog();
            }

            // Mientras se escribe (o el cuadro está abierto) el personaje no se mueve y Esc/Tab no cierran el inventario.
            if (_search.isFocused || _dialog.activeSelf || (_dialogAmount && _dialogAmount.isFocused))
                _blockUntilFrame = Time.frameCount + 2;
            SetInputBlocked(Time.frameCount <= _blockUntilFrame);

            if (Time.time >= _nextRefresh) Refresh(player);
        }

        private void Refresh(Player player)
        {
            _nextRefresh = Time.time + RefreshSeconds;
            _network = StorageNetwork.For(_core, player.GetPlayerID());
            _items = _network.AggregateItems();
            _chestsText.text = T("rv_chests", _network.ChestCount);
            RedrawGrid();
        }

        private void SetInputBlocked(bool block)
        {
            if (block == _inputBlocked) return;
            _inputBlocked = block;
            GUIManager.BlockInput(block);
        }

        // ================= Construcción del panel =================

        private void Build()
        {
            if (_panel) Destroy(_panel);
            _cells.Clear();
            var gui = InventoryGui.instance;
            var container = gui.m_container;

            _panel = new GameObject("RuneVaultPanel", typeof(RectTransform));
            var rt = (RectTransform)_panel.transform;
            rt.SetParent(container.parent, false);
            rt.anchorMin = container.anchorMin;
            rt.anchorMax = container.anchorMax;
            rt.pivot = new Vector2(container.pivot.x, 1f);
            rt.anchoredPosition = container.anchoredPosition + new Vector2(0f, container.rect.height * (1f - container.pivot.y));
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, container.rect.width);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, container.rect.height + ExtraHeight);
            rt.SetSiblingIndex(container.GetSiblingIndex() + 1);

            // El cuadro de dividir del juego tiene que quedar encima del panel.
            if (gui.m_splitDialog && gui.m_splitDialog.transform.parent == rt.parent)
                gui.m_splitDialog.transform.SetAsLastSibling();

            float width = rt.rect.width;
            float height = rt.rect.height;

            var bg = GUIManager.Instance.CreateWoodpanel(rt, Vector2.zero, Vector2.one, Vector2.zero, 0f, 0f, false);
            Stretch((RectTransform)bg.transform);
            bg.AddComponent<PanelDropTarget>();     // soltar aquí un objeto arrastrado lo guarda

            // Título y cofres
            var title = MakeText(T("rv_title"), rt, 18, GUIManager.Instance.ValheimOrange, TextAnchor.MiddleLeft);
            Place(title.rectTransform, Pad, 8f, width * 0.65f, 26f);
            _chestsText = MakeText("", rt, 15, Color.white, TextAnchor.MiddleRight);
            Place(_chestsText.rectTransform, width * 0.65f, 8f, width * 0.35f - Pad, 26f);

            // Buscador de ancho fijo + botón para borrarlo
            var searchGo = GUIManager.Instance.CreateInputField(rt, Vector2.zero, Vector2.zero, Vector2.zero,
                InputField.ContentType.Standard, T("rv_search"), 16, 100f, 30f);
            _search = searchGo.GetComponentInChildren<InputField>(true);
            _search.characterLimit = 32;
            _search.onValueChanged.AddListener(_ => RedrawGrid());
            Place((RectTransform)searchGo.transform, Pad, 38f, width - 2 * Pad - 40f, 30f);
            var clear = MakeButton("X", rt, () => { _search.text = ""; RedrawGrid(); });
            Place((RectTransform)clear.transform, width - Pad - 34f, 38f, 34f, 30f);

            // Pestañas
            string[] cats = { "rv_cat_all", "rv_cat_materials", "rv_cat_food", "rv_cat_weapons", "rv_cat_armor", "rv_cat_tools", "rv_cat_other" };
            float tabW = (width - 2 * Pad) / cats.Length;
            _categoryLabels.Clear();
            for (int i = 0; i < cats.Length; i++)
            {
                int index = i;
                var b = MakeButton(T(cats[i]), rt, () => { _category = (Category)index; UpdateTabLabels(); RedrawGrid(); }, 12);
                Place((RectTransform)b.transform, Pad + i * tabW, 74f, tabW - 2f, 28f);
                _categoryLabels.Add(b.GetComponentInChildren<Text>());
            }

            // Orden + guardar existentes
            string[] sorts = { "rv_sort_amount", "rv_sort_name", "rv_sort_type" };
            _sortLabels.Clear();
            for (int i = 0; i < sorts.Length; i++)
            {
                int index = i;
                var b = MakeButton(T(sorts[i]), rt, () => { _sort = (SortMode)index; UpdateTabLabels(); RedrawGrid(); }, 12);
                Place((RectTransform)b.transform, Pad + i * 82f, 106f, 80f, 26f);
                _sortLabels.Add(b.GetComponentInChildren<Text>());
            }
            var storeAll = MakeButton(T("rv_store_existing"), rt, StoreExisting, 12);
            Place((RectTransform)storeAll.transform, Pad + 3 * 82f + 6f, 106f, width - 2 * Pad - 3 * 82f - 6f, 26f);

            // Grilla con scroll, armada a mano (la vista de Jotunn deja su contenido de 100 px pegado a la derecha).
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.SetParent(rt, false);
            Place(scrollRt, Pad, 138f, width - 2 * Pad, height - 138f - 40f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewRt = (RectTransform)viewport.transform;
            viewRt.SetParent(scrollRt, false);
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = new Vector2(-14f, 0f);              // espacio para la barra

            var contentGo = new GameObject("Content", typeof(RectTransform));
            _gridContent = (RectTransform)contentGo.transform;
            _gridContent.SetParent(viewRt, false);

            var barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var barRt = (RectTransform)barGo.transform;
            barRt.SetParent(scrollRt, false);
            barRt.anchorMin = new Vector2(1f, 0f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(1f, 0.5f);
            barRt.sizeDelta = new Vector2(10f, 0f);
            barRt.anchoredPosition = Vector2.zero;
            var area = new GameObject("Sliding Area", typeof(RectTransform));
            var areaRt = (RectTransform)area.transform;
            areaRt.SetParent(barRt, false);
            areaRt.anchorMin = Vector2.zero;
            areaRt.anchorMax = Vector2.one;
            areaRt.offsetMin = areaRt.offsetMax = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            var handleRt = (RectTransform)handle.transform;
            handleRt.SetParent(areaRt, false);
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = handleRt.offsetMax = Vector2.zero;
            var bar = barGo.GetComponent<Scrollbar>();
            bar.handleRect = handleRt;
            bar.targetGraphic = handle.GetComponent<Image>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            try { GUIManager.Instance.ApplyScrollbarStyle(bar); } catch { }   // estilo de Valheim si está disponible

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewRt;
            scroll.content = _gridContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 120f;   // una fila y media por paso de rueda
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            foreach (var lg in _gridContent.GetComponents<LayoutGroup>())
                if (!(lg is GridLayoutGroup)) DestroyImmediate(lg);         // la vista de Jotunn trae una lista vertical
            var grid = _gridContent.GetComponent<GridLayoutGroup>();
            if (!grid) grid = _gridContent.gameObject.AddComponent<GridLayoutGroup>();
            // El contenido ocupa todo el ancho de la vista y crece hacia abajo (antes quedaba
            // como una columna angosta pegada a la derecha).
            _gridContent.anchorMin = new Vector2(0f, 1f);
            _gridContent.anchorMax = new Vector2(1f, 1f);
            _gridContent.pivot = new Vector2(0f, 1f);
            _gridContent.anchoredPosition = Vector2.zero;
            _gridContent.sizeDelta = Vector2.zero;

            float viewWidth = width - 2 * Pad - 14f;   // menos la barra de scroll
            int columns = Mathf.Max(1, Mathf.FloorToInt((viewWidth - 8f + 4f) / (CellSize + 4f)));
            grid.cellSize = new Vector2(CellSize, CellSize);
            grid.spacing = new Vector2(4f, 4f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            var fitter = _gridContent.GetComponent<ContentSizeFitter>();
            if (!fitter) fitter = _gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Pie: ayuda o nombre del ítem bajo el mouse
            _footer = MakeText("", rt, 13, GUIManager.Instance.ValheimBeige, TextAnchor.MiddleLeft);
            Place(_footer.rectTransform, Pad, height - 38f, width - 2 * Pad, 32f);
            ShowHelp();

            BuildDialog(rt, width, height);
            UpdateTabLabels();
            _panel.SetActive(false);
        }

        private void BuildDialog(RectTransform parent, float width, float height)
        {
            // Capa que bloquea los clics a la grilla mientras el cuadro está abierto.
            _dialog = new GameObject("AmountDialog", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)_dialog.transform;
            drt.SetParent(parent, false);
            Stretch(drt);
            _dialog.GetComponent<Image>().color = new Color(0, 0, 0, 0.45f);

            float w = 320f, h = 160f;
            var box = GUIManager.Instance.CreateWoodpanel(drt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, w, h, false);
            var brt = (RectTransform)box.transform;

            _dialogTitle = MakeText("", brt, 15, GUIManager.Instance.ValheimOrange, TextAnchor.MiddleCenter);
            Place(_dialogTitle.rectTransform, 10f, 10f, w - 20f, 24f);

            var minus = MakeButton("-", brt, () => AddPending(-1));
            Place((RectTransform)minus.transform, 60f, 40f, 34f, 30f);
            var amountGo = GUIManager.Instance.CreateInputField(brt, Vector2.zero, Vector2.zero, Vector2.zero,
                InputField.ContentType.IntegerNumber, "", 16, 100f, 30f);
            _dialogAmount = amountGo.GetComponentInChildren<InputField>(true);
            _dialogAmount.characterLimit = 6;
            _dialogAmount.onValueChanged.AddListener(_ => UpdateTakeLabel());
            Place((RectTransform)amountGo.transform, 100f, 40f, 120f, 30f);
            var plus = MakeButton("+", brt, () => AddPending(1));
            Place((RectTransform)plus.transform, 226f, 40f, 34f, 30f);

            string[] quick = { "1", "10", "rv_half", "rv_stack", "rv_all" };
            float qw = (w - 20f) / quick.Length;
            for (int i = 0; i < quick.Length; i++)
            {
                int index = i;
                string label = quick[i].StartsWith("rv_") ? T(quick[i]) : quick[i];
                var b = MakeButton(label, brt, () => QuickPending(index), 12);
                Place((RectTransform)b.transform, 10f + i * qw, 78f, qw - 4f, 26f);
            }

            var take = MakeButton("", brt, ConfirmDialog);
            _dialogTakeLabel = take.GetComponentInChildren<Text>();
            Place((RectTransform)take.transform, 10f, 114f, (w - 30f) / 2f, 32f);
            var cancel = MakeButton(T("rv_cancel"), brt, HideDialog);
            Place((RectTransform)cancel.transform, 20f + (w - 30f) / 2f, 114f, (w - 30f) / 2f, 32f);

            _dialog.SetActive(false);
        }

        // ================= Grilla =================

        private void RedrawGrid()
        {
            if (!_panel || !_open) return;
            var visible = Filtered();

            while (_cells.Count < visible.Count) _cells.Add(ItemCell.Create(_gridContent, this));
            for (int i = 0; i < _cells.Count; i++)
            {
                if (i < visible.Count) _cells[i].Show(visible[i]);
                else _cells[i].gameObject.SetActive(false);
            }

            if (visible.Count == 0) _footer.text = _items.Count == 0 ? T("rv_empty") : T("rv_no_match");
            else if (!ItemCell.Hovered) ShowHelp();
        }

        private List<NetworkItem> Filtered()
        {
            IEnumerable<NetworkItem> q = _items;
            if (_category != Category.Todo) q = q.Where(i => CategoryOf(i.Sample) == _category);
            string s = _search ? _search.text.Trim().ToLowerInvariant() : "";
            if (s.Length > 0) q = q.Where(i => Localize(i.Name).ToLowerInvariant().Contains(s));
            switch (_sort)
            {
                case SortMode.Nombre: q = q.OrderBy(i => Localize(i.Name)); break;
                case SortMode.Tipo: q = q.OrderBy(i => CategoryOf(i.Sample)).ThenBy(i => Localize(i.Name)); break;
                default: q = q.OrderByDescending(i => i.Amount); break;
            }
            return q.ToList();
        }

        private void UpdateTabLabels()
        {
            for (int i = 0; i < _categoryLabels.Count; i++)
                _categoryLabels[i].color = i == (int)_category ? GUIManager.Instance.ValheimOrange : Color.white;
            for (int i = 0; i < _sortLabels.Count; i++)
                _sortLabels[i].color = i == (int)_sort ? GUIManager.Instance.ValheimOrange : Color.white;
        }

        internal void ShowHelp() => _footer.text = T("rv_help_take") + "\n" + T("rv_help_store");

        internal void ShowTooltip(NetworkItem item)
        {
            string name = Localize(item.Name);
            _footer.text = item.Quality > 1 ? $"{name} ({T("rv_level", item.Quality)}) × {item.Amount}" : $"{name} × {item.Amount}";
        }

        /// <summary>Clic sobre una celda.</summary>
        internal void OnCellClick(NetworkItem item, PointerEventData.InputButton button)
        {
            if (TryStoreDragged()) return;          // venía arrastrando algo: se guarda
            if (_dialog.activeSelf) return;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            int stack = Mathf.Max(1, item.Sample.m_shared.m_maxStackSize);

            if (button == PointerEventData.InputButton.Right) Take(item, 1);
            else if (button == PointerEventData.InputButton.Left && ctrl) Take(item, Mathf.Min(stack, item.Amount));
            else if (button == PointerEventData.InputButton.Left && item.Amount <= 1) Take(item, 1);
            else if (button == PointerEventData.InputButton.Left) ShowDialog(item);
        }

        /// <summary>Si el jugador arrastra un objeto de su inventario, lo guarda y suelta el cursor.</summary>
        internal bool TryStoreDragged()
        {
            var gui = InventoryGui.instance;
            var player = Player.m_localPlayer;
            if (!gui || !player) return false;
            var item = DragItem(gui);
            if (item == null) return false;

            if (DragInventory(gui) == player.GetInventory())
                Store(item, DragAmount(gui));

            AccessTools.Method(typeof(InventoryGui), "SetupDragItem").Invoke(gui, new object[] { null, null, 1 });
            return true;
        }

        // ================= Cuadro de cantidad =================

        private void ShowDialog(NetworkItem item)
        {
            _pending = item;
            int stack = Mathf.Max(1, item.Sample.m_shared.m_maxStackSize);
            _dialogTitle.text = T("rv_take_title", Localize(item.Name), item.Amount);
            SetPending(Mathf.Min(stack, item.Amount));
            _dialog.SetActive(true);
            _dialog.transform.SetAsLastSibling();
        }

        private void HideDialog()
        {
            _pending = null;
            if (_dialog) _dialog.SetActive(false);
        }

        private int PendingMax => _pending == null ? 1 : Mathf.Max(1, (_items.FirstOrDefault(i => i.Matches(_pending.Sample)) ?? _pending).Amount);

        private int PendingValue => int.TryParse(_dialogAmount.text, out int n) ? Mathf.Clamp(n, 1, PendingMax) : 1;

        private void SetPending(int n) { _dialogAmount.text = Mathf.Clamp(n, 1, PendingMax).ToString(); UpdateTakeLabel(); }

        private void AddPending(int delta) => SetPending(PendingValue + delta);

        private void QuickPending(int index)
        {
            if (_pending == null) return;
            int stack = Mathf.Max(1, _pending.Sample.m_shared.m_maxStackSize);
            switch (index)
            {
                case 0: SetPending(1); break;
                case 1: SetPending(10); break;
                case 2: SetPending(PendingMax / 2); break;
                case 3: SetPending(stack); break;
                default: SetPending(PendingMax); break;
            }
        }

        private void UpdateTakeLabel()
        {
            if (_dialogTakeLabel) _dialogTakeLabel.text = T("rv_take_n", PendingValue);
        }

        private void ConfirmDialog()
        {
            if (_pending == null) return;
            var item = _pending;
            int amount = PendingValue;
            HideDialog();
            Take(item, amount);
        }

        // ================= Sacar / guardar =================

        private void Take(NetworkItem item, int wanted)
        {
            var player = Player.m_localPlayer;
            if (!player || _network == null || wanted <= 0) return;
            // Los ítems llegan con la respuesta del dueño de cada cofre (ver ChestRpc).
            if (_network.RequestTake(player, item, wanted) == 0)
                player.Message(MessageHud.MessageType.Center, T("rv_take_failed"));
        }

        private void Store(ItemDrop.ItemData item, int amount)
        {
            var player = Player.m_localPlayer;
            if (!player || _network == null) return;

            if (player.IsItemEquiped(item))
            {
                player.Message(MessageHud.MessageType.Center, T("rv_store_equipped"));
                return;
            }
            if (_network.RequestStore(player, item, Mathf.Min(amount, item.m_stack)) == 0)
                player.Message(MessageHud.MessageType.Center, T("rv_store_failed", Localize(item.m_shared.m_name)));
        }

        private void StoreExisting()
        {
            var player = Player.m_localPlayer;
            if (!player || _network == null) return;
            if (_network.RequestStoreExisting(player) == 0)
                player.Message(MessageHud.MessageType.Center, T("rv_stored_none"));
        }

        // ================= Categorías =================

        internal static Category CategoryOf(ItemDrop.ItemData item)
        {
            // La carnada de pesca es "munición" para la caña, pero se busca con las herramientas.
            if (item.m_shared.m_name.ToLowerInvariant().Contains("bait")) return Category.Herramientas;

            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Material:
                case ItemDrop.ItemData.ItemType.Trophy:
                    return Category.Materiales;
                case ItemDrop.ItemData.ItemType.Consumable:
                case ItemDrop.ItemData.ItemType.Fish:
                    return Category.Comida;
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                    return Category.Armas;
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                    return Category.Armaduras;
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                    return Category.Herramientas;
                default:
                    return Category.Otros;
            }
        }

        // ================= Utilidades de interfaz =================

        internal static string Localize(string token) => global::Localization.instance.Localize(token);

        /// <summary>Coloca un elemento medido desde la esquina superior izquierda del padre.</summary>
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Text MakeText(string text, Transform parent, int size, Color color, TextAnchor align)
        {
            var go = GUIManager.Instance.CreateText(text, parent, Vector2.zero, Vector2.zero, Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, size, color, true, Color.black, 100f, 20f, false);
            var t = go.GetComponentInChildren<Text>(true);
            t.alignment = align;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        private static GameObject MakeButton(string text, Transform parent, UnityEngine.Events.UnityAction onClick, int fontSize = 14)
        {
            var go = GUIManager.Instance.CreateButton(text, parent, Vector2.zero, Vector2.zero, Vector2.zero, 100f, 30f);
            go.GetComponentInChildren<Button>(true).onClick.AddListener(onClick);
            var label = go.GetComponentInChildren<Text>();
            if (label)
            {
                label.fontSize = fontSize;
                label.resizeTextForBestFit = true;      // textos largos (inglés/español) se achican, no se cortan
                label.resizeTextMinSize = 9;
                label.resizeTextMaxSize = fontSize;
            }
            return go;
        }
    }

    /// <summary>Una celda de la grilla: ícono, cantidad y nivel. Clic izquierdo/derecho y tooltip.</summary>
    internal class ItemCell : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal static bool Hovered;

        private TerminalWindow _owner;
        private NetworkItem _item;
        private Image _icon;
        private Text _amount;
        private Text _quality;

        internal static ItemCell Create(Transform parent, TerminalWindow owner)
        {
            var go = new GameObject("Cell", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var cell = go.AddComponent<ItemCell>();
            cell._owner = owner;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6, 6); irt.offsetMax = new Vector2(-6, -6);
            cell._icon = iconGo.GetComponent<Image>();
            cell._icon.raycastTarget = false;
            cell._icon.preserveAspect = true;

            cell._amount = MakeLabel(go.transform, TextAnchor.LowerRight, Color.white);
            cell._quality = MakeLabel(go.transform, TextAnchor.UpperLeft, GUIManager.Instance.ValheimOrange);
            return cell;
        }

        private static Text MakeLabel(Transform parent, TextAnchor align, Color color)
        {
            var go = GUIManager.Instance.CreateText("", parent, Vector2.zero, Vector2.one, Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, 13, color, true, Color.black, 0f, 0f, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(3, 1); rt.offsetMax = new Vector2(-3, -1);
            var t = go.GetComponentInChildren<Text>(true);
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        internal void Show(NetworkItem item)
        {
            _item = item;
            gameObject.SetActive(true);
            Sprite icon = null;
            try { icon = item.Sample.GetIcon(); } catch { }
            _icon.sprite = icon;
            _icon.enabled = icon;
            _amount.text = item.Amount >= 10000 ? (item.Amount / 1000) + "k" : item.Amount.ToString();
            _quality.text = item.Quality > 1 ? "★" + item.Quality : "";
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_item != null) _owner.OnCellClick(_item, eventData.button);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = true;
            if (_item != null) _owner.ShowTooltip(_item);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
            _owner.ShowHelp();
        }
    }

    /// <summary>Fondo del panel: soltar ahí un objeto arrastrado del inventario lo guarda.</summary>
    internal class PanelDropTarget : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData) => TerminalWindow.StoreDraggedFromCursor();
    }
}
