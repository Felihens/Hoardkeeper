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
    // The button column right of the chest window (copies of the game's own "Take all" button, so they look
    // like Valheim's), the lock marks on your slots, and Alt + click to lock a slot.
    [HarmonyPatch]
    internal static class Ui
    {
        private static RectTransform _column;
        private static readonly Vector3[] Corners = new Vector3[4];
        internal static RectTransform Column { get { return _column; } }
        private static Button _store;
        private static readonly List<Button> _all = new List<Button>();
        private const float W = 110f, H = 30f, Gap = 4f, Can = 52f;

        private static readonly FieldInfo Elements = AccessTools.Field(typeof(InventoryGrid), "m_elements");
        private static readonly FieldInfo DragF = AccessTools.Field(typeof(InventoryGui), "m_dragItem");

        // ------------------------------------------------------------ buttons

        [HarmonyPatch(typeof(InventoryGui), "Update"), HarmonyPostfix]
        private static void Tick(InventoryGui __instance)
        {
            if (__instance.m_player == null || __instance.m_takeAllButton == null) return;
            if (_column == null) Build(__instance);
            if (_column == null || !InventoryGui.IsVisible()) return;
            // beside the chest window (its top-right corner), not beside your inventory, where the game shows your
            // armor; the chest window keeps its place when no chest is open, so the column doesn't move
            if (__instance.m_container != null)
            {
                __instance.m_container.GetWorldCorners(Corners);   // 2 = top right
                _column.position = Corners[2];
                _column.anchoredPosition += Plugin.ButtonsOffset.Value;
            }
            else _column.anchoredPosition = Plugin.ButtonsOffset.Value;
            _store.gameObject.SetActive(__instance.IsContainerOpen());
            float y = 0f;
            foreach (Button b in _all)
            {
                if (!b.gameObject.activeSelf) continue;
                RectTransform r = (RectTransform)b.transform;
                r.anchoredPosition = new Vector2(r.sizeDelta.x < W ? (W - r.sizeDelta.x) / 2f : 0f, y);
                y -= r.sizeDelta.y + Gap;
            }
        }

        private static void Build(InventoryGui gui)
        {
            _all.Clear();
            GameObject col = new GameObject("Hoardkeeper_Buttons", typeof(RectTransform));
            _column = (RectTransform)col.transform;
            _column.SetParent(gui.m_player, false);
            _column.anchorMin = _column.anchorMax = new Vector2(1f, 1f);
            _column.pivot = new Vector2(0f, 1f);
            _column.sizeDelta = new Vector2(W, 300f);
            Make(gui, "Storage", Storage.Toggle, "Every chest around you in one window (Shift + F)");
            Make(gui, "Sort", () => { if (Shift()) Actions.NextOrder(); else Actions.SortHere(Player.m_localPlayer); }, "Sorts the open chest, or your bag when no chest is open (T). Shift + click: change the order");
            Make(gui, "Stack", () => Actions.QuickStack(Player.m_localPlayer), "Into nearby chests that hold the same item (Shift + Q)");
            Make(gui, "Restock", () => Actions.Restock(Player.m_localPlayer), "Top up hotbar, ammo and locked slots (Shift + R)");
            _store = Make(gui, "Store all", () => Actions.StoreAll(Player.m_localPlayer), "Everything but hotbar, locked and worn into this chest");
            Button trash = Make(gui, "", () => Actions.Trash(Player.m_localPlayer), "Put an item here to destroy it");
            ((RectTransform)trash.transform).sizeDelta = new Vector2(Can, Can);
            UITooltip tt = trash.GetComponent<UITooltip>();
            if (tt != null) tt.Set("Trash", "Put an item here to destroy it");
            GameObject ic = new GameObject("Can", typeof(RectTransform), typeof(Image));
            RectTransform ir = (RectTransform)ic.transform;
            ir.SetParent(trash.transform, false);
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(9f, 8f); ir.offsetMax = new Vector2(-9f, -8f);
            Image img = ic.GetComponent<Image>();
            img.sprite = TrashSprite();
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        internal static bool Shift() { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); }

        private static Button Make(InventoryGui gui, string label, Action click, string tip)
        {
            Button b = CloneButton(gui, _column, label, W, H, click, tip);
            _all.Add(b);
            return b;
        }

        // a copy of the game's own Take all button
        internal static Button CloneButton(InventoryGui gui, Transform parent, string label, float w, float h, Action click, string tip)
        {
            GameObject g = UnityEngine.Object.Instantiate(gui.m_takeAllButton.gameObject, parent, false);
            g.name = "Hoardkeeper_" + (label.Length > 0 ? label : "Trash");
            g.SetActive(true);
            RectTransform r = (RectTransform)g.transform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(w, h);
            Button b = g.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(() => { try { if (Player.m_localPlayer != null) click(); } catch (Exception e) { Plugin.Log.LogWarning(g.name + " failed: " + e); } });
            foreach (UIGamePad gp in g.GetComponentsInChildren<UIGamePad>(true)) UnityEngine.Object.Destroy(gp);
            TMP_Text t = g.GetComponentInChildren<TMP_Text>(true);
            if (t != null)
            {
                t.text = label; t.enableAutoSizing = false; t.fontSize = h < 28f ? 14 : 16;
                // the copied label keeps margins that hide it in short buttons: fill the button instead
                RectTransform tr = t.rectTransform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(2f, 0f); tr.offsetMax = new Vector2(-2f, 0f);
                t.alignment = TextAlignmentOptions.Center;
                t.enableWordWrapping = false;
                t.overflowMode = TextOverflowModes.Overflow;
                if (label.Length == 0) t.gameObject.SetActive(false);
            }
            UITooltip tt = g.GetComponent<UITooltip>();
            if (tt != null) { if (tip != null) tt.Set(label, tip); else UnityEngine.Object.Destroy(tt); }
            return b;
        }

        internal static void Label(Button b, string label)
        {
            TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
            if (t != null && t.text != label) t.text = label;
        }

        private static TMP_FontAsset _font;
        internal static TMP_FontAsset Font(InventoryGui gui)
        {
            if (_font == null && gui.m_takeAllButton != null)
            {
                TMP_Text t = gui.m_takeAllButton.GetComponentInChildren<TMP_Text>(true);
                if (t != null) _font = t.font;
            }
            return _font;
        }

        internal static TextMeshProUGUI Text(InventoryGui gui, Transform parent, float size, Color color, TextAlignmentOptions align)
        {
            GameObject g = new GameObject("Text", typeof(RectTransform));
            g.transform.SetParent(parent, false);
            TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
            if (Font(gui) != null) t.font = Font(gui);
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        // a plain text field (the search box)
        internal static TMP_InputField Field(InventoryGui gui, Transform parent, string placeholder)
        {
            GameObject g = new GameObject("Field", typeof(RectTransform));
            g.SetActive(false);
            g.transform.SetParent(parent, false);
            Image bg = g.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            GameObject area = new GameObject("Area", typeof(RectTransform));
            RectTransform ar = (RectTransform)area.transform;
            ar.SetParent(g.transform, false);
            ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(8f, 2f); ar.offsetMax = new Vector2(-8f, -2f);
            area.AddComponent<RectMask2D>();
            TextMeshProUGUI ph = Text(gui, ar, 16f, new Color(0.7f, 0.7f, 0.7f, 0.6f), TextAlignmentOptions.MidlineLeft);
            ph.text = placeholder;
            ph.fontStyle = FontStyles.Italic;
            TextMeshProUGUI tx = Text(gui, ar, 16f, Color.white, TextAlignmentOptions.MidlineLeft);
            foreach (TextMeshProUGUI t in new[] { ph, tx })
            {
                RectTransform tr = t.rectTransform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = tr.offsetMax = Vector2.zero;
            }
            TMP_InputField f = g.AddComponent<TMP_InputField>();
            f.textViewport = ar;
            f.textComponent = tx;
            f.placeholder = ph;
            if (Font(gui) != null) f.fontAsset = Font(gui);
            f.pointSize = 16f;
            f.lineType = TMP_InputField.LineType.SingleLine;
            f.characterLimit = 40;
            g.SetActive(true);
            return f;
        }

        // a trash can, drawn here (light grey with dark ribs)
        private static Sprite _trash;
        private static Sprite TrashSprite()
        {
            if (_trash != null) return _trash;
            const int N = 32;
            Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f), body = new Color(0.86f, 0.82f, 0.74f, 1f), rib = new Color(0.35f, 0.3f, 0.25f, 1f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Color c = clear;
                    // body: a slightly narrowing bin, y 2..22
                    if (y >= 2 && y <= 22)
                    {
                        float half = 9f + (y - 2) * 0.12f;
                        if (Mathf.Abs(x - 15.5f) <= half)
                        {
                            c = body;
                            if ((x == 11 || x == 15 || x == 16 || x == 20) && y >= 5 && y <= 19) c = rib;
                        }
                    }
                    // lid and handle
                    if (y >= 24 && y <= 26 && x >= 3 && x <= 28) c = body;
                    if (y >= 27 && y <= 29 && x >= 12 && x <= 19 && !(y <= 28 && x >= 14 && x <= 17)) c = body;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            _trash = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
            return _trash;
        }

        // ------------------------------------------------------------ lock marks on your slots

        [HarmonyPatch(typeof(InventoryGrid), "UpdateInventory"), HarmonyPostfix]
        private static void Marks(InventoryGrid __instance, Inventory inventory, Player player)
        {
            if (player == null || player != Player.m_localPlayer || InventoryGui.instance == null || __instance != InventoryGui.instance.m_playerGrid) return;
            IList els = Elements.GetValue(__instance) as IList;
            if (els == null) return;
            int w = inventory.GetWidth();
            for (int i = 0; i < els.Count; i++)
            {
                InventoryElement e = els[i] as InventoryElement;
                if (e == null) continue;
                ItemDrop.ItemData it = inventory.GetItemAt(i % w, i / w);
                Transform fav = e.transform.Find("Hoardkeeper_Fav");
                bool isFav = it != null && Locks.IsFavorite(player, it);
                if (isFav && fav == null) fav = NewFav(e.transform);
                if (fav != null && fav.gameObject.activeSelf != isFav) fav.gameObject.SetActive(isFav);
                bool locked = Locks.IsLocked(player, i % w, i / w);
                Transform mark = e.transform.Find("Hoardkeeper_Lock");
                if (!locked) { if (mark != null && mark.gameObject.activeSelf) mark.gameObject.SetActive(false); continue; }
                if (mark == null) mark = NewMark(e.transform);
                if (!mark.gameObject.activeSelf) mark.gameObject.SetActive(true);
            }
        }

        private static Transform NewMark(Transform slot)
        {
            GameObject g = new GameObject("Hoardkeeper_Lock", typeof(RectTransform), typeof(Image));
            RectTransform r = (RectTransform)g.transform;
            r.SetParent(slot, false);
            r.SetAsFirstSibling();   // under the icon
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(2f, 2f); r.offsetMax = new Vector2(-2f, -2f);
            Image im = g.GetComponent<Image>();
            im.color = new Color(1f, 0.55f, 0.1f, 0.35f);
            im.raycastTarget = false;
            // a small corner tab, so it shows even under a big icon
            GameObject tab = new GameObject("Tab", typeof(RectTransform), typeof(Image));
            RectTransform tr = (RectTransform)tab.transform;
            tr.SetParent(slot, false);
            tr.anchorMin = tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(1f, 1f);
            tr.anchoredPosition = new Vector2(-3f, -3f);
            tr.sizeDelta = new Vector2(9f, 9f);
            Image ti = tab.GetComponent<Image>();
            ti.color = new Color(1f, 0.6f, 0.15f, 0.95f);
            ti.raycastTarget = false;
            tab.transform.SetParent(r, true);
            return r;
        }

        // favorites: a small gold diamond in the top-left corner
        private static Transform NewFav(Transform slot)
        {
            GameObject g = new GameObject("Hoardkeeper_Fav", typeof(RectTransform), typeof(Image));
            RectTransform r = (RectTransform)g.transform;
            r.SetParent(slot, false);
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(9f, -9f);
            r.sizeDelta = new Vector2(8f, 8f);
            r.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image im = g.GetComponent<Image>();
            im.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            im.raycastTarget = false;
            return r;
        }

        // Alt + right-click an item in your bag: make that kind of item a favorite (or not)
        [HarmonyPatch(typeof(InventoryGui), "OnRightClickItem"), HarmonyPrefix]
        private static bool AltRightClick(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
        {
            if (!(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return true;
            if (grid != __instance.m_playerGrid || item == null || Player.m_localPlayer == null) return true;
            Locks.ToggleFavorite(Player.m_localPlayer, item);
            return false;
        }

        // Alt + click a slot of your bag: lock or unlock it
        [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem"), HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool AltClick(InventoryGui __instance, InventoryGrid grid, Vector2i pos)
        {
            if (!(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return true;
            if (grid != __instance.m_playerGrid || Player.m_localPlayer == null) return true;
            if (DragF.GetValue(__instance) != null) return true;
            if (pos.x < 0 || pos.y < 0) return true;
            Locks.Toggle(Player.m_localPlayer, pos.x, pos.y);
            return false;
        }

    }
}
