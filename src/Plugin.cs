using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Hoardkeeper
{
    // Personal mod (never published): inventory and chest tools, written from scratch (2026-11-12) to replace
    // Storage Master and Quick Stack Store (everything both had, nothing twice).
    //   Storage     one window with every chest around you (Shift + F or the Storage button): drag items between
    //               them, Ctrl + click to send, search, right-click to choose an item, Gather, Tidy, Sort all,
    //               per-chest Sort / Take all / Name / Skip, hide your bag to see more
    //   Sort        tidies your bag (below the hotbar), merging stacks; T sorts the open chest instead when one is
    //               open; Shift + T (or Shift + click Sort) changes the sort order
    //   Lock        Alt + click a slot: nothing moves it. The lock remembers the item it held (a "loadout" slot)
    //   Favorite    Alt + right-click an item: that kind of item is never stacked, stored or sorted away
    //   Stack       puts your items into nearby chests that already hold that item
    //   Restock     tops up your hotbar, ammo and locked slots from nearby chests (refills empty locked slots too)
    //   Store       moves everything (not hotbar, locked, favorite or equipped) into the open chest
    //   Trash       the trash can: click it with an item in hand, confirm, and it's destroyed
    // (user, 2026-11-12: no Fetch, the crafting-from-chests mod already does it; no junk marks; no Find beams;
    //  T sorts the open chest when one is open, else your bag; range 50 m)
    [HarmonyPatch]
    [BepInPlugin("felih.Hoardkeeper", "Hoardkeeper", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal enum SortOrder { Type, NameAZ, NameZA, Quality, Weight, Amount, Value }

        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<int> MaxChests;
        internal static ConfigEntry<KeyboardShortcut> StackKey, RestockKey, SortKey, StorageKey;
        internal static ConfigEntry<Vector2> ButtonsOffset;
        internal static ConfigEntry<float> StorageHeight, StorageWidth, ScrollSpeed;
        internal static ConfigEntry<SortOrder> Order;
        internal static ConfigEntry<bool> AutoSortOpen, AutoSortPickup, GatherFromBag;
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Range = Config.Bind("General", "ChestRange", 50f, new ConfigDescription("How far (metres) Storage, Stack and Restock reach for chests.", new AcceptableValueRange<float>(5f, 50f)));
            MaxChests = Config.Bind("General", "MaxChests", 60, new ConfigDescription("Most chests the Storage window shows (nearest first).", new AcceptableValueRange<int>(4, 60)));
            StorageKey = Config.Bind("Keys", "Storage", new KeyboardShortcut(KeyCode.F, KeyCode.LeftShift), "Opens (or closes) the Storage window with every chest around you.");
            StackKey = Config.Bind("Keys", "Stack", new KeyboardShortcut(KeyCode.Q, KeyCode.LeftShift), "Put your items into nearby chests that hold the same item.");
            RestockKey = Config.Bind("Keys", "Restock", new KeyboardShortcut(KeyCode.R, KeyCode.LeftShift), "Top up hotbar, ammo and locked slots from nearby chests.");
            SortKey = Config.Bind("Keys", "SortKey", new KeyboardShortcut(KeyCode.T), "With the inventory open: sorts the open chest, or your bag when no chest is open. With Shift: changes the sort order.");
            Order = Config.Bind("Sort", "Order", SortOrder.Type, "How Sort orders items: by Type (weapons, ammo, armor, tools, food, materials, trophies), Name A-Z / Z-A, Quality, Weight, Amount or Value.");
            AutoSortOpen = Config.Bind("Sort", "AutoSortOnOpen", false, "Sort your bag when you open the inventory, and a chest when you open it.");
            AutoSortPickup = Config.Bind("Sort", "AutoSortOnPickup", false, "Sort your bag after you pick something up.");
            GatherFromBag = Config.Bind("Storage", "GatherFromBag", false, "Gather also takes from your bag (never the hotbar, locked, favorite or worn items). Also a toggle in the window.");
            StorageHeight = Config.Bind("Display", "WindowHeight", 0f, "Height of the Storage window. 0 = from the top of your bag down to near the bottom of the screen.");
            ScrollSpeed = Config.Bind("Display", "ScrollSpeed", 3000f, "How far the mouse wheel scrolls the Storage window.");
            StorageWidth = Config.Bind("Display", "WindowWidth", 0f, "Width of the Storage window. 0 = from the button column to near the right edge of the screen.");
            ButtonsOffset = Config.Bind("Display", "ButtonsOffset", new Vector2(8f, 0f), "Where the button column sits, from the top-right corner of your inventory.");
            new Harmony("felih.Hoardkeeper").PatchAll(typeof(Plugin).Assembly);
        }

        private void Update()
        {
            Player p = Player.m_localPlayer;
            if (p == null || p.IsDead()) return;
            AutoSort.Tick(p);
            if (Typing()) return;
            bool inv = InventoryGui.IsVisible();
            if (StorageKey.Value.IsDown()) Storage.Toggle();
            else if (StackKey.Value.IsDown()) Actions.QuickStack(p);
            else if (RestockKey.Value.IsDown()) Actions.Restock(p);
            else if (SortKey.Value.IsDown() && inv) Actions.SortHere(p);
            else if (inv && Input.GetKeyDown(SortKey.Value.MainKey) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) Actions.NextOrder();
        }

        internal static bool Typing()
        {
            return (Chat.instance != null && Chat.instance.HasFocus()) || Console.IsVisible() || TextInput.IsVisible() || Menu.IsVisible() || Storage.Typing();
        }

        internal static void Say(string msg)
        {
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, msg);
        }
    }
}
