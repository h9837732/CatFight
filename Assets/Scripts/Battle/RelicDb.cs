using System;
using System.Collections.Generic;

namespace CatFight.Battle
{
    public enum RelicRarity { Starter, Common, Uncommon, Rare }

    public class RelicDef
    {
        public string id;
        public string name;
        public string desc;
        public RelicRarity rarity = RelicRarity.Common;
    }

    /// <summary>
    /// 遗物库。开局三选一，之后由战斗/宝箱授予。
    /// 每件遗物的实际效果在 BattleScreen.ApplyBattleStartRelics / RunHooks 中实现，
    /// 这里只放展示与判定所需的数据。
    /// </summary>
    public static class RelicDb
    {
        private static Dictionary<string, RelicDef> _defs;

        public static RelicDef Get(string id)
        {
            EnsureBuilt();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }

        public static IEnumerable<RelicDef> All
        {
            get { EnsureBuilt(); return _defs.Values; }
        }

        private static void EnsureBuilt()
        {
            if (_defs != null) return;
            _defs = new Dictionary<string, RelicDef>();

            Add(new RelicDef
            {
                id = "relic_burning_blood", name = "燃烧之血", rarity = RelicRarity.Starter,
                desc = "战斗胜利后，回复 6 点生命。"
            });

            Add(new RelicDef
            {
                id = "relic_vajra", name = "金刚杵", rarity = RelicRarity.Common,
                desc = "战斗开始时，获得 1 点力量。"
            });

            Add(new RelicDef
            {
                id = "relic_anchor", name = "船锚", rarity = RelicRarity.Common,
                desc = "战斗开始时，获得 10 点格挡。"
            });

            Add(new RelicDef
            {
                id = "relic_iron_plate", name = "铁板", rarity = RelicRarity.Common,
                desc = "战斗开始时，获得 4 点格挡。"
            });

            Add(new RelicDef
            {
                id = "relic_blood_vial", name = "血瓶", rarity = RelicRarity.Common,
                desc = "战斗开始时，回复 2 点生命。"
            });

            Add(new RelicDef
            {
                id = "relic_lantern", name = "灯笼", rarity = RelicRarity.Uncommon,
                desc = "每场战斗的第一回合，额外获得 1 点行动点。"
            });

            Add(new RelicDef
            {
                id = "relic_purse", name = "钱袋", rarity = RelicRarity.Common,
                desc = "获得时立即得到 100 金币。"
            });

            Add(new RelicDef
            {
                id = "relic_pocket_watch", name = "怀表", rarity = RelicRarity.Uncommon,
                desc = "战斗开始时，获得 1 点敏捷。"
            });

            Add(new RelicDef
            {
                id = "relic_placeholder_0", name = "占位护符", rarity = RelicRarity.Common,
                desc = "一件来历不明的护符（占位）。"
            });
        }

        private static void Add(RelicDef d) => _defs[d.id] = d;

        /// <summary>开局三选一：随机不重复抽取。</summary>
        public static List<RelicDef> RollChoices(int seed, int count = 3)
        {
            EnsureBuilt();
            var pool = new List<string>
            {
                "relic_burning_blood", "relic_vajra", "relic_anchor", "relic_iron_plate",
                "relic_blood_vial", "relic_lantern", "relic_purse", "relic_pocket_watch"
            };

            var rng = new Random(seed * 7919 + 13);
            var result = new List<RelicDef>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                result.Add(Get(pool[k]));
                pool.RemoveAt(k);
            }
            return result;
        }

        public static string NameOf(string id)
        {
            var d = Get(id);
            return d != null ? d.name : id;
        }

        public static string DescOf(string id)
        {
            var d = Get(id);
            return d != null ? d.desc : "";
        }
    }
}
