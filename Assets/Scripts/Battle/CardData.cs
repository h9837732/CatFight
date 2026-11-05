using System;
using System.Collections.Generic;

namespace CatFight.Battle
{
    public enum StatusType
    {
        Strength,       // 力量：每层 +1 基础伤害
        Dexterity,      // 敏捷：每层 +1 格挡
        Vulnerable,     // 易伤：受到伤害 +50%
        Weak,           // 虚弱：造成伤害 -25%
        Frail,          // 脆弱：获得格挡 -25%
        Poison,         // 中毒：回合开始失去等量生命（无视格挡）
        Regen,          // 再生：回合结束回复等量生命
        Thorns          // 荆棘：受击后反弹等量伤害
    }

    public enum CardType { Attack, Skill, Power, Status, Curse }
    public enum CardRarity { Basic, Common, Uncommon, Rare }
    public enum TargetMode { SingleEnemy, AllEnemies, Self, NoTarget }

    public enum EffectOp
    {
        Damage,          // 单体伤害
        DamageAll,       // 群体伤害
        Block,           // 获得格挡
        Draw,            // 抽牌
        ApplySelf,       // 给自己上状态
        ApplyTarget,     // 给目标上状态
        ApplyAll,        // 给所有敌人上状态
        GainEnergy,      // 获得能量
        Heal,            // 回复生命
        LoseHp           // 失去生命（无视格挡）
    }

    [Serializable]
    public class CardEffect
    {
        public EffectOp op;
        public int value;
        public int times = 1;
        public StatusType status;
    }

    [Serializable]
    public class CardData
    {
        public string id;
        public string name;
        public CardType type = CardType.Attack;
        public CardRarity rarity = CardRarity.Basic;
        public int cost = 1;
        public TargetMode target = TargetMode.SingleEnemy;
        public bool exhaust;                 // 打出后消耗
        /// <summary>
        /// 卡面底图（不含前缀）。4 张猫脸里选一张：
        /// strike = 张嘴哈气 / defend = 圆头呆脸 / bash = 压低眯眼 / heal = 绿光环绕。
        /// 留空则按类型退回。选图看语义：凶一点的给 bash，可爱的给 defend，治疗/毒给 heal。
        /// </summary>
        public string art;
        public List<CardEffect> effects = new List<CardEffect>();
        public List<CardEffect> upgradedEffects = new List<CardEffect>();
        public string flavour = "";

        public List<CardEffect> GetEffects(bool upgraded)
        {
            if (upgraded && upgradedEffects != null && upgradedEffects.Count > 0) return upgradedEffects;
            return effects ?? new List<CardEffect>();
        }

        public int GetCost(bool upgraded)
        {
            // 目前只有"升级降费"这一种情况，通过 upgradedEffects 里不表达，
            // 需要时在各卡定义里显式给出
            return cost;
        }

        /// <summary>根据效果列表生成卡面描述（保证文案与实际数值一致，避免手写描述不同步）。</summary>
        public string Describe(bool upgraded)
        {
            var lines = new List<string>();
            var list = GetEffects(upgraded);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                switch (e.op)
                {
                    case EffectOp.Damage:
                        lines.Add(e.times > 1 ? $"造成 {e.value} 点伤害 {e.times} 次" : $"造成 {e.value} 点伤害");
                        break;
                    case EffectOp.DamageAll:
                        lines.Add($"对所有敌人造成 {e.value} 点伤害");
                        break;
                    case EffectOp.Block:
                        lines.Add($"获得 {e.value} 点格挡");
                        break;
                    case EffectOp.Draw:
                        lines.Add($"抽 {e.value} 张牌");
                        break;
                    case EffectOp.ApplySelf:
                        lines.Add($"自身获得 {e.value} 层{StatusName(e.status)}");
                        break;
                    case EffectOp.ApplyTarget:
                        lines.Add($"施加 {e.value} 层{StatusName(e.status)}");
                        break;
                    case EffectOp.ApplyAll:
                        lines.Add($"对所有敌人施加 {e.value} 层{StatusName(e.status)}");
                        break;
                    case EffectOp.GainEnergy:
                        lines.Add($"获得 {e.value} 点能量");
                        break;
                    case EffectOp.Heal:
                        lines.Add($"回复 {e.value} 点生命");
                        break;
                    case EffectOp.LoseHp:
                        lines.Add($"失去 {e.value} 点生命");
                        break;
                }
            }
            return string.Join("\n", lines);
        }

        public static string StatusName(StatusType s)
        {
            switch (s)
            {
                case StatusType.Strength: return "力量";
                case StatusType.Dexterity: return "敏捷";
                case StatusType.Vulnerable: return "易伤";
                case StatusType.Weak: return "虚弱";
                case StatusType.Frail: return "脆弱";
                case StatusType.Poison: return "中毒";
                case StatusType.Regen: return "再生";
                case StatusType.Thorns: return "荆棘";
                default: return s.ToString();
            }
        }
    }

    /// <summary>战斗中的一张卡实例（带升级标记）。</summary>
    public class CardInstance
    {
        public CardData Data;
        public bool Upgraded;

        public CardInstance(CardData data, bool upgraded = false)
        {
            Data = data;
            Upgraded = upgraded;
        }

        public int Cost => Data.GetCost(Upgraded);
        public string Name => Data.name + (Upgraded ? "+" : "");
        public string Description => Data.Describe(Upgraded);
    }
}
