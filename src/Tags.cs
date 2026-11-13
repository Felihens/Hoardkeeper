using System.Collections.Generic;
using UnityEngine;

namespace Hoardkeeper
{
    // Chest tags: give a chest one or more categories (Food, Ores, Wood...) in the Storage window, then
    // "Fill tags" sends every item of those categories there, from your bag and from chests not tagged for it.
    // Tags live on the chest (ZDO), so everyone with the mod sees them.
    internal static class Tags
    {
        internal static readonly string[] All =
        {
            "Weapons", "Ammo", "Armor", "Tools", "Food", "Meads", "Meat", "Fish",
            "Crops", "Seeds", "Ores", "Metals", "Wood", "Stone", "Hides", "Creature",
            "Valuables", "Trophies", "Materials", "Misc"
        };

        private const string Key = "Hoardkeeper_tags";

        private static bool Has(string name, params string[] parts)
        {
            foreach (string s in parts) if (name.Contains(s)) return true;
            return false;
        }

        // which category an item belongs to (by type, then by its internal name for materials)
        internal static string Category(ItemDrop.ItemData it)
        {
            ItemDrop.ItemData.SharedData d = it.m_shared;
            string n = (d.m_name ?? "").ToLowerInvariant();
            string id = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
            // by the item's prefab name first: things the rules below would get wrong
            if (id.StartsWith("Beastkeeper_")) return "Tools";
            if (id.StartsWith("Catapult") || id.StartsWith("BombSiege")) return "Ammo";
            if (id.StartsWith("MeadBase") || id == "BarleyWineBase") return "Meads";
            if ((id.StartsWith("Feast") && id.EndsWith("_Material")) || id.StartsWith("Spice") || id.EndsWith("Flour") || id == "BreadDough") return "Food";
            if (id.EndsWith("Uncooked") && !Gear(id)) return "Food";
            if (d.m_itemType == ItemDrop.ItemData.ItemType.Material && (id.StartsWith("Mold") || id.StartsWith("Orb") || id.StartsWith("Staff") || id.EndsWith("Uncooked"))) return "Materials";
            switch (d.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir: return "Weapons";
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable: return "Ammo";
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket: return "Armor";
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch: return "Tools";
                case ItemDrop.ItemData.ItemType.Trophy: return "Trophies";
                case ItemDrop.ItemData.ItemType.Fish: return "Fish";
                case ItemDrop.ItemData.ItemType.Consumable:
                    if (d.m_food > 0f || d.m_foodStamina > 0f || d.m_foodEitr > 0f) return "Food";
                    return "Meads";
                case ItemDrop.ItemData.ItemType.Material:
                    // order matters: the first match wins (e.g. leather scraps are Hides, not Ores; chitin isn't a metal)
                    if (Has(n, "coin", "amber", "ruby", "pearl", "necklace", "gemstone", "jewel")) return "Valuables";
                    if (Has(n, "seed", "sapling", "cone", "acorn")) return "Seeds";
                    if (n.StartsWith("$item_fish")) return "Fish";
                    if (Has(n, "hide", "pelt", "leather")) return "Hides";
                    if (Has(n, "meat", "entrails", "necktail", "neck_tail", "chicken")) return "Meat";
                    if (Has(n, "feather", "gland", "bone", "claw", "fang", "tooth", "horn", "antler", "carapace", "mandible",
                            "chitin", "tissue", "ooze", "guck", "bile", "ectoplasm", "scale", "blood", "wing", "hair", "core",
                            "eye", "needle", "skull", "tail", "bladder", "carrion", "paw", "sinew", "heart", "blubber", "ribcage",
                            "larva")) return "Creature";
                    if (Has(n, "barley", "flax", "turnip", "onion", "thistle", "dandelion", "fiddlehead", "mushroom", "berr",
                            "carrot", "smokepuff", "jotunpuff", "magecap", "vineberry", "seaweed")) return "Crops";
                    if (Has(n, "ore", "scrap")) return "Ores";
                    if (Has(n, "copper", "tin", "bronze", "iron", "silver", "blackmetal", "flametal", "ingot", "metal")) return "Metals";
                    if (Has(n, "wood", "roundlog", "elderbark", "log")) return "Wood";
                    if (Has(n, "stone", "marble", "obsidian", "flint", "grausten", "crystal")) return "Stone";
                    return "Materials";
                default: return "Misc";
            }
        }

