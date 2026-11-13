using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hoardkeeper
{
    // The Storage window: every chest around you at once, under your bag (where a chest's window goes).
    // Each chest is a copy of the game's own chest grid, so dragging, splitting (Shift) and dropping work the way
    // they always do. Ctrl + click sends an item across (chest -> bag, bag -> a chest that holds it).
    // Right-click an item to choose it: every stack of it lights up, with the total. Or type in the search box.
    // While open, the chests are yours (owner + "in use"), like an opened chest.
    [HarmonyPatch]
    internal static class Storage
    {
        private class Section
        {
            public Container c;
            public bool skipped;
            public int muted = -1;
            public RectTransform root;
            public InventoryGrid grid;
            public TextMeshProUGUI title;
            public int rows = -1, cols = -1;
            public float btnW, h;
            public RectTransform chips;
            public readonly List<KeyValuePair<string, Button>> chipBtns = new List<KeyValuePair<string, Button>>();
        }

        private const float ChipW = 66f, ChipH = 26f;

        private static readonly List<Section> _sections = new List<Section>();
        private static bool _open;
        private static RectTransform _panel, _viewport, _content;
        private static TMP_InputField _search, _rename;
        private static Section _renaming;
        private static TextMeshProUGUI _status;
        private static Button _bagBtn;
        private static CanvasGroup _bagGroup;
        private static string _chosen;
        private static float _statusAt;
        private static int _frame;
        private static bool _logged;

        private const float Pad = 8f, Gap = 8f, SideW = 158f, TitleH = 26f, BtnW = 62f, BtnH = 24f, CardMin = 440f, Head = TitleH;

        private static readonly MethodInfo SelectedM = AccessTools.Method(typeof(InventoryGui), "OnSelectedItem");
        private static readonly MethodInfo ReleasedM = AccessTools.Method(typeof(InventoryGui), "OnReleasedItem");
        private static readonly MethodInfo CloseM = AccessTools.Method(typeof(InventoryGui), "CloseContainer");
        private static readonly MethodInfo SetupDrag = AccessTools.Method(typeof(InventoryGui), "SetupDragItem");
        private static readonly FieldInfo DragF = AccessTools.Field(typeof(InventoryGui), "m_dragItem");
        private static readonly FieldInfo DragInvF = AccessTools.Field(typeof(InventoryGui), "m_dragInventory");
        private static readonly FieldInfo ElementsF = AccessTools.Field(typeof(InventoryGrid), "m_elements");
        private static readonly FieldInfo WidthF = AccessTools.Field(typeof(InventoryGrid), "m_width");
        private static readonly FieldInfo HeightF = AccessTools.Field(typeof(InventoryGrid), "m_height");
        private static readonly FieldInfo GridF = AccessTools.Field(typeof(InventoryGui), "m_containerGrid");

        internal static bool IsOpen() { return _open; }

        internal static bool Holds(Container c)
        {
            if (!_open) return false;
            foreach (Section s in _sections) if (s.c == c) return true;
            return false;
        }

        internal static bool Typing()
        {
            return (_search != null && _search.isFocused) || (_rename != null && _rename.gameObject.activeSelf && _rename.isFocused) || ChestName.Typing();
        }

        // the chests the actions work on (shown, not skipped)
        private static List<Container> Live()
        {
            List<Container> l = new List<Container>();
            foreach (Section s in _sections) if (!s.skipped && s.c != null) l.Add(s.c);
            return l;
        }

        // ------------------------------------------------------------ open / close

        internal static void Toggle()
        {
            try { if (_open) Close(); else Open(); }
            catch (Exception e) { Plugin.Log.LogWarning("Storage window failed: " + e); }
        }

        private static void Open()
        {
            InventoryGui gui = InventoryGui.instance;
            Player p = Player.m_localPlayer;
            if (gui == null || p == null) return;
            if (!InventoryGui.IsVisible()) gui.Show(null, 1);
            if (gui.IsContainerOpen()) CloseM.Invoke(gui, null);
            if (_panel == null) Build(gui);
            if (_panel == null) return;
            _open = true;
            _chosen = null;
            Fill(gui, p);
            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
            _content.anchoredPosition = Vector2.zero;
            if (_sections.Count == 0) Plugin.Say("No chests within " + Mathf.RoundToInt(Plugin.Range.Value) + " m");
        }

        private static void Fill(InventoryGui gui, Player p)
        {
            List<Container> near = Chests.Near(p, Plugin.Range.Value, true);
            List<Container> use = new List<Container>(), skip = new List<Container>();
            foreach (Container c in near) (Chests.Skipped(c) ? skip : use).Add(c);
            int max = Plugin.MaxChests.Value;
            foreach (Container c in use) if (_sections.Count < max) Add(gui, c, false);
            foreach (Container c in skip) if (_sections.Count < max) Add(gui, c, true);
        }

        private static void Clear(InventoryGui gui)
        {
            if (gui != null)
            {
                Inventory di = DragInvF.GetValue(gui) as Inventory;
                if (di != null && Player.m_localPlayer != null && di != Player.m_localPlayer.GetInventory())
                    SetupDrag.Invoke(gui, new object[] { null, null, 1 });
            }
            if (_rename != null) { _rename.transform.SetParent(_panel, false); _rename.gameObject.SetActive(false); }
            _renaming = null;
            foreach (Section s in _sections)
            {
                if (s.c != null && !s.skipped) Chests.MarkInUse(s.c, false);
                if (s.root != null) UnityEngine.Object.Destroy(s.root.gameObject);
            }
            _sections.Clear();
        }

        private static void Close()
        {
            Clear(InventoryGui.instance);
            _open = false;
            _chosen = null;
            if (_search != null) _search.text = "";
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        // after Skip / Use: the same window again, same scroll
        private static void Rebuild()
        {
            InventoryGui gui = InventoryGui.instance;
            Vector2 at = _content.anchoredPosition;
            Clear(gui);
            Fill(gui, Player.m_localPlayer);
            _content.anchoredPosition = at;
        }

        private static void ShowBag(bool on)
        {
            if (_bagGroup == null) return;
            _bagGroup.alpha = on ? 1f : 0f;
            _bagGroup.blocksRaycasts = on;
        }

        // ------------------------------------------------------------ building

        private static void Build(InventoryGui gui)
        {
            if (gui.m_container == null || gui.m_takeAllButton == null) return;
            GameObject pg = new GameObject("Hoardkeeper_Storage", typeof(RectTransform));
            _panel = (RectTransform)pg.transform;
            _panel.SetParent(gui.m_inventoryRoot != null ? gui.m_inventoryRoot : gui.m_container.parent, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0f, 1f);
            Skin.Window(gui, _panel, pg.AddComponent<Image>());

            // left side: search, the window-wide commands, and the keys
            _search = Ui.Field(gui, _panel, "Find item...");
            Place((RectTransform)_search.transform, Pad, -Pad, SideW - Pad, 28f);
            _search.onValueChanged.AddListener(s => { _chosen = null; _statusAt = 0f; });
            float y = -Pad - 36f;
            y = Side(gui, "Sort all", y, () => Actions.SortAll(Live()), "Sort every chest here (order: Shift + T)");
            y = Side(gui, "Tidy up", y, () => Actions.Tidy(Live()), "Each kind of item into the chest that holds the most of it, then sort them all");
            y = Side(gui, "Stack", y, () => Actions.QuickStack(Player.m_localPlayer), "Your items into the chests that hold the same item (Shift + Q)");
            y = Side(gui, "Fill tags", y, () => Tags.Fill(Player.m_localPlayer, Live()), "Send every item to the chest tagged with its category (Food, Ores, Wood...), from your bag and from chests not tagged for it. Tag a chest with its Tags button");
            _bagBtn = Ui.CloneButton(gui, _panel, "Bag: off", SideW - Pad, 28f, () => { Plugin.GatherFromBag.Value = !Plugin.GatherFromBag.Value; }, "Gather also takes from your bag (never hotbar, locked, favorite or worn)");
            Place((RectTransform)_bagBtn.transform, Pad, y, SideW - Pad, 28f);
            y -= 32f;
            y = Side(gui, "Close", y, Close, "Close the Storage window (Shift + F)");
            TextMeshProUGUI keys = Ui.Text(gui, _panel, 13f, new Color(0.85f, 0.82f, 0.75f, 1f), TextAlignmentOptions.TopLeft);
            keys.enableWordWrapping = true;
            keys.overflowMode = TextOverflowModes.Overflow;
            keys.lineSpacing = -6f;
            keys.text = "<color=#ffcc66>Keys</color>\n"
                + "<color=#ffe0a0>Shift+F</color> open / close\n"
                + "<color=#ffe0a0>Click</color> pick up / put\n"
                + "<color=#ffe0a0>Shift+click</color> split\n"
                + "<color=#ffe0a0>Ctrl+click</color> send across\n"
                + "<color=#ffe0a0>Right-click</color> find all of it\n"
                + "<color=#ffe0a0>T</color> sort  <color=#ffe0a0>Shift+T</color> order\n"
                + "<color=#ffe0a0>Alt+click</color> lock bag slot\n"
                + "<color=#ffe0a0>Alt+right-click</color> favorite\n"
                + "<color=#ffe0a0>Shift+Q</color> stack nearby\n"
                + "<color=#ffe0a0>Shift+R</color> restock";
            Place(keys.rectTransform, Pad + 2f, y - 6f, SideW - Pad - 2f, 300f);

            _status = Ui.Text(gui, _panel, 15f, new Color(1f, 0.85f, 0.55f, 1f), TextAlignmentOptions.MidlineLeft);
            RectTransform st = _status.rectTransform;
            st.anchorMin = new Vector2(0f, 1f); st.anchorMax = new Vector2(1f, 1f);
            st.pivot = new Vector2(0f, 1f);
            st.offsetMin = new Vector2(SideW + Pad + 4f, -Pad - 26f);
            st.offsetMax = new Vector2(-Pad - 40f, -Pad);
            Button x = Ui.CloneButton(gui, _panel, "X", 32f, 28f, Close, "Close the Storage window (Shift + F)");
            RectTransform xr = (RectTransform)x.transform;
            xr.anchorMin = xr.anchorMax = new Vector2(1f, 1f);
            xr.pivot = new Vector2(1f, 1f);
            xr.anchoredPosition = new Vector2(-Pad, -Pad + 2f);
            xr.sizeDelta = new Vector2(32f, 28f);

            GameObject vp = new GameObject("Viewport", typeof(RectTransform));
            _viewport = (RectTransform)vp.transform;
            _viewport.SetParent(_panel, false);
            _viewport.anchorMin = Vector2.zero; _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = new Vector2(SideW + Pad, Pad); _viewport.offsetMax = new Vector2(-Pad, -Pad - 32f);
            vp.AddComponent<RectMask2D>();
            Image vimg = vp.AddComponent<Image>();
            vimg.color = new Color(0f, 0f, 0f, 0.001f);   // catches the mouse wheel between chests

            GameObject ct = new GameObject("Content", typeof(RectTransform));
            _content = (RectTransform)ct.transform;
            _content.SetParent(_viewport, false);
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = new Vector2(0f, 10f);

            ScrollRect sr = pg.AddComponent<ScrollRect>();
            sr.content = _content;
            sr.viewport = _viewport;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = false;
            sr.scrollSensitivity = Plugin.ScrollSpeed.Value;

            _rename = Ui.Field(gui, _panel, "Chest name");
            _rename.characterLimit = 30;
            _rename.onEndEdit.AddListener(EndRename);
            _rename.gameObject.SetActive(false);

            pg.SetActive(false);
        }

        private static float Side(InventoryGui gui, string label, float y, Action click, string tip)
        {
            Button b = Ui.CloneButton(gui, _panel, label, SideW - Pad, 28f, click, tip);
            Place((RectTransform)b.transform, Pad, y, SideW - Pad, 28f);
            return y - 32f;
        }

        private static void Place(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static void Add(InventoryGui gui, Container c, bool skipped)
        {
            Section s = new Section();
            s.c = c;
            s.skipped = skipped;
            GameObject rg = new GameObject("Chest", typeof(RectTransform));
            s.root = (RectTransform)rg.transform;
            s.root.SetParent(_content, false);
            s.root.anchorMin = s.root.anchorMax = new Vector2(0f, 1f);
            s.root.pivot = new Vector2(0f, 1f);
            Skin.Card(rg.AddComponent<Image>(), skipped);

            s.title = Ui.Text(gui, s.root, 16f, skipped ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(1f, 0.82f, 0.45f, 1f), TextAlignmentOptions.MidlineLeft);
            Place(s.title.rectTransform, 8f, -3f, 240f, TitleH - 2f);

            if (skipped)
            {
                Button b = Ui.CloneButton(gui, s.root, "Use", 52f, BtnH, () => { if (s.c != null) { Chests.SetSkipped(c, false); Rebuild(); } }, "Show this chest again and let the buttons use it");
                Corner((RectTransform)b.transform, -6f, -3f, 52f);
                s.btnW = 60f;
            }
            else
            {
                // Skip in the title row; the chest's own commands in a row under it, like Storage Master
                Button sk = Ui.CloneButton(gui, s.root, "Skip", 50f, BtnH, () => { if (s.c != null) { Chests.SetSkipped(c, true); Rebuild(); } }, "Leave this chest out of Storage, Stack and Restock (only for you)");
                Corner((RectTransform)sk.transform, -6f, -3f, 50f);
                float y = 8f;
                y = Cmd(gui, s, "Gather", 64f, y, () => Actions.Gather(Player.m_localPlayer, c, Live(), Plugin.GatherFromBag.Value), "Bring here every item of the kinds this chest holds, from the other chests");
                y = Cmd(gui, s, "Sort", 48f, y, () => { Actions.SortChest(c); Plugin.Say("Chest sorted by " + Actions.OrderName()); }, "Sort this chest");
                y = Cmd(gui, s, "Take all", 66f, y, () => Actions.TakeAll(Player.m_localPlayer, c), "Take everything from this chest");
                y = Cmd(gui, s, "Tags", 48f, y, () => { if (s.chips != null) { s.chips.gameObject.SetActive(!s.chips.gameObject.activeSelf); ChipLabels(s); } }, "Choose which kinds of item belong in this chest; Fill tags sends them here");
                y = Cmd(gui, s, "Name", 52f, y, () => StartRename(s), "Give this chest a name (shown when you look at it)");
                s.btnW = 58f;
                BuildChips(gui, s);
                s.grid = CloneGrid(gui, s.root);
                if (s.grid == null) s.skipped = true;
                else
                {
                    Chests.Claim(c);
                    Chests.MarkInUse(c, true);
                }
            }
            _sections.Add(s);
        }

        private static void Corner(RectTransform r, float x, float y, float w)
        {
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, BtnH);
        }

        // a button in the chest's command row (x from the left)
        private static float Cmd(InventoryGui gui, Section s, string label, float w, float x, Action click, string tip)
        {
            Button b = Ui.CloneButton(gui, s.root, label, w, BtnH, () => { if (s.c != null) click(); }, tip);
            Place((RectTransform)b.transform, x, -TitleH - 2f, w, BtnH);
            return x + w + 4f;
        }

        private static void BuildChips(InventoryGui gui, Section s)
        {
            GameObject g = new GameObject("Tags", typeof(RectTransform));
            s.chips = (RectTransform)g.transform;
            s.chips.SetParent(s.root, false);
            Place(s.chips, 8f, -TitleH - 4f, 100f, 30f);
            foreach (string t in Tags.All)
            {
                string tag = t;
                Button b = Ui.CloneButton(gui, s.chips, tag, ChipW, ChipH, () => { if (s.c != null) { Tags.Toggle(s.c, tag); ChipLabels(s); } }, null);
                s.chipBtns.Add(new KeyValuePair<string, Button>(tag, b));
            }
            g.SetActive(false);
        }

        // lays the tag buttons out in rows that fit width; returns their height
        private static float LayChips(Section s, float width)
        {
            int per = Mathf.Max(1, Mathf.FloorToInt((width + 4f) / (ChipW + 4f)));
            for (int i = 0; i < s.chipBtns.Count; i++)
                Place((RectTransform)s.chipBtns[i].Value.transform, (i % per) * (ChipW + 4f), -(i / per) * (ChipH + 4f), ChipW, ChipH);
            int rows = (s.chipBtns.Count + per - 1) / per;
            float h = rows * (ChipH + 4f);
            s.chips.sizeDelta = new Vector2(width, h);
            return h;
        }

        private static void ChipLabels(Section s)
        {
            if (s.c == null) return;
            HashSet<string> on = Tags.Of(s.c);
            foreach (KeyValuePair<string, Button> kv in s.chipBtns)
                Ui.Label(kv.Value, on.Contains(kv.Key) ? "<b><color=#ffcc55>" + kv.Key + "</color></b>" : "<color=#8a8a8a>" + kv.Key + "</color>");
        }

        // a copy of the game's chest grid, emptied, wired to the game's own click handling
        private static InventoryGrid CloneGrid(InventoryGui gui, Transform parent)
        {
            InventoryGrid orig = GridF.GetValue(gui) as InventoryGrid;
            if (orig == null) return null;
            GameObject g = UnityEngine.Object.Instantiate(orig.gameObject, parent, false);
            g.name = "Grid";
            g.SetActive(true);
            InventoryGrid grid = g.GetComponent<InventoryGrid>();
            if (grid.m_gridRoot == null || grid.m_gridRoot == orig.m_gridRoot)
            {
                Plugin.Log.LogWarning("Storage: the chest grid has an unexpected layout; can't copy it");
                UnityEngine.Object.Destroy(g);
                return null;
            }
            if (!_logged)
            {
                _logged = true;
                RectTransform gr = grid.m_gridRoot, pr = grid.m_elementPrefab != null ? grid.m_elementPrefab.transform as RectTransform : null;
                Plugin.Log.LogInfo("Storage grid: root under grid=" + gr.IsChildOf(g.transform) + " rootAnchors=" + gr.anchorMin + "-" + gr.anchorMax + " rootPivot=" + gr.pivot
                    + " slot=" + (pr != null ? pr.anchorMin + "-" + pr.anchorMax + " pivot " + pr.pivot + " size " + pr.sizeDelta : "?") + " space=" + grid.m_elementSpace
                    + " parts=" + g.GetComponentsInChildren<Component>(true).Length);
            }
            List<GameObject> old = new List<GameObject>();
            foreach (Transform t in grid.m_gridRoot) old.Add(t.gameObject);
            foreach (GameObject o in old) { o.transform.SetParent(null, false); UnityEngine.Object.Destroy(o); }
            // (ScrollRectEnsureVisible needs its ScrollRect: switch it off instead of removing it)
            foreach (ScrollRect sr in g.GetComponentsInChildren<ScrollRect>(true)) sr.enabled = false;
            foreach (RectMask2D m in g.GetComponentsInChildren<RectMask2D>(true)) UnityEngine.Object.Destroy(m);
            foreach (Mask m in g.GetComponentsInChildren<Mask>(true)) UnityEngine.Object.Destroy(m);
            foreach (UIGamePad gp in g.GetComponentsInChildren<UIGamePad>(true)) UnityEngine.Object.Destroy(gp);
            // the game fills this in only on its own two grids; empty, every redraw throws
            grid.CanDropDragOntoItem = orig.CanDropDragOntoItem ?? (it => true);
            foreach (Scrollbar sb in g.GetComponentsInChildren<Scrollbar>(true)) sb.gameObject.SetActive(false);
            foreach (UIGroupHandler gh in g.GetComponentsInChildren<UIGroupHandler>(true)) UnityEngine.Object.Destroy(gh);
            foreach (CanvasGroup cg in g.GetComponentsInChildren<CanvasGroup>(true)) { cg.interactable = true; cg.blocksRaycasts = true; cg.alpha = 1f; }
            grid.m_uiGroup = orig.m_uiGroup;
            grid.m_ensureVisible = null;
            ElementsF.SetValue(grid, new List<InventoryElement>());
            WidthF.SetValue(grid, -1);
            HeightF.SetValue(grid, -1);
            grid.m_onSelected = (g2, it, pos, mod) => OnSelected(g2, it, pos, mod);
            grid.m_onRightClick = (g2, it, pos) => OnRightClick(g2, it);
            grid.m_onReleased = (Action<InventoryGrid, ItemDrop.ItemData, Vector2i>)Delegate.CreateDelegate(typeof(Action<InventoryGrid, ItemDrop.ItemData, Vector2i>), gui, ReleasedM);
            grid.m_onEnter = null;
            RectTransform r = (RectTransform)g.transform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(8f, -TitleH - 4f);
            grid.m_gridRoot.anchorMin = new Vector2(0f, 1f);
            grid.m_gridRoot.anchorMax = new Vector2(1f, 1f);
            grid.m_gridRoot.pivot = new Vector2(0.5f, 1f);
            grid.m_gridRoot.anchoredPosition = Vector2.zero;
            return grid;
        }

        // ------------------------------------------------------------ clicks

        // the copied slots keep the game's button click sound on top of the item move sound: two clicks.
        // Only the move sound stays (checked again whenever the grid makes new slots).
        private static void Mute(Section s)
        {
            if (s.grid == null || s.grid.m_gridRoot == null || s.muted == s.grid.m_gridRoot.childCount) return;
            s.muted = s.grid.m_gridRoot.childCount;
            foreach (ButtonSfx b in s.grid.m_gridRoot.GetComponentsInChildren<ButtonSfx>(true)) UnityEngine.Object.Destroy(b);
        }

        private static readonly FieldInfo TipHoveredF = AccessTools.Field(typeof(UITooltip), "m_hovered");
        private static bool _tipLogged;

        // the game's tooltips of whatever lies under this window (the crafting list) must not show through it
        private static void NoTipsUnder()
        {
            if (_panel == null || TipHoveredF == null) return;
            GameObject h = TipHoveredF.GetValue(null) as GameObject;
            if (h == null || h.transform.IsChildOf(_panel)) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_panel, Input.mousePosition)) return;
            if (!_tipLogged) { _tipLogged = true; Plugin.Log.LogInfo("Storage: hid a tooltip from " + h.name + (h.transform.parent != null ? " < " + h.transform.parent.name : "")); }
            UITooltip.HideTooltip();
        }

        private static Section Of(InventoryGrid grid)
        {
            foreach (Section s in _sections) if (s.grid == grid) return s;
            return null;
        }

        private static void OnSelected(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos, InventoryGrid.Modifier mod)
        {
            try
            {
                InventoryGui gui = InventoryGui.instance;
                Player p = Player.m_localPlayer;
                Section s = Of(grid);
                if (gui == null || p == null || s == null || s.c == null) return;
                Chests.Claim(s.c);
                if (mod == InventoryGrid.Modifier.Move && item != null && DragF.GetValue(gui) == null)
                {
                    if (!item.m_shared.m_questItem) p.GetInventory().MoveItemToThis(grid.GetInventory(), item);
                    return;
                }
                SelectedM.Invoke(gui, new object[] { grid, item, pos, mod });
            }
            catch (Exception e) { Plugin.Log.LogWarning("Storage click failed: " + e); }
        }

        // right-click: choose this item (again, or on an empty slot: unchoose)
        private static void OnRightClick(InventoryGrid grid, ItemDrop.ItemData item)
        {
            _chosen = item == null || item.m_shared.m_name == _chosen ? null : item.m_shared.m_name;
            _statusAt = 0f;
        }

        // Ctrl + click in your bag while the window is open: into the chests (the game would drop it on the ground)
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem"), HarmonyPrefix]
        private static bool BagMove(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            if (!_open || grid != __instance.m_playerGrid || mod != InventoryGrid.Modifier.Move || item == null) return true;
            if (DragF.GetValue(__instance) != null) return true;
            Actions.SendToChests(Player.m_localPlayer, item, Live());
            return false;
        }

        // With no chest open the game drops any held item that isn't from your bag, every frame (so picking an
        // item up in this window did nothing). While the window is open, items from its chests may stay in hand.
        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer"), HarmonyPrefix]
        private static bool KeepHeld(InventoryGui __instance)
        {
            if (!_open || __instance.IsContainerOpen()) return true;
            __instance.m_container.gameObject.SetActive(false);
            Inventory di = DragInvF.GetValue(__instance) as Inventory;
            if (di != null && Player.m_localPlayer != null && di != Player.m_localPlayer.GetInventory() && !OursInv(di))
                SetupDrag.Invoke(__instance, new object[] { null, null, 1 });
            return false;
        }

        private static bool OursInv(Inventory inv)
        {
            foreach (Section s in _sections) if (s.c != null && s.c.GetInventory() == inv) return true;
            return false;
        }

        [HarmonyPatch(typeof(InventoryGui), "Hide"), HarmonyPrefix]
        private static void OnHide()
        {
            if (_open) Close();
        }

        // ------------------------------------------------------------ names

        private static void StartRename(Section s)
        {
            _renaming = s;
            _rename.transform.SetParent(s.root, false);
            RectTransform r = (RectTransform)_rename.transform;
            Place(r, 6f, -2f, 230f, TitleH - 2f);
            _rename.text = Chests.Name(s.c);
            _rename.gameObject.SetActive(true);
            _rename.Select();
            _rename.ActivateInputField();
        }

        private static void EndRename(string text)
        {
            Section s = _renaming;
            _renaming = null;
            if (_rename != null) _rename.gameObject.SetActive(false);
            if (s == null || s.c == null) return;
            if (text != Chests.Name(s.c)) { Chests.SetName(s.c, text); Plugin.Say(text.Trim().Length > 0 ? "Chest named " + text.Trim() : "Chest name removed"); }
        }

        // ------------------------------------------------------------ every frame

        [HarmonyPatch(typeof(InventoryGui), "Update"), HarmonyPostfix]
        private static void Tick(InventoryGui __instance)
        {
            if (!_open) return;
            try
            {
                Player p = Player.m_localPlayer;
                if (p == null || !InventoryGui.IsVisible() || __instance.IsContainerOpen()) { Close(); return; }
                Refresh(__instance, p);
                NoTipsUnder();
            }
            catch (Exception e) { Plugin.Log.LogWarning("Storage window: " + e); Close(); }
        }

        private static void Refresh(InventoryGui gui, Player p)
        {
            _frame++;
            ItemDrop.ItemData drag = DragF.GetValue(gui) as ItemDrop.ItemData;
            float range = Plugin.Range.Value + 8f;

            // chests gone or left behind
            for (int i = _sections.Count - 1; i >= 0; i--)
            {
                Section s = _sections[i];
                ZNetView nv = Chests.View(s.c);
                if (s.c != null && nv != null && nv.IsValid() && Vector3.Distance(p.transform.position, s.c.transform.position) <= range) continue;
                if (s.c != null && !s.skipped) Chests.MarkInUse(s.c, false);
                if (s.root != null) UnityEngine.Object.Destroy(s.root.gameObject);
                _sections.RemoveAt(i);
            }

            // where: in place of a chest's window, or over your bag when it's hidden
            // where: right of the button column, from the top of your bag to near the bottom of the screen,
            // over the equipment and crafting panels (the canvas draws in screen pixels; local units are scaled)
            Vector3[] cs = new Vector3[4];
            gui.m_player.GetWorldCorners(cs);
            float top = cs[1].y, left = cs[2].x + 150f * _panel.lossyScale.x;
            RectTransform col = Ui.Column;
            if (col != null && col.gameObject.activeInHierarchy) { col.GetWorldCorners(cs); left = cs[2].x; foreach (Button b in col.GetComponentsInChildren<Button>()) { ((RectTransform)b.transform).GetWorldCorners(cs); left = Mathf.Max(left, cs[2].x); } }
            left += 12f;
            float sx = Mathf.Max(0.01f, _panel.lossyScale.x), sy = Mathf.Max(0.01f, _panel.lossyScale.y);
            _panel.position = new Vector3(left, top, _panel.position.z);
            float w = Plugin.StorageWidth.Value > 0f ? Plugin.StorageWidth.Value : Mathf.Max(560f, (Screen.width - 30f - left) / sx);
            float h = Plugin.StorageHeight.Value > 0f ? Plugin.StorageHeight.Value : Mathf.Max(300f, (top - 90f) / sy);
            _panel.sizeDelta = new Vector2(w, h);
            if (_panel.GetSiblingIndex() != _panel.parent.childCount - 1) _panel.SetAsLastSibling();
            Ui.Label(_bagBtn, Plugin.GatherFromBag.Value ? "Bag: on" : "Bag: off");
            if (_renaming != null && !_rename.isFocused && !_rename.gameObject.activeSelf) _renaming = null;

            Vector3[] vc = new Vector3[4];
            _viewport.GetWorldCorners(vc);
            // a grid of cards: at least two chests side by side; slots shrink when a chest is too wide
            float cw = _content.rect.width;
            int ncol = Mathf.Max(2, Mathf.FloorToInt((cw + Gap) / (CardMin + Gap)));
            float secW = (cw - Gap * (ncol - 1)) / ncol;
            foreach (Section s in _sections)
            {
                Inventory inv = s.c.GetInventory();
                string tags = Tags.Shown(s.c);
                string title = Chests.Title(s.c) + (s.skipped ? "  (skipped)" : "") + (tags.Length > 0 ? "  <size=12><color=#8fc8ff>[" + tags + "]</color></size>" : "");
                s.title.rectTransform.sizeDelta = new Vector2(Mathf.Max(80f, secW - 16f - s.btnW), TitleH - 2f);
                if (s.title.text != title) s.title.text = title;
                if (s.skipped || s.grid == null) { s.h = TitleH + 8f; continue; }
                float room = secW - 16f;
                float y0 = TitleH + BtnH + 8f;
                if (s.chips.gameObject.activeSelf)
                {
                    s.chips.anchoredPosition = new Vector2(8f, -y0);
                    y0 += LayChips(s, room) + 4f;
                }
                int cols = inv.GetWidth(), rows = inv.GetHeight();
                float space = s.grid.m_elementSpace;
                RectTransform gr = (RectTransform)s.grid.transform;
                if (cols != s.cols || rows != s.rows)
                {
                    s.cols = cols; s.rows = rows;
                    gr.sizeDelta = new Vector2(cols * space, rows * space);
                }
                float sc = Mathf.Min(1f, room / Mathf.Max(1f, cols * space));
                if (Mathf.Abs(gr.localScale.x - sc) > 0.001f) gr.localScale = new Vector3(sc, sc, 1f);
                gr.anchoredPosition = new Vector2(8f, -y0);
                s.h = y0 + rows * space * sc + 8f;
                // only the chests you can see are redrawn every frame
                s.root.GetWorldCorners(cs);
                bool seen = cs[0].y < vc[1].y && cs[1].y > vc[0].y;
                if (seen || _frame % 20 == 0 || s.grid.GetInventory() != inv)
                {
                    ZNetView nv = Chests.View(s.c);
                    if (!nv.IsOwner()) { Chests.Claim(s.c); Chests.MarkInUse(s.c, true); }
                    s.grid.UpdateInventory(inv, null, drag);
                    Mute(s);
                    if (seen) Tint(s.grid, inv);
                }
            }
            // rows of cards, every card in a row as tall as the tallest
            float y = 0f;
            for (int i = 0; i < _sections.Count; i += ncol)
            {
                float rowH = 0f;
                for (int k = 0; k < ncol && i + k < _sections.Count; k++) rowH = Mathf.Max(rowH, _sections[i + k].h);
                for (int k = 0; k < ncol && i + k < _sections.Count; k++)
                {
                    Section s = _sections[i + k];
                    s.root.anchoredPosition = new Vector2(k * (secW + Gap), -y);
                    s.root.sizeDelta = new Vector2(secW, rowH);
                }
                y += rowH + Gap;
            }
            _content.sizeDelta = new Vector2(0f, y);
            Tint(gui.m_playerGrid, p.GetInventory());
            if (Time.time >= _statusAt) { _statusAt = Time.time + 0.3f; Status(p); }
        }

        // ------------------------------------------------------------ search and chosen item

        private static readonly Dictionary<string, string> _shown = new Dictionary<string, string>();

        private static string Shown(string name)
        {
            string s;
            if (!_shown.TryGetValue(name, out s)) _shown[name] = s = Localization.instance.Localize(name);
            return s;
        }

        private static string Filter() { return _search != null ? _search.text.Trim() : ""; }

        private static bool Matches(ItemDrop.ItemData it, string filter)
        {
            if (_chosen != null) return it.m_shared.m_name == _chosen;
            return Shown(it.m_shared.m_name).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Tint(InventoryGrid grid, Inventory inv)
        {
            IList els = ElementsF.GetValue(grid) as IList;
            if (els == null) return;
            string filter = Filter();
            bool on = _chosen != null || filter.Length > 0;
            int w = inv.GetWidth();
            bool[] hit = new bool[els.Count];
            if (on)
                foreach (ItemDrop.ItemData it in inv.GetAllItems())
                {
                    int i = it.m_gridPos.y * w + it.m_gridPos.x;
                    if (i >= 0 && i < hit.Length && Matches(it, filter)) hit[i] = true;
                }
            for (int i = 0; i < els.Count; i++)
            {
                InventoryElement e = els[i] as InventoryElement;
                if (e == null) continue;
                Transform mark = e.transform.Find("Hoardkeeper_Hit");
                bool lit = on && hit[i];
                if (lit && mark == null) mark = NewHit(e.transform);
                if (mark != null && mark.gameObject.activeSelf != lit) mark.gameObject.SetActive(lit);
                if (on && !hit[i] && e.m_icon.enabled) e.m_icon.color = new Color(1f, 1f, 1f, 0.18f);
            }
        }

        private static Transform NewHit(Transform slot)
        {
            GameObject g = new GameObject("Hoardkeeper_Hit", typeof(RectTransform), typeof(Image));
            RectTransform r = (RectTransform)g.transform;
            r.SetParent(slot, false);
            r.SetAsFirstSibling();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(1f, 1f); r.offsetMax = new Vector2(-1f, -1f);
            Image im = g.GetComponent<Image>();
            im.color = new Color(0.35f, 0.85f, 1f, 0.45f);
            im.raycastTarget = false;
            return r;
        }

        private static void Status(Player p)
        {
            List<Container> live = Live();
            string filter = Filter();
            string text;
            if (_chosen != null || filter.Length > 0)
            {
                int total = 0, stacks = 0, chests = 0;
                foreach (Container c in live)
                {
                    bool any = false;
                    foreach (ItemDrop.ItemData it in c.GetInventory().GetAllItems())
                        if (Matches(it, filter)) { total += it.m_stack; stacks++; any = true; }
                    if (any) chests++;
                }
                int bag = 0;
                foreach (ItemDrop.ItemData it in p.GetInventory().GetAllItems()) if (Matches(it, filter)) bag += it.m_stack;
                string what = _chosen != null ? Shown(_chosen) : "\"" + filter + "\"";
                text = what + ": " + total + " in " + chests + " chest" + (chests == 1 ? "" : "s") + " (" + stacks + " stack" + (stacks == 1 ? "" : "s") + ")" + (bag > 0 ? ", " + bag + " in your bag" : "")
                    + (_chosen != null ? "   <color=#999999>right-click again to clear</color>" : "");
            }
            else
                text = live.Count + " chest" + (live.Count == 1 ? "" : "s") + " within " + Mathf.RoundToInt(Plugin.Range.Value) + " m   <color=#999999>sort by " + Actions.OrderName() + " · right-click an item to find it all · Ctrl + click sends</color>";
            if (_status.text != text) _status.text = text;
        }

        // ------------------------------------------------------------ typing: the game doesn't hear the keys

        private static bool Free(KeyCode k)
        {
            return k == KeyCode.Return || k == KeyCode.KeypadEnter || (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6)
                || k == KeyCode.LeftShift || k == KeyCode.RightShift || k == KeyCode.LeftControl || k == KeyCode.RightControl;
        }

        [HarmonyPatch]
        private static class KeyBlock
        {
            private static MethodBase TargetMethod() { return AccessTools.Method(typeof(ZInput), "TryGetKeyStateLowLevel"); }
            private static bool Prefix(KeyCode keyCode, ref bool __result)
            {
                if (!Typing() || Free(keyCode)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch]
        private static class ButtonBlock
        {
            private static MethodBase TargetMethod() { return AccessTools.Method(typeof(ZInput), "TryGetButtonState"); }
            private static bool Prefix(string name, ref bool __result)
            {
                if (!Typing() || name == null || name.StartsWith("Mouse")) return true;
                __result = false;
                return false;
            }
        }
    }
}
