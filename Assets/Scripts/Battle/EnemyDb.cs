using System.Collections.Generic;

namespace CatFight.Battle
{
    /// <summary>全部卡牌定义。数值集中在 Build() 里，方便调整与后续接入数据外置。</summary>
    public static class CardDb
    {
        private static Dictionary<string, CardData> _cards;

        public static CardData Get(string id)
        {
            EnsureBuilt();
            return _cards.TryGetValue(id, out var c) ? c : null;
        }

        public static IEnumerable<CardData> All
        {
            get { EnsureBuilt(); return _cards.Values; }
        }

        private static void EnsureBuilt()
        {
            if (_cards != null) return;
            _cards = new Dictionary<string, CardData>();

            // --- 起始卡（角色：耄耋 / 圆头猫爹）---
            // 卡名取自猫的行为：哈气、飞机耳（耳背到头后→圆头）、重击、舔毛。
            // id 保持旧值（strike/defend/bash），旧存档不会因为改名而读不到卡。
            Add(new CardData
            {
                id = "strike", name = "哈气", type = CardType.Attack, rarity = CardRarity.Basic,
                cost = 1, target = TargetMode.SingleEnemy,
                effects = { new CardEffect { op = EffectOp.Damage, value = 6 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 9 } },
                flavour = "喉咙里滚出一声低鸣，耳朵向后咎。"
            });

            Add(new CardData
            {
                id = "defend", name = "飞机耳", type = CardType.Skill, rarity = CardRarity.Basic,
                cost = 1, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.Block, value = 5 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Block, value = 8 } },
                flavour = "耳朵背到头后，整颗头变成一个圆。"
            });