        // the gold gear and staffs still to be finished at a forge ("...Uncooked") aren't food
        private static bool Gear(string id)
        {
            foreach (string g in new[] { "Armor", "Atgeir", "Axe", "Battleaxe", "Bow", "Crossbow", "Fist", "Keys", "Knife", "Mace",
                "Shield", "Sledge", "SmallParts", "Spear", "Staff", "Sword", "THSword", "Generic", "Mold" })
                if (id.StartsWith(g)) return true;
            return false;
        }

        internal static HashSet<string> Of(Container c)
        {
            HashSet<string> set = new HashSet<string>();
            ZNetView nv = Chests.View(c);
            if (nv == null || !nv.IsValid()) return set;
            foreach (string t in nv.GetZDO().GetString(Key, "").Split(',')) if (t.Length > 0) set.Add(t);
            return set;
        }

        internal static void Toggle(Container c, string tag)
        {
            ZNetView nv = Chests.View(c);
            if (nv == null || !nv.IsValid()) return;
            HashSet<string> set = Of(c);
            if (!set.Remove(tag)) set.Add(tag);
            List<string> ordered = new List<string>();
            foreach (string t in All) if (set.Contains(t)) ordered.Add(t);
            Chests.Claim(c);
            nv.GetZDO().Set(Key, string.Join(",", ordered.ToArray()));
        }

        internal static string Shown(Container c)
        {
            HashSet<string> set = Of(c);
            if (set.Count == 0) return "";
            List<string> ordered = new List<string>();
            foreach (string t in All) if (set.Contains(t)) ordered.Add(t);
            return string.Join(", ", ordered.ToArray());
        }

        // "Fill tags": every item goes to a chest tagged with its category (one that already holds it first)
        // only != null: just fill that one chest (the chest window's Fill tags), from the bag and the other chests
        internal static void Fill(Player p, List<Container> chests, Container only = null)
        {
            if (only != null && !chests.Contains(only)) chests.Add(only);
            Dictionary<string, List<Container>> byTag = new Dictionary<string, List<Container>>();
            Dictionary<Container, HashSet<string>> tagsOf = new Dictionary<Container, HashSet<string>>();
            foreach (Container c in chests)
            {
                HashSet<string> set = Of(c);
                tagsOf[c] = set;
                if (only != null && c != only) continue;
                foreach (string t in set)
                {
                    List<Container> l;
                    if (!byTag.TryGetValue(t, out l)) byTag[t] = l = new List<Container>();
                    l.Add(c);
                }
            }
            if (byTag.Count == 0 && only != null) { Plugin.Say("This chest has no tags yet: click Tags first"); return; }
            if (byTag.Count == 0) { Plugin.Say("No tagged chests here: use a chest's Tags button first"); return; }
            int moved = 0, full = 0;
            Inventory bag = p.GetInventory();
            foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(bag.GetAllItems()))
            {
                if (Locks.Keep(p, it)) continue;
                List<Container> to;
                if (!byTag.TryGetValue(Category(it), out to)) continue;
                int n = Put(bag, it, to);
                moved += n;
                if (bag.ContainsItem(it)) full++;
            }
            foreach (Container c in chests)
            {
                HashSet<string> mine = tagsOf[c];
                foreach (ItemDrop.ItemData it in new List<ItemDrop.ItemData>(c.GetInventory().GetAllItems()))
                {
                    string cat = Category(it);
                    List<Container> to;
                    if (mine.Contains(cat) || !byTag.TryGetValue(cat, out to)) continue;
                    Chests.Claim(c);
                    moved += Put(c.GetInventory(), it, to);
                    if (c.GetInventory().ContainsItem(it)) full++;
                }
            }
            Plugin.Say(moved > 0 ? "Sent " + moved + " items to their tagged chests" + (full > 0 ? " (some didn't fit)" : "") : (full > 0 ? "The tagged chests are full" : "Nothing to send"));
        }

        private static int Put(Inventory from, ItemDrop.ItemData it, List<Container> to)
        {
            int moved = 0;
            List<Container> order = new List<Container>();
            foreach (Container c in to) if (Chests.Count(c.GetInventory(), it.m_shared.m_name) > 0) order.Add(c);
            foreach (Container c in to) if (!order.Contains(c)) order.Add(c);
            foreach (Container c in order)
            {
                if (!from.ContainsItem(it)) break;
                Chests.Claim(c);
                moved += Chests.Move(from, c.GetInventory(), it, it.m_stack);
            }
            return moved;
        }
    }
}
