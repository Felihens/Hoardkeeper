using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Hoardkeeper
{
    internal static class Actions
    {
        private static Container Open() { return Chests.OpenOne(); }

        private static string L(string s) { return Localization.instance != null ? Localization.instance.Localize(s) : s; }

        // ------------------------------------------------------------ sort

        private static int Group(ItemDrop.ItemData it)
        {
            switch (it.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Torch: return 0;
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable: return 1;
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket: return 2;
                case ItemDrop.ItemData.ItemType.Tool: return 3;
                case ItemDrop.ItemData.ItemType.Consumable: return 4;
                case ItemDrop.ItemData.ItemType.Material: return 5;
                case ItemDrop.ItemData.ItemType.Trophy: return 7;
                default: return 6;
            }
        }

        private static int ByName(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            return string.Compare(L(a.m_shared.m_name), L(b.m_shared.m_name), StringComparison.CurrentCultureIgnoreCase);
        }

        private static int Compare(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            int c;
            switch (Plugin.Order.Value)
            {
                case Plugin.SortOrder.NameAZ: c = ByName(a, b); break;
                case Plugin.SortOrder.NameZA: c = ByName(b, a); break;
                case Plugin.SortOrder.Quality: c = b.m_quality.CompareTo(a.m_quality); break;
                case Plugin.SortOrder.Weight: c = b.GetWeight().CompareTo(a.GetWeight()); break;
                case Plugin.SortOrder.Amount: c = b.m_stack.CompareTo(a.m_stack); break;
                case Plugin.SortOrder.Value: c = (b.m_shared.m_value * b.m_stack).CompareTo(a.m_shared.m_value * a.m_stack); break;
                default: c = Group(a).CompareTo(Group(b)); break;
            }
            if (c == 0) c = ByName(a, b);
            if (c == 0) c = b.m_quality.CompareTo(a.m_quality);
            if (c == 0) c = b.m_stack.CompareTo(a.m_stack);
            return c;
        }

        private static readonly string[] OrderNames = { "type", "name A-Z", "name Z-A", "quality", "weight", "amount", "value" };
        internal static string OrderName() { return OrderNames[(int)Plugin.Order.Value]; }

        // Shift + T, or Shift + click on Sort
        internal static void NextOrder()
        {
            Plugin.Order.Value = (Plugin.SortOrder)(((int)Plugin.Order.Value + 1) % OrderNames.Length);
            Plugin.Say("Sort by " + OrderName());
        }

        // sorts inv; items for which keep() is true stay where they are, slots for which blocked() is true stay free
        internal static void Sort(Inventory inv, Func<ItemDrop.ItemData, bool> keep, Func<int, int, bool> blocked, int firstRow)
        {
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            List<ItemDrop.ItemData> move = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData it in all) if (!keep(it)) move.Add(it);
            // merge partial stacks of the same thing
            Dictionary<string, List<ItemDrop.ItemData>> same = new Dictionary<string, List<ItemDrop.ItemData>>();
            foreach (ItemDrop.ItemData it in move)
            {
                if (it.m_shared.m_maxStackSize <= 1 || it.m_customData.Count > 0) continue;
                string k = it.m_shared.m_name + "|" + it.m_quality + "|" + it.m_worldLevel;
                List<ItemDrop.ItemData> g;
                if (!same.TryGetValue(k, out g)) same[k] = g = new List<ItemDrop.ItemData>();
                g.Add(it);
            }
            foreach (List<ItemDrop.ItemData> g in same.Values)
            {
                if (g.Count < 2) continue;
                int total = 0;
                foreach (ItemDrop.ItemData it in g) total += it.m_stack;
                foreach (ItemDrop.ItemData it in g)
                {
                    int n = Mathf.Min(total, it.m_shared.m_maxStackSize);
                    it.m_stack = n;
                    total -= n;
                    if (n == 0) { all.Remove(it); move.Remove(it); }
                }
            }
            move.Sort(Compare);
            HashSet<long> taken = new HashSet<long>();
            foreach (ItemDrop.ItemData it in all) if (keep(it)) taken.Add(it.m_gridPos.x * 1000L + it.m_gridPos.y);
            int i = 0;
            for (int y = firstRow; y < inv.GetHeight() && i < move.Count; y++)
                for (int x = 0; x < inv.GetWidth() && i < move.Count; x++)
                {
                    if (taken.Contains(x * 1000L + y) || blocked(x, y)) continue;
                    move[i++].m_gridPos = new Vector2i(x, y);
                }
            // no room left for the rest (shouldn't happen): leave them where they were
            Chests.Changed(inv);
        }

        internal static void SortMine(Player p, bool quiet = false)
        {
            Sort(p.GetInventory(), it => Locks.Keep(p, it), (x, y) => Locks.IsLocked(p, x, y), 1);
            if (!quiet) Plugin.Say("Inventory sorted by " + OrderName());
        }

        internal static void SortChest(Container c)
        {
            Chests.Claim(c);
            Sort(c.GetInventory(), it => false, (x, y) => false, 0);
        }

        // T and the Sort button: the open chest if there is one, else your bag
        internal static void SortHere(Player p)
        {
            Container c = Open();
            if (c != null) { SortChest(c); Plugin.Say("Chest sorted by " + OrderName()); }
            else SortMine(p);
        }

        // ------------------------------------------------------------ stack into nearby chests

        internal static void QuickStack(Player p)
        {
            Inventory mine = p.GetInventory();
            int moved = 0, chests = 0;
            foreach (Container c in Chests.Near(p, Plugin.Range.Value))
            {
                Inventory ci = c.GetInventory();
                HashSet<string> has = new HashSet<string>();
                foreach (ItemDrop.ItemData o in ci.GetAllItems()) has.Add(o.m_shared.m_name);
                if (has.Count == 0) continue;
                bool used = false;
                foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(mine.GetAllItems()))
                {
                    if (Locks.Keep(p, it) || !has.Contains(it.m_shared.m_name)) continue;
                    if (!used) { Chests.Claim(c); used = true; }
                    moved += Chests.Move(mine, ci, it, it.m_stack);
                }
                if (used) chests++;
            }
            Plugin.Say(moved > 0 ? "Stacked " + moved + " items into " + chests + " chest" + (chests == 1 ? "" : "s") : "Nothing to stack nearby");
        }

        // ------------------------------------------------------------ restock from nearby chests

        internal static void Restock(Player p)
        {
            Inventory mine = p.GetInventory();
            List<Container> near = Chests.Near(p, Plugin.Range.Value);
            if (near.Count == 0) { Plugin.Say("No chests nearby"); return; }
            int got = 0;
            // stacks you carry that should be full: hotbar, ammo, locked slots
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(mine.GetAllItems()))
            {
                if (it.m_shared.m_maxStackSize <= 1 || it.m_stack >= it.m_shared.m_maxStackSize) continue;
                bool want = it.m_gridPos.y == 0 || Locks.IsLocked(p, it.m_gridPos.x, it.m_gridPos.y)
                    || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
                if (want) got += Fill(near, mine, it.m_shared.m_name, it.m_gridPos.x, it.m_gridPos.y, it.m_shared.m_maxStackSize - it.m_stack);
            }
            // empty locked slots that remember an item
            foreach (KeyValuePair<Vector2i, string> kv in Locks.All(p))
            {
                if (string.IsNullOrEmpty(kv.Value) || mine.GetItemAt(kv.Key.x, kv.Key.y) != null) continue;
                got += Fill(near, mine, kv.Value, kv.Key.x, kv.Key.y, int.MaxValue);
            }
            Plugin.Say(got > 0 ? "Restocked " + got + " items" : "Nothing to restock");
        }

        // brings up to want of this item into slot x,y from the chests
        private static int Fill(List<Container> near, Inventory mine, string name, int x, int y, int want)
        {
            int got = 0;
            foreach (Container c in near)
            {
                Inventory ci = c.GetInventory();
                foreach (ItemDrop.ItemData o in new List<ItemDrop.ItemData>(ci.GetAllItems()))
                {
                    if (want <= 0) return got;
                    if (o.m_shared.m_name != name) continue;
                    ItemDrop.ItemData there = mine.GetItemAt(x, y);
                    if (there != null && !there.IsSameType(o)) continue;
                    int room = there != null ? there.m_shared.m_maxStackSize - there.m_stack : o.m_shared.m_maxStackSize;
                    int n = Mathf.Min(want, room, o.m_stack);
                    if (n <= 0) return got;
                    Chests.Claim(c);
                    if (mine.MoveItemToThis(ci, o, n, x, y)) { got += n; want -= n; }
                }
            }
            return got;
        }

        // ------------------------------------------------------------ store all into the open chest

        internal static void StoreAll(Player p)
        {
            Container c = Open();
            if (c == null) return;
            Chests.Claim(c);
            Inventory mine = p.GetInventory(), ci = c.GetInventory();
            int moved = 0;
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(mine.GetAllItems()))
                if (!Locks.Keep(p, it)) moved += Chests.Move(mine, ci, it, it.m_stack);
            Plugin.Say(moved > 0 ? "Stored " + moved + " items" : "Nothing to store");
        }

        // ------------------------------------------------------------ many chests at once (the Storage window)

        internal static void SortAll(List<Container> chests)
        {
            foreach (Container c in chests) SortChest(c);
            Plugin.Say("Sorted " + chests.Count + " chest" + (chests.Count == 1 ? "" : "s") + " by " + OrderName());
        }

        // every kind of item goes to the chest that already holds the most of it, then all are sorted
        internal static void Tidy(List<Container> chests)
        {
            Dictionary<string, Container> best = new Dictionary<string, Container>();
            Dictionary<string, int> most = new Dictionary<string, int>();
            foreach (Container c in chests)
            {
                Dictionary<string, int> here = new Dictionary<string, int>();
                foreach (ItemDrop.ItemData o in c.GetInventory().GetAllItems())
                {
                    int n;
                    here.TryGetValue(o.m_shared.m_name, out n);
                    here[o.m_shared.m_name] = n + o.m_stack;
                }
                foreach (KeyValuePair<string, int> kv in here)
                {
                    int m;
                    if (!most.TryGetValue(kv.Key, out m) || kv.Value > m) { most[kv.Key] = kv.Value; best[kv.Key] = c; }
                }
            }
            int moved = 0;
            foreach (Container c in chests)
                foreach (ItemDrop.ItemData o in new List<ItemDrop.ItemData>(c.GetInventory().GetAllItems()))
                {
                    Container to;
                    if (!best.TryGetValue(o.m_shared.m_name, out to) || to == c) continue;
                    Chests.Claim(c);
                    Chests.Claim(to);
                    moved += Chests.Move(c.GetInventory(), to.GetInventory(), o, o.m_stack);
                }
            foreach (Container c in chests) SortChest(c);
            Plugin.Say("Tidied up: " + moved + " items moved, " + chests.Count + " chests sorted");
        }

        // brings into this chest every item of the kinds it already holds, from the other chests (and your bag)
        internal static void Gather(Player p, Container target, List<Container> chests, bool fromBag)
        {
            Inventory ti = target.GetInventory();
            HashSet<string> kinds = new HashSet<string>();
            foreach (ItemDrop.ItemData o in ti.GetAllItems()) kinds.Add(o.m_shared.m_name);
            if (kinds.Count == 0) { Plugin.Say("Put an item in this chest first: Gather brings more of what it holds"); return; }
            Chests.Claim(target);
            int moved = 0;
            foreach (Container c in chests)
            {
                if (c == target) continue;
                foreach (ItemDrop.ItemData o in new List<ItemDrop.ItemData>(c.GetInventory().GetAllItems()))
                {
                    if (!kinds.Contains(o.m_shared.m_name)) continue;
                    Chests.Claim(c);
                    moved += Chests.Move(c.GetInventory(), ti, o, o.m_stack);
                }
            }
            if (fromBag)
                foreach (ItemDrop.ItemData o in new List<ItemDrop.ItemData>(p.GetInventory().GetAllItems()))
                    if (kinds.Contains(o.m_shared.m_name) && !Locks.Keep(p, o)) moved += Chests.Move(p.GetInventory(), ti, o, o.m_stack);
            Plugin.Say(moved > 0 ? "Gathered " + moved + " items into " + Chests.Title(target) : "Nothing to gather");
        }

        internal static void TakeAll(Player p, Container c)
        {
            Chests.Claim(c);
            int moved = 0;
            foreach (ItemDrop.ItemData o in new List<ItemDrop.ItemData>(c.GetInventory().GetAllItems()))
                moved += Chests.Move(c.GetInventory(), p.GetInventory(), o, o.m_stack);
            Plugin.Say(moved > 0 ? "Took " + moved + " items" : "Nothing taken (is your bag full?)");
        }

        // Ctrl + click in your bag with the Storage window open: into a chest that holds that item, else any with room
        // where the last SendToChests put the item, and how many
        internal static Container LastTo;
        internal static int LastMoved;

        internal static bool SendToChests(Player p, ItemDrop.ItemData it, List<Container> chests)
        {
            LastTo = null; LastMoved = 0;
            if (it.m_shared.m_questItem) return false;
            // a chest tagged with the item's category comes first (one already holding it before an empty one)
            string cat = Tags.Category(it);
            List<Container> tagged = new List<Container>(), rest = new List<Container>();
            foreach (Container c in chests) (Tags.Of(c).Contains(cat) ? tagged : rest).Add(c);
            tagged.AddRange(rest);
            chests = tagged;
            Container to = null;
            foreach (Container c in chests)
                if (Tags.Of(c).Contains(cat) && Chests.Space(c.GetInventory(), it) > 0)
                {
                    if (to == null) to = c;
                    if (Chests.Count(c.GetInventory(), it.m_shared.m_name) > 0) { to = c; break; }
                }
            if (to == null)
            foreach (Container c in chests)
                if (Chests.Count(c.GetInventory(), it.m_shared.m_name) > 0 && Chests.Space(c.GetInventory(), it) > 0) { to = c; break; }
            if (to == null)
                foreach (Container c in chests)
                    if (Chests.Space(c.GetInventory(), it) > 0) { to = c; break; }
            if (to == null) { Plugin.Say("No room in the chests"); return false; }
            if (it.m_equipped) p.UnequipItem(it, false);
            Chests.Claim(to);
            LastMoved = Chests.Move(p.GetInventory(), to.GetInventory(), it, it.m_stack);
            LastTo = to;
            return LastMoved > 0;
        }

        // ------------------------------------------------------------ trash

        // the trash can, clicked with an item in hand: asks, then destroys it
        internal static void Trash(Player p)
        {
            InventoryGui gui = InventoryGui.instance;
            Traverse t = Traverse.Create(gui);
            ItemDrop.ItemData drag = t.Field("m_dragItem").GetValue<ItemDrop.ItemData>();
            Inventory from = t.Field("m_dragInventory").GetValue<Inventory>();
            if (drag == null) { Plugin.Say("Pick up an item and put it on the trash can to destroy it"); return; }
            int amount = t.Field("m_dragAmount").GetValue<int>();
            if (amount <= 0 || amount > drag.m_stack) amount = drag.m_stack;
            string what = (amount > 1 ? amount + " " : "") + L(drag.m_shared.m_name);
            UnifiedPopup.Push(new YesNoPopup("Destroy item", "Destroy " + what + "? This can't be undone.", () =>
            {
                UnifiedPopup.Pop();
                if (from == null || !from.ContainsItem(drag)) return;
                if (drag.m_equipped) p.UnequipItem(drag, false);
                from.RemoveItem(drag, amount);
                Traverse.Create(InventoryGui.instance).Method("SetupDragItem", new[] { typeof(ItemDrop.ItemData), typeof(Inventory), typeof(int) }).GetValue(null, null, 1);
                Plugin.Say("Destroyed " + what);
            }, () => UnifiedPopup.Pop(), false));
        }
    }
}
