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
            // 卡名取自猫的行为：哈气、圆头（耳背到头后整颗头变圆）、强力一哈、嚼口香糖。
            // id 保持旧值（strike/defend/bash），旧存档不会因为改名而读不到卡。
            Add(new CardData
            {
                id = "strike", name = "哈气", type = CardType.Attack, rarity = CardRarity.Basic,
                cost = 1, target = TargetMode.SingleEnemy,
                art = "strike",
                effects = { new CardEffect { op = EffectOp.Damage, value = 6 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 9 } },
                flavour = "喉咙里滚出一声低鸣，耳朵向后咎。"
            });

            Add(new CardData
            {
                id = "defend", name = "圆头", type = CardType.Skill, rarity = CardRarity.Basic,
                cost = 1, target = TargetMode.Self,
                art = "defend",
                effects = { new CardEffect { op = EffectOp.Block, value = 5 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Block, value = 8 } },
                flavour = "耳朵背到头后，整颗头变成一个圆。"
            });

            Add(new CardData
            {
                id = "bash", name = "强力一哈", type = CardType.Attack, rarity = CardRarity.Basic,
                cost = 2, target = TargetMode.SingleEnemy,
                art = "bash",
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
                flavour = "重重地哈一下，对面直接没声了。"
            });

            Add(new CardData
            {
                id = "heal", name = "嚼口香糖", type = CardType.Skill, rarity = CardRarity.Basic,
                cost = 1, target = TargetMode.Self,
                art = "heal",
                effects = { new CardEffect { op = EffectOp.Heal, value = 5 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Heal, value = 8 } },
                flavour = "嘴一直在动，不知道在嚼什么。"
            });

            // --- 可获得的卡 ---
            Add(new CardData
            {
                id = "cleave", name = "顺劈斩", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.AllEnemies,
                art = "strike",
                effects = { new CardEffect { op = EffectOp.DamageAll, value = 8 } },
                upgradedEffects = { new CardEffect { op = EffectOp.DamageAll, value = 11 } }
            });

            Add(new CardData
            {
                id = "iron_wave", name = "铁斩波", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                art = "bash",
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
                art = "defend",
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
                art = "strike",
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Strength } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Strength } }
            });

            Add(new CardData
            {
                id = "twin_strike", name = "双重打击", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                art = "strike",
                effects = { new CardEffect { op = EffectOp.Damage, value = 5, times = 2 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 7, times = 2 } }
            });

            Add(new CardData
            {
                id = "deadly_poison", name = "致命毒药", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                art = "heal",
                effects = { new CardEffect { op = EffectOp.ApplyTarget, value = 5, status = StatusType.Poison } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplyTarget, value = 7, status = StatusType.Poison } }
            });

            Add(new CardData
            {
                id = "battle_trance", name = "战斗恍惚", type = CardType.Skill, rarity = CardRarity.Uncommon,
                cost = 0, target = TargetMode.Self,
                art = "defend",
                effects = { new CardEffect { op = EffectOp.Draw, value = 3 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Draw, value = 4 } }
            });

            Add(new CardData
            {
                id = "metallicize", name = "金属化", type = CardType.Power, rarity = CardRarity.Uncommon,
                cost = 1, target = TargetMode.Self,
                art = "bash",
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Thorns } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 4, status = StatusType.Thorns } }
            });

            // --- 耄耋专属（取材于猫的行为，不涉及虐待类梗）---

            Add(new CardData
            {
                id = "shelf_ambush", name = "博古架伏击", type = CardType.Attack, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.SingleEnemy,
                art = "bash",
                effects = { new CardEffect { op = EffectOp.Damage, value = 12 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 16 } },
                flavour = "跳到博古架上就不下来，僵持到凌晨四点。"
            });

            Add(new CardData
            {
                id = "silent_stare", name = "沉默盯视", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                art = "bash",
                effects = { new CardEffect { op = EffectOp.ApplyTarget, value = 2, status = StatusType.Weak } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplyTarget, value = 3, status = StatusType.Weak } },
                flavour = "它从来不叫，只是从眼缝里看着你。"
            });

            Add(new CardData
            {
                id = "round_head", name = "足球形态", type = CardType.Power, rarity = CardRarity.Uncommon,
                cost = 1, target = TargetMode.Self,
                art = "defend",
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Dexterity } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 4, status = StatusType.Dexterity } },
                // id 保留 round_head（改过两次名了）：id 是存档用的，永久固定，不要再改
                flavour = "缩成一团，圆得能拿去踢。"
            });

            Add(new CardData
            {
                id = "hiss_barrage", name = "哈气连击", type = CardType.Attack, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.SingleEnemy,
                art = "strike",
                effects = { new CardEffect { op = EffectOp.Damage, value = 4, times = 3 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Damage, value = 5, times = 3 } },
                flavour = "一声接一声，不给你回嘴的机会。"
            });

            Add(new CardData
            {
                id = "wall_run", name = "飞檐走壁", type = CardType.Skill, rarity = CardRarity.Uncommon,
                cost = 1, target = TargetMode.Self,
                art = "defend",
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
                art = "defend",
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
                art = "bash",
                effects = { new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Strength } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplySelf, value = 4, status = StatusType.Strength } },
                flavour = "附近没有一只流浪猫愿意靠近它。"
            });

            Add(new CardData
            {
                id = "cat_dad_roar", name = "猫爹一喝", type = CardType.Attack, rarity = CardRarity.Rare,
                cost = 2, target = TargetMode.AllEnemies,
                art = "strike",
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

            Add(new CardData
            {
                id = "knock_over", name = "打翻水杯", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.AllEnemies,
                art = "strike",
                effects = { new CardEffect { op = EffectOp.DamageAll, value = 6 } },
                upgradedEffects = { new CardEffect { op = EffectOp.DamageAll, value = 9 } },
                flavour = "桌子上的杯子，永远活不过今晚。"
            });

            Add(new CardData
            {
                id = "play_dead", name = "装死", type = CardType.Skill, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.Self, exhaust = true,
                art = "defend",
                effects = { new CardEffect { op = EffectOp.Block, value = 15 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Block, value = 20 } },
                flavour = "一动不动，连呼吸都省了。"
            });

            // ------------------------------------------------------------------
            // 热梗牌（哈基米 / 老吴 / 曼波 / 脊背龙 / 赛博猫咖 等）
            // 命名取自猫的行为与叫声，效果尽量让名字和机制对得上。
            // ------------------------------------------------------------------

            Add(new CardData
            {
                id = "laowu", name = "老吴", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 0, target = TargetMode.Self,
                art = "strike",
                effects = { new CardEffect { op = EffectOp.ApplyAll, value = 1, status = StatusType.Weak } },
                upgradedEffects = { new CardEffect { op = EffectOp.ApplyAll, value = 2, status = StatusType.Weak } },
                flavour = "半夜那一声凄厉悠长的长嚎。"
            });

            Add(new CardData
            {
                id = "plane_ear", name = "飞机耳", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 0, target = TargetMode.Self,
                art = "defend",
                effects = { new CardEffect { op = EffectOp.Block, value = 4 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Block, value = 6 } },
                flavour = "耳朵一收，先躲为敬。"
            });

            Add(new CardData
            {
                id = "neige", name = "内个内个", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 0, target = TargetMode.Self,
                art = "defend",
                effects = { new CardEffect { op = EffectOp.Draw, value = 1 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Draw, value = 2 } },
                flavour = "嘴里念念有词，也不知道在说什么。"
            });

            Add(new CardData
            {
                id = "manbo", name = "曼波", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 0, target = TargetMode.Self,
                art = "defend",
                effects =
                {
                    new CardEffect { op = EffectOp.Draw, value = 2 },
                    new CardEffect { op = EffectOp.LoseHp, value = 3 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Draw, value = 2 },
                    new CardEffect { op = EffectOp.LoseHp, value = 1 }
                },
                flavour = "傻了吧唧，但怀里的牌是真的多。"
            });

            Add(new CardData
            {
                id = "good_cat", name = "豪猫", type = CardType.Skill, rarity = CardRarity.Common,
                cost = 0, target = TargetMode.Self,
                art = "heal",
                effects = { new CardEffect { op = EffectOp.Heal, value = 4 } },
                upgradedEffects = { new CardEffect { op = EffectOp.Heal, value = 7 } },
                flavour = "乖得让人想多喂两口。"
            });

            Add(new CardData
            {
                id = "bad_cat", name = "剑猫", type = CardType.Attack, rarity = CardRarity.Common,
                cost = 1, target = TargetMode.SingleEnemy,
                art = "bash",
                effects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 9 },
                    new CardEffect { op = EffectOp.LoseHp, value = 2 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 12 },
                    new CardEffect { op = EffectOp.LoseHp, value = 2 }
                },
                flavour = "见谁挠谁，连自己也不放过。"
            });

            Add(new CardData
            {
                id = "spinosaurus", name = "脊背龙形态", type = CardType.Power, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.Self,
                art = "strike",
                effects =
                {
                    new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Strength },
                    new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Thorns }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.ApplySelf, value = 3, status = StatusType.Strength },
                    new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Thorns }
                },
                flavour = "弓背炸毛，背脊一侧立起来。"
            });

            Add(new CardData
            {
                id = "cyber_cat_cafe", name = "赛博猫咖", type = CardType.Attack, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.AllEnemies,
                art = "strike",
                // DamageAll 不支持 times，用两条等效效果模拟「打两次」
                effects =
                {
                    new CardEffect { op = EffectOp.DamageAll, value = 4 },
                    new CardEffect { op = EffectOp.DamageAll, value = 4 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.DamageAll, value = 5 },
                    new CardEffect { op = EffectOp.DamageAll, value = 5 }
                },
                flavour = "几百只哈基米连麦麟麟，已经没人了。"
            });

            Add(new CardData
            {
                id = "hiss_night", name = "哈气之夜", type = CardType.Attack, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.AllEnemies,
                art = "strike",
                effects = { new CardEffect { op = EffectOp.DamageAll, value = 7 } },
                upgradedEffects = { new CardEffect { op = EffectOp.DamageAll, value = 10 } },
                flavour = "整栋楼的猫同时开始哈气。"
            });

            Add(new CardData
            {
                id = "cat_universe", name = "猫猫宇宙", type = CardType.Skill, rarity = CardRarity.Uncommon,
                cost = 2, target = TargetMode.Self,
                art = "defend",
                effects =
                {
                    new CardEffect { op = EffectOp.ApplyAll, value = 1, status = StatusType.Vulnerable },
                    new CardEffect { op = EffectOp.Draw, value = 2 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.ApplyAll, value = 2, status = StatusType.Vulnerable },
                    new CardEffect { op = EffectOp.Draw, value = 2 }
                },
                flavour = "你又来看猫猫了呀。"
            });

            Add(new CardData
            {
                id = "hachimi_song", name = "哈基米之歌", type = CardType.Power, rarity = CardRarity.Rare,
                cost = 1, target = TargetMode.Self,
                art = "heal",
                effects =
                {
                    new CardEffect { op = EffectOp.ApplySelf, value = 1, status = StatusType.Strength },
                    new CardEffect { op = EffectOp.ApplySelf, value = 1, status = StatusType.Dexterity }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Strength },
                    new CardEffect { op = EffectOp.ApplySelf, value = 2, status = StatusType.Dexterity }
                },
                flavour = "只要舔了蜂蜜，脚步就会变轻快。"
            });

            Add(new CardData
            {
                id = "trex", name = "改装霸王龙", type = CardType.Attack, rarity = CardRarity.Rare,
                cost = 3, target = TargetMode.SingleEnemy,
                art = "bash",
                effects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 22 },
                    new CardEffect { op = EffectOp.LoseHp, value = 3 }
                },
                upgradedEffects =
                {
                    new CardEffect { op = EffectOp.Damage, value = 28 },
                    new CardEffect { op = EffectOp.LoseHp, value = 3 }
                },
                flavour = "背上装了点东西，从此不再是一只普通的猫。"
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
                "shelf_ambush", "hiss_barrage", "strongest_war_cat", "cat_dad_roar",
                "knock_over", "play_dead",
                // 热梗（哈基米 / 老吴 / 曼波 / 脊背龙 / 赛博猫咖 等）
                "laowu", "plane_ear", "neige", "manbo", "good_cat", "bad_cat",
                "spinosaurus", "cyber_cat_cafe", "hiss_night", "cat_universe",
                "hachimi_song", "trex"
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