            Add(new CardData
            {
                id = "bash", name = "重击", type = CardType.Attack, rarity = CardRarity.Basic,
                cost = 2, target = TargetMode.SingleEnemy,
                effects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 8 },
                    new CardEffect { op = EffectOp.ApplyTarget, value = 2, status = StatusType.Vulnerable }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 10 },
                    new CardEffect { op = EffectOp.ApplyTarget, value = 3, status = StatusType.Vulnerable }
                },
                flavour = "从博古架上跳下来的那一下。"
            });

            Add(new CardData
            {
                id = "heal", name = "舔毛", type = CardType.Skill, rarity = CardRarity.Basic,
                cost = 1, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.Heal, value = 5 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Heal, value = 8 } },
                flavour = "伤口舔一舔就会好。"
            });

            // --- 可获得的卡 ---
            Add(new CardData
            {
                id = "cleave", name = "顺劈斩", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.AllEnemies,
                effects = { new CardEffect { op = EffectOp.DamageAll, value = 8 } },
                upgradedEffects = { new CardEffect { op = EffectOp.DamageAll, value = 11 } }
            });

            Add(new CardData
            {
                id = "iron_wave", name = "铁斩波", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                effects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 5 },
                    new CardEffect { op = EffectOp.Block, value = 5 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 7 },
                    new CardEffect { op = EffectOp.Block, value = 7 }
                }
            });

            Add(new CardData
            {
                id = "shrug_it_off", name = "耸肩无视", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.Self,
                effects =
                {
                    new CardEffect { op = EffectOp.Block, value = 8 },
                    new CardEffect { op = EffectOp.Draw, value = 1 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Block, value = 11 },
                    new CardEffect { op = EffectOp.Draw, value = 1 }
                }
            });

            Add(new CardData
            {
                id = "inflame", name = "炎爆", type = CardType.Power, rarity = CardRarity.Uncommon,
                cost = 1, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Strength } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Strength } }
            });

            Add(new CardData
            {
                id = "twin_strike", name = "双重打击", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                effects = { new CardEffect { op = EffectOp.Damage, value = 5, times = 2 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 7, times = 2 } }
            });

            Add(new CardData
            {
                id = "deadly_poison", name = "致命毒药", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                effects = { new CardEffect { op = EffectOp.ApplyTarget, value = 5, status = StatusType.Poison } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplyTarget, value = 7, status = StatusType.Poison } }
            });

            Add(new CardData
            {
                id = "battle_trance", name = "战斗恍惚", type = CardType.Skill, rarity = CardRarity.Uncommon,
                cost = 0, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.Draw, value = 3 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Draw, value = 4 } }
            });

            Add(new CardData
            {
                id = "metallicize", name = "金属化", type = CardType.Power, rarity = CardRarity.Uncommon,
                cost = 1, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Thorns } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 4, status = StatusType.Thorns } }
            });

            // --- 耄耋专属（取材于猫的行为，不涉及虐待类梗）---

            Add(new CardData
            {
                id = "shelf_ambush", name = "博古架伏击", type = CardType.Attack, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.SingleEnemy,
                effects = { new CardEffect { op = EffectOp.Damage, value = 12 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 16 } },
                flavour = "跳到博古架上就不下来，僵持到凌晨四点。"
            });

            Add(new CardData
            {
                id = "silent_stare", name = "沉默盯视", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                effects = { new CardEffect { op = EffectOp.ApplyTarget, value = 2, status = StatusType.Weak } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplyTarget, value = 3, status = StatusType.Weak } },
                flavour = "它从来不叫，只是从眼缝里看着你。"
            });

            Add(new CardData
            {
                id = "round_head", name = "圆头", type = CardType.Power, rarity = CardRarity.Uncommon,
                cost = 1, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Dexterity } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 4, status = StatusType.Dexterity } },
                flavour = "耳朵一背，头就圆了。"
            });

            Add(new CardData
            {
                id = "hiss_barrage", name = "哈气连击", type = CardType.Attack, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.SingleEnemy,
                effects = { new CardEffect { op = EffectOp.Damage, value = 4, times = 3 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 5, times = 3 } },
                flavour = "一声接一声，不给你回嘴的机会。"
            });

            Add(new CardData
            {
                id = "wall_run", name = "飞檐走壁", type = CardType.Skill, rarity = CardRarity.Uncommon,
                cost = 1, target = TargetMode.Self,
                effects =
                {
                    new CardEffect { op = EffectOp.Block, value = 7 },
                    new CardEffect { op = EffectOp.Draw, value = 1 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Block, value = 10 },
                    new CardEffect { op = EffectOp.Draw, value = 1 }
                },
                flavour = "从墙头到窗台，不过一下。"
            });

            Add(new CardData
            {
                id = "raid", name = "翻墙入户", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 0, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.Draw, value = 1 } },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Draw, value = 1 },
                    new CardEffect { op = EffectOp.GainEnergy, value = 1 }
                },
                flavour = "没人知道它是怎么进来的。"
            });

            Add(new CardData
            {
                id = "strongest_war_cat", name = "史上最强战猫", type = CardType.Power, rarity = CardRarity.Rare,
                cost = 2, target = TargetMode.Self,
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Strength } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 4, status = StatusType.Strength } },
                flavour = "附近没有一只流浪猫愿意靠近它。"
            });

            Add(new CardData
            {
                id = "cat_dad_roar", name = "猫爹一喝", type = CardType.Attack, rarity = CardRarity.Rare,
                cost = 2, target = TargetMode.AllEnemies,
                effects =
                {
                    new CardEffect { op = EffectOp.DamageAll, value = 9 },
                    new CardEffect { op = EffectOp.ApplyAll, value = 1, status = StatusType.Weak }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.DamageAll, value = 12 },
                    new CardEffect { op = EffectOp.ApplyAll, value = 2, status = StatusType.Weak }
                },
                flavour = "一屋子猫，一瞬间都安静了。"
            });
        }

        private static void Add(CardData c) => _cards[c.id] = c;

        /// <summary>战斗胜利后的三选一奖励池。</summary>
        public static List<string> RewardPool()
            => new List<string>
            {
                // 通用
                "cleave", "iron_wave", "shrug_it_off", "inflame", "twin_strike",
                "deadly_poison", "battle_trance", "metallicize",
                // 耄耋专属
                "silent_stare", "raid", "wall_run", "round_head",
                "shelf_ambush", "hiss_barrage", "strongest_war_cat", "cat_dad_roar"
            };

        /// <summary>随机抽 3 张不重复的奖励卡。</summary>
        public static List<CardData> RollRewards(System.Random rng, int count = 3)
        {
            var pool = RewardPool();
            var result = new List<CardData>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                result.Add(Get(pool[k]));
                pool.RemoveAt(k);
            }
            return result;
        }
    }

    // ----------------------------------------------------------------------

    public enum IntentType { Attack, AttackMulti, Defend, Buff, Debuff, Unknown, Sleep }

    public class EnemyIntentDef
    {
        public IntentType type = IntentType.Attack;
        public int value = 6;
        public int times = 1;
        public StatusType status;
        public int statusValue;
    }

    public class EnemyDef
    {
        public string id;
        public string name;
        public int maxHp = 30;
        public bool isElite;
        public bool isBoss;
        public List<EnemyIntentDef> pattern = new List<EnemyIntentDef>();
        public bool loopPattern = true;
    }

    public static class EnemyDb
    {
        private static Dictionary<string, EnemyDef> _defs;

        public static EnemyDef Get(string id)
        {
            EnsureBuilt();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }

        private static void EnsureBuilt()
        {
            if (_defs != null) return;
            _defs = new Dictionary<string, EnemyDef>();

            Add(new EnemyDef
            {
                id = "slime_small", name = "小史莱姆", maxHp = 26,
                pattern =
                {
                    new EnemyIntentDef { type = IntentType.Attack, value = 6 },
                    new EnemyIntentDef { type = IntentType.Attack, value = 6 },
                    new EnemyIntentDef { type = IntentType.Debuff, value = 1, status = StatusType.Weak, statusValue = 1 },
                }
            });

            Add(new EnemyDef
            {
                id = "bat", name = "蝙蝠", maxHp = 22,
                pattern =
                {
                    new EnemyIntentDef { type = IntentType.Attack, value = 4 },
                    new EnemyIntentDef { type = IntentType.Attack, value = 4 },
                    new EnemyIntentDef { type = IntentType.Buff, value = 2, status = StatusType.Strength, statusValue = 2 },
                }
            });

            Add(new EnemyDef
            {
                id = "spider", name = "毒蛛", maxHp = 30,
                pattern =
                {
                    new EnemyIntentDef { type = IntentType.Debuff, value = 1, status = StatusType.Poison, statusValue = 3 },
                    new EnemyIntentDef { type = IntentType.Attack, value = 7 },
                    new EnemyIntentDef { type = IntentType.Defend, value = 7 },
                }
            });

            Add(new EnemyDef
            {
                id = "jaw_worm", name = "颚虫", maxHp = 62, isElite = true,
                pattern =
                {
                    new EnemyIntentDef { type = IntentType.Attack, value = 11 },
                    new EnemyIntentDef { type = IntentType.Defend, value = 8 },
                    new EnemyIntentDef { type = IntentType.Attack, value = 7, times = 2 },
                    new EnemyIntentDef { type = IntentType.Buff, value = 3, status = StatusType.Strength, statusValue = 3 },
                }
            });

            Add(new EnemyDef
            {
                id = "guardian", name = "守卫者", maxHp = 118, isBoss = true,
                pattern =
                {
                    new EnemyIntentDef { type = IntentType.Attack, value = 14 },
                    new EnemyIntentDef { type = IntentType.Defend, value = 14 },
                    new EnemyIntentDef { type = IntentType.Attack, value = 8, times = 3 },
                    new EnemyIntentDef { type = IntentType.Buff, value = 4, status = StatusType.Strength, statusValue = 4 },
                }
            });
        }

        private static void Add(EnemyDef d) => _defs[d.id] = d;

        /// <summary>按遭遇 id 生成敌人队列。</summary>
        public static List<EnemyDef> BuildEncounter(string encounterId, int streak)
        {
            var list = new List<EnemyDef>();
            switch (encounterId)
            {
                case "elite_jaw_worm":
                    list.Add(Get("jaw_worm"));
                    break;

                case "boss_guardian":
                    list.Add(Get("guardian"));
                    break;

                default:
                    // 普通遭遇：随步数从固定组合里挑
                    var rng = new System.Random(streak * 7919);
                    var combos = new[]
                    {
                        new[] { "slime_small", "bat" },
                        new[] { "spider", "slime_small" },
                        new[] { "bat", "bat", "slime_small" },
                    };
                    var pick = combos[rng.Next(combos.Length)];
                    foreach (var id in pick) list.Add(Get(id));
                    break;
            }
            return list;
        }
    }
}
