using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hoardkeeper
{
    // The game's own window textures, found at run time (nothing copied from other mods):
    // the inventory window's "Bkg" image for the Storage window, and the inner panel texture for each chest card.
    internal static class Skin
    {
        private static Dictionary<string, Sprite> _sprites;

        private static Sprite Find(params string[] names)
        {
            if (_sprites == null)
            {
                _sprites = new Dictionary<string, Sprite>();
                foreach (Sprite sp in Resources.FindObjectsOfTypeAll<Sprite>())
                    if (sp != null && !_sprites.ContainsKey(sp.name)) _sprites[sp.name] = sp;
            }
            foreach (string n in names)
            {
                Sprite sp;
                if (_sprites.TryGetValue(n, out sp)) return sp;
            }
            return null;
        }

        // the dark wood of the game's inventory / crafting windows (the same art CDEV's equipment panel finds):
        // the biggest visible image under the inventory screen whose sprite is one of those panels
        private static readonly string[] Art = { "woodpanel_playerinventory", "woodpanel_info", "woodpanel_crafting" };

        internal static void Window(InventoryGui gui, RectTransform panel, Image img)
        {
            Image art = null;
            Transform root = gui.m_player != null ? gui.m_player.parent : null;
            foreach (string want in Art)
            {
                art = Biggest(root, want, panel) ?? Biggest(gui.m_player, want, panel);
                if (art != null) break;
            }
            if (art != null)
            {
                img.sprite = art.sprite;
                img.type = art.type;
                img.fillCenter = true;
                img.color = art.color;
                img.material = art.material;
                img.pixelsPerUnitMultiplier = art.pixelsPerUnitMultiplier;
                Plugin.Log.LogInfo("Storage background: " + art.sprite.name + " from " + art.name);
                return;
            }
            Plugin.Log.LogInfo("Storage background: window art not found, plain colour");
            img.color = new Color(0.16f, 0.12f, 0.08f, 0.97f);
        }

        private static Image Biggest(Transform root, string sprite, Transform skip)
        {
            if (root == null) return null;
            Image best = null;
            float area = 0f;
            foreach (Image im in root.GetComponentsInChildren<Image>(true))
            {
                if (im.sprite == null || im.color.a < 0.5f || (skip != null && im.transform.IsChildOf(skip))) continue;
                string n = im.sprite.name;
                if (n.IndexOf(sprite, System.StringComparison.OrdinalIgnoreCase) < 0 || n.IndexOf("mask", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                Rect r = im.rectTransform.rect;
                if (r.width * r.height > area) { area = r.width * r.height; best = im; }
            }
            return best;
        }

        internal static void Card(Image img, bool dim)
        {
            // just a light shade, so the game's slot squares show on it
            img.color = dim ? new Color(0.3f, 0.06f, 0.03f, 0.35f) : new Color(0f, 0f, 0f, 0.25f);
        }
    }
}
