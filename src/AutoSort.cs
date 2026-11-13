using HarmonyLib;
using UnityEngine;

namespace Hoardkeeper
{
    // Optional (config Sort): sort your bag when the inventory opens or after a pickup, and a chest when it opens.
    [HarmonyPatch]
    internal static class AutoSort
    {
        private static float _due = -1f;

        private static bool Dragging()
        {
            return InventoryGui.instance != null && Traverse.Create(InventoryGui.instance).Field("m_dragItem").GetValue<ItemDrop.ItemData>() != null;
        }

        internal static void Tick(Player p)
        {
            if (_due < 0f || Time.time < _due) return;
            if (Dragging()) { _due = Time.time + 0.5f; return; }
            _due = -1f;
            Actions.SortMine(p, true);
        }

        [HarmonyPatch(typeof(Humanoid), "Pickup"), HarmonyPostfix]
        private static void Picked(Humanoid __instance, bool __result)
        {
            if (__result && Plugin.AutoSortPickup.Value && __instance == Player.m_localPlayer) _due = Time.time + 0.4f;
        }

        [HarmonyPatch(typeof(InventoryGui), "Show"), HarmonyPostfix]
        private static void Opened(Container container)
        {
            if (!Plugin.AutoSortOpen.Value || Player.m_localPlayer == null) return;
            try
            {
                if (container != null) Actions.SortChest(container);
                else Actions.SortMine(Player.m_localPlayer, true);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Auto sort failed: " + e.Message); }
        }
    }
}
