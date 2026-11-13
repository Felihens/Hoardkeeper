using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hoardkeeper
{
    // A "Name" button in the game's own chest window: click it, type over the title, Enter. Same name as the
    // Storage window's Name (ZDO on the chest, shown when you look at it).
    [HarmonyPatch]
    internal static class ChestName
    {
        private static Button _btn;
        private static TMP_InputField _field;
        private static Container _for;
        private static RectTransform _tags;
        private static readonly List<KeyValuePair<string, Button>> _chips = new List<KeyValuePair<string, Button>>();
        private const float ChipW = 66f, ChipH = 26f;

        internal static bool Typing() { return _field != null && _field.gameObject.activeSelf && _field.isFocused; }

        [HarmonyPatch(typeof(InventoryGui), "Update"), HarmonyPostfix]
        private static void Tick(InventoryGui __instance)
        {
            try
            {
                if (__instance.m_container == null || __instance.m_takeAllButton == null) return;
                if (_btn == null) Build(__instance);
                Container c = Chests.OpenOne();
                if (c == null && _field != null && _field.gameObject.activeSelf) { _field.gameObject.SetActive(false); _for = null; }
                if (c == null && _tags != null && _tags.gameObject.activeSelf) _tags.gameObject.SetActive(false);
            }
            catch (Exception e) { Plugin.Log.LogWarning("Chest name button: " + e.Message); }
        }

        private static void Build(InventoryGui gui)
        {
            _btn = Ui.CloneButton(gui, gui.m_container, "Name", 60f, 26f, Start, "Give this chest a name (shown when you look at it and in the Storage window)");
            _btn.name = "Hoardkeeper_ChestName";
            RectTransform r = (RectTransform)_btn.transform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(1f, 0f);
            r.anchoredPosition = new Vector2(-12f, 12f);
            r.sizeDelta = new Vector2(60f, 26f);

            Corner(Ui.CloneButton(gui, gui.m_container, "Tags", 50f, 26f, ToggleTags, "Choose which kinds of item belong in this chest (same tags as in the Storage window)"), -76f, 50f);
            Corner(Ui.CloneButton(gui, gui.m_container, "Fill tags", 70f, 26f, FillHere, "Bring every item of this chest's tags here, from your bag and the chests nearby"), -130f, 70f);
            BuildTags(gui);

            _field = Ui.Field(gui, gui.m_container, "Chest name");
            _field.characterLimit = 30;
            _field.onEndEdit.AddListener(End);
            _field.gameObject.SetActive(false);
        }

        private static void Corner(Button b, float x, float w)
        {
            RectTransform r = (RectTransform)b.transform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(1f, 0f);
            r.anchoredPosition = new Vector2(x, 12f);
            r.sizeDelta = new Vector2(w, 26f);
        }

        // the tag buttons, in a small panel just under the chest window
        private static void BuildTags(InventoryGui gui)
        {
            const int per = 4;
            int rows = (Tags.All.Length + per - 1) / per;
            GameObject g = new GameObject("Hoardkeeper_ChestTags", typeof(RectTransform), typeof(Image));
            _tags = (RectTransform)g.transform;
            _tags.SetParent(gui.m_container, false);
            _tags.anchorMin = _tags.anchorMax = new Vector2(0.5f, 0f);
            _tags.pivot = new Vector2(0.5f, 1f);
            _tags.anchoredPosition = new Vector2(0f, -4f);
            _tags.sizeDelta = new Vector2(per * (ChipW + 4f) - 4f + 16f, rows * (ChipH + 4f) - 4f + 16f);
            Skin.Window(gui, _tags, g.GetComponent<Image>());
            for (int i = 0; i < Tags.All.Length; i++)
            {
                string tag = Tags.All[i];
                Button b = Ui.CloneButton(gui, _tags, tag, ChipW, ChipH, () => { Container c = Chests.OpenOne(); if (c != null) { Tags.Toggle(c, tag); Labels(); } }, null);
                RectTransform r = (RectTransform)b.transform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(8f + (i % per) * (ChipW + 4f), -8f - (i / per) * (ChipH + 4f));
                r.sizeDelta = new Vector2(ChipW, ChipH);
                _chips.Add(new KeyValuePair<string, Button>(tag, b));
            }
            g.SetActive(false);
        }

        private static void Labels()
        {
            Container c = Chests.OpenOne();
            if (c == null) return;
            HashSet<string> on = Tags.Of(c);
            foreach (KeyValuePair<string, Button> kv in _chips)
                Ui.Label(kv.Value, on.Contains(kv.Key) ? "<b><color=#ffcc55>" + kv.Key + "</color></b>" : "<color=#8a8a8a>" + kv.Key + "</color>");
        }

        private static void ToggleTags()
        {
            if (_tags == null || Chests.OpenOne() == null) return;
            bool on = !_tags.gameObject.activeSelf;
            _tags.gameObject.SetActive(on);
            if (on) { _tags.SetAsLastSibling(); Labels(); }
        }

        private static void FillHere()
        {
            Player p = Player.m_localPlayer;
            Container c = Chests.OpenOne();
            if (p == null || c == null) return;
            Tags.Fill(p, Chests.Near(p, Plugin.Range.Value), c);
        }

        private static void Start()
        {
            InventoryGui gui = InventoryGui.instance;
            Container c = Chests.OpenOne();
            if (gui == null || c == null) return;
            _for = c;
            // over the window's title
            RectTransform t = gui.m_containerName != null ? gui.m_containerName.rectTransform : null;
            RectTransform f = (RectTransform)_field.transform;
            if (t != null)
            {
                Vector3[] cs = new Vector3[4];
                t.GetWorldCorners(cs);
                f.anchorMin = f.anchorMax = new Vector2(0.5f, 0.5f);
                f.pivot = new Vector2(0.5f, 0.5f);
                f.position = (cs[0] + cs[2]) * 0.5f;
                f.sizeDelta = new Vector2(Mathf.Max(200f, t.rect.width), 30f);
            }
            _field.transform.SetAsLastSibling();
            _field.text = Chests.Name(c);
            _field.gameObject.SetActive(true);
            _field.Select();
            _field.ActivateInputField();
        }

        private static void End(string text)
        {
            Container c = _for;
            _for = null;
            if (_field != null) _field.gameObject.SetActive(false);
            if (c == null) return;
            if (text != Chests.Name(c)) { Chests.SetName(c, text); Plugin.Say(text.Trim().Length > 0 ? "Chest named " + text.Trim() : "Chest name removed"); }
        }
    }
}
