using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Hoardkeeper
{
    // Finding the chests around you that you may use, their names and skip marks, and moving items between
    // inventories.
    [HarmonyPatch]
    internal static class Chests
    {
        private static readonly MethodInfo ChangedM = AccessTools.Method(typeof(Inventory), "Changed", new[] { typeof(bool), typeof(bool) });
        private static readonly FieldInfo NView = AccessTools.Field(typeof(Container), "m_nview");
        private static readonly FieldInfo PieceF = AccessTools.Field(typeof(Container), "m_piece");
        private static readonly FieldInfo InUseF = AccessTools.Field(typeof(Container), "m_inUse");
        private static readonly MethodInfo UseVisual = AccessTools.Method(typeof(Container), "UpdateUseVisual");
        private const string NameKey = "Hoardkeeper_name", SkipKey = "Hoardkeeper_skip_";

        internal static void Changed(Inventory inv)
        {
            if (inv != null && ChangedM != null) ChangedM.Invoke(inv, new object[] { false, false });
        }

        internal static ZNetView View(Container c) { return c != null ? NView.GetValue(c) as ZNetView : null; }

        internal static Container OpenOne()
        {
            return InventoryGui.instance != null ? Traverse.Create(InventoryGui.instance).Field("m_currentContainer").GetValue<Container>() : null;
        }

        // chests you could open yourself right now: built pieces (no tombstones), not opened by someone else,
        // inside wards that let you in, nearest first. Skipped chests only when asked for.
        internal static List<Container> Near(Player p, float range, bool withSkipped = false)
        {
            List<Container> list = new List<Container>();
            Container open = OpenOne();
            Vector3 at = p.transform.position;
            foreach (Container c in Object.FindObjectsOfType<Container>())
            {
                if (c == null || c.GetComponent<TombStone>() != null) continue;
                if (Vector3.Distance(at, c.transform.position) > range) continue;
                ZNetView nv = View(c);
                if (nv == null || !nv.IsValid()) continue;
                bool ours = c == open || Storage.Holds(c);
                if (!ours && (c.IsInUse() || (!nv.IsOwner() && nv.GetZDO().GetInt(ZDOVars.s_inUse) == 1))) continue;
                Piece piece = PieceF.GetValue(c) as Piece;
                if (piece == null) continue;
                if (c.m_privacy == Container.PrivacySetting.Private && piece.GetCreator() != p.GetPlayerID()) continue;
                if (c.m_privacy == Container.PrivacySetting.Group) continue;
                if (!PrivateArea.CheckAccess(c.transform.position, 0f, false, false)) continue;
                if (c.GetInventory() == null) continue;
                if (!withSkipped && Skipped(c)) continue;
                list.Add(c);
            }
            list.Sort((a, b) => Vector3.Distance(at, a.transform.position).CompareTo(Vector3.Distance(at, b.transform.position)));
            return list;
        }

        // we change its contents: become its owner first so the change is saved (the game's own Place stacks does
        // the same with the open chest)
        internal static void Claim(Container c)
        {
            ZNetView nv = View(c);
            if (nv != null && nv.IsValid() && !nv.IsOwner()) nv.ClaimOwnership();
        }

        // opened lid and "in use" for others, without the open sound (many chests open at once)
        internal static void MarkInUse(Container c, bool on)
        {
            ZNetView nv = View(c);
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            if ((bool)InUseF.GetValue(c) == on) return;
            InUseF.SetValue(c, on);
            UseVisual.Invoke(c, null);
        }

        // ------------------------------------------------------------ names and skip marks (on the chest)

        internal static string Name(Container c)
        {
            ZNetView nv = View(c);
            return nv != null && nv.IsValid() ? nv.GetZDO().GetString(NameKey, "") : "";
        }

        internal static void SetName(Container c, string name)
        {
            ZNetView nv = View(c);
            if (nv == null || !nv.IsValid()) return;
            Claim(c);
            nv.GetZDO().Set(NameKey, (name ?? "").Trim());
        }

        internal static string Title(Container c)
        {
            string n = Name(c);
            return n.Length > 0 ? n : Localization.instance.Localize(c.GetInventory().GetName());
        }

        // skipping is yours alone: the mark carries your player id
        internal static bool Skipped(Container c)
        {
            ZNetView nv = View(c);
            return nv != null && nv.IsValid() && Player.m_localPlayer != null && nv.GetZDO().GetBool(SkipKey + Player.m_localPlayer.GetPlayerID(), false);
        }

        internal static void SetSkipped(Container c, bool on)
        {
            ZNetView nv = View(c);
            if (nv == null || !nv.IsValid() || Player.m_localPlayer == null) return;
            Claim(c);
            nv.GetZDO().Set(SkipKey + Player.m_localPlayer.GetPlayerID(), on);
        }

        // the name shows when you look at the chest, and as the title of its window
        [HarmonyPatch(typeof(Container), "GetHoverText"), HarmonyPostfix]
        private static void Hover(Container __instance, ref string __result)
        {
            string n = Name(__instance);
            if (n.Length > 0) __result = "<color=#ffcc66>" + n + "</color>" + (Skipped(__instance) ? " <color=#888888>(skipped)</color>" : "") + "\n" + __result;
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateContainer"), HarmonyPostfix]
        private static void WindowTitle(InventoryGui __instance)
        {
            Container c = OpenOne();
            if (c == null || __instance.m_containerName == null) return;
            string n = Name(c);
            if (n.Length > 0) __instance.m_containerName.text = n;
        }

        // ------------------------------------------------------------ moving

        // how many of this item still fit
        internal static int Space(Inventory inv, ItemDrop.ItemData item)
        {
            int free = inv.GetWidth() * inv.GetHeight() - inv.GetAllItems().Count;
            int room = free * item.m_shared.m_maxStackSize;
            foreach (ItemDrop.ItemData o in inv.GetAllItems())
                if (o.m_shared.m_name == item.m_shared.m_name && o.m_quality == item.m_quality && o.m_worldLevel == item.m_worldLevel)
                    room += Mathf.Max(0, o.m_shared.m_maxStackSize - o.m_stack);
            return room;
        }

        // moves up to amount of item from one inventory to another (stacking onto stacks there first); returns how many
        internal static int Move(Inventory from, Inventory to, ItemDrop.ItemData item, int amount)
        {
            int n = Mathf.Min(amount, item.m_stack, Space(to, item));
            if (n <= 0) return 0;
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = n;
            copy.m_equipped = false;
            if (!to.AddItem(copy)) return 0;
            from.RemoveItem(item, n);
            return n;
        }

        internal static int Count(Inventory inv, string name)
        {
            int n = 0;
            foreach (ItemDrop.ItemData o in inv.GetAllItems()) if (o.m_shared.m_name == name) n += o.m_stack;
            return n;
        }
    }
}
