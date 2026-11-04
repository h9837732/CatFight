using System;
using System.Collections.Generic;

namespace CatFight.Battle
{
    /// <summary>
    /// 消耗型道具（药水）。与卡牌共用 EffectOp，但不需要费用、不进卡组，用掉就消失。
    /// 存档里只记 id，所以新增道具无需改存档结构。
    /// </summary>
    public class ItemDef
    {
        public string id;
        public string name;
        public string desc;
        public TargetMode target = TargetMode.Self;
        public List<CardEffect> effects = new List<CardEffect>();
        /// <summary>只能战斗中使用（目前全部为 true，留字段给后续非战斗道具）。</summary>
        public bool battleOnly = true;
    }

    public static class ItemDb
    {
        /// <summary>道具栏容量（纵向格数）。</summary>
        public const int MaxSlots = 3;

        private static Dictionary<string, ItemDef> _defs;

        public static ItemDef Get(string id)
        {
            EnsureBuilt();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }

        public static IEnumerable<ItemDef> All
        {
            get { EnsureBuilt(); return _defs.Values; }
        }

        private static void EnsureBuilt()
        {
            if (_defs != null) return;
            _defs = new Dictionary<string, ItemDef>();

            Add(new ItemDef
            {
                id = "item_heal_potion", name = "治疗药水",
                desc = "回复 20 点生命。",
                target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.Heal, value = 20 } }
            });

            Add(new ItemDef
            {
                id = "item_strength_potion", name = "力量药水",
                desc = "获得 2 层力量。",
                target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Strength } }
            });

            Add(new ItemDef
            {
                id = "item_fire_potion", name = "火焰药水",
                desc = "对一个敌人造成 20 点伤害。",
                target = TargetMode.SingleEnemy,
                effects = { new CardEffect { op = EffectOp.Damage, value = 20 } }
            });

            Add(new ItemDef
            {
                id = "item_block_potion", name = "铁壁药水",
                desc = "获得 12 点格挡。",
                target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.Block, value = 12 } }
            });

            Add(new ItemDef
            {
                id = "item_swift_potion", name = "迅捷药水",
                desc = "抽 3 张牌。",
                target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.Draw, value = 3 } }
            });

            Add(new ItemDef
            {
                id = "item_energy_potion", name = "活力药水",
                desc = "获得 2 点行动点。",
                target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.GainEnergy, value = 2 } }
            });
        }

        private static void Add(ItemDef d) => _defs[d.id] = d;

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

        /// <summary>奖励池里随机一件道具。</summary>
        public static string RollRandom(System.Random rng)
        {
            EnsureBuilt();
            var ids = new List<string>(_defs.Keys);
            return ids[rng.Next(ids.Count)];
        }
    }
}
