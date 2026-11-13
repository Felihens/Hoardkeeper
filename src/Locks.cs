using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Hoardkeeper
{
    // Locked slots, saved on your character (Player.m_customData).
    // A locked slot remembers the item it held when you locked it (its m_name), or "" if it was empty:
    // Restock fills it again with that item. Favorites are kinds of item (by m_name) that never leave your bag.
    internal static class Locks
    {
        private const string LockKey = "Hoardkeeper_locks";
        private static Player _for;
        private static readonly Dictionary<long, string> _locks = new Dictionary<long, string>();   // key: x * 1000 + y

        private static long K(int x, int y) { return x * 1000L + y; }

        private static void Load(Player p)
        {
            if (p == _for) return;
            _for = p;
            _locks.Clear();
            string s;
            if (p.m_customData.TryGetValue(LockKey, out s))
                foreach (string part in s.Split(';'))
                {
                    string[] f = part.Split(new[] { ',' }, 3);
                    int x, y;
                    if (f.Length >= 2 && int.TryParse(f[0], out x) && int.TryParse(f[1], out y)) _locks[K(x, y)] = f.Length > 2 ? f[2] : "";
                }
        }

        private static void Save(Player p)
        {
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<long, string> kv in _locks)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(kv.Key / 1000).Append(',').Append(kv.Key % 1000).Append(',').Append(kv.Value);
            }
            p.m_customData[LockKey] = sb.ToString();
        }

        internal static bool IsLocked(Player p, int x, int y) { Load(p); return _locks.ContainsKey(K(x, y)); }

        internal static string LockedItem(Player p, int x, int y)
        {
            Load(p);
            string s;
            return _locks.TryGetValue(K(x, y), out s) ? s : null;
        }

        internal static IEnumerable<KeyValuePair<Vector2i, string>> All(Player p)
        {
            Load(p);
            foreach (KeyValuePair<long, string> kv in _locks) yield return new KeyValuePair<Vector2i, string>(new Vector2i((int)(kv.Key / 1000), (int)(kv.Key % 1000)), kv.Value);
        }

        internal static void Toggle(Player p, int x, int y)
        {
            Load(p);
            long k = K(x, y);
            if (_locks.ContainsKey(k)) { _locks.Remove(k); Plugin.Say("Slot unlocked"); }
            else
            {
                ItemDrop.ItemData it = p.GetInventory().GetItemAt(x, y);
                _locks[k] = it != null ? it.m_shared.m_name : "";
                Plugin.Say(it != null ? "Slot locked for " + Localization.instance.Localize(it.m_shared.m_name) + " (Restock refills it)" : "Slot locked");
            }
            Save(p);
        }

        // ------------------------------------------------------------ favorite kinds of item

        private const string FavKey = "Hoardkeeper_favs";
        private static Player _favFor;
        private static readonly HashSet<string> _favs = new HashSet<string>();

        private static void LoadFavs(Player p)
        {
            if (p == _favFor) return;
            _favFor = p;
            _favs.Clear();
            string s;
            if (p.m_customData.TryGetValue(FavKey, out s))
                foreach (string n in s.Split(';')) if (n.Length > 0) _favs.Add(n);
        }

        internal static bool IsFavorite(Player p, ItemDrop.ItemData it) { LoadFavs(p); return it != null && _favs.Contains(it.m_shared.m_name); }

        internal static void ToggleFavorite(Player p, ItemDrop.ItemData it)
        {
            LoadFavs(p);
            string n = it.m_shared.m_name, shown = Localization.instance.Localize(n);
            if (_favs.Remove(n)) Plugin.Say(shown + " is no longer a favorite");
            else { _favs.Add(n); Plugin.Say(shown + " is a favorite: it stays with you"); }
            p.m_customData[FavKey] = string.Join(";", new List<string>(_favs).ToArray());
        }

        // outside the normal bag grid: equipment / quick / quiver slots of CDEV.EquipmentSlots (rows 20+) and the like
        internal static bool OutsideGrid(Player p, ItemDrop.ItemData it)
        {
            Inventory inv = p.GetInventory();
            return it.m_gridPos.x < 0 || it.m_gridPos.y < 0 || it.m_gridPos.x >= inv.GetWidth() || it.m_gridPos.y >= inv.GetHeight();
        }

        // the things nothing should move: hotbar row, locked slots, favorites, worn gear, extra slots from other mods
        internal static bool Keep(Player p, ItemDrop.ItemData it)
        {
            return it.m_gridPos.y == 0 || OutsideGrid(p, it) || it.m_equipped || IsLocked(p, it.m_gridPos.x, it.m_gridPos.y) || IsFavorite(p, it);
        }
    }
}
