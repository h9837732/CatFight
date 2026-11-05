using System;
using System.Collections.Generic;

namespace CatFight.Battle
{
    /// <summary>战斗单位（玩家与敌人共用）。</summary>
    public class BattleUnit
    {
        public string name;
        public int hp;
        public int maxHp;
        public int block;

        public readonly Dictionary<StatusType, int> statuses = new Dictionary<StatusType, int>();

        public bool IsDead => hp <= 0;

        public BattleUnit(string name, int maxHp)
        {
            this.name = name;
            this.maxHp = maxHp;
            this.hp = maxHp;
        }

        public int GetStatus(StatusType s) => statuses.TryGetValue(s, out int v) ? v : 0;

        public void AddStatus(StatusType s, int amount)
        {
            if (amount == 0) return;
            int cur = GetStatus(s);
            int next = cur + amount;
            if (next <= 0) statuses.Remove(s);
            else statuses[s] = next;
        }

        public void SetStatus(StatusType s, int amount)
        {
            if (amount <= 0) statuses.Remove(s);
            else statuses[s] = amount;
        }

        /// <summary>回合结束时递减的层数型状态。</summary>
        public void TickDownDurations()
        {
            var keys = new List<StatusType>(statuses.Keys);
            foreach (var k in keys)
            {
                if (k == StatusType.Vulnerable || k == StatusType.Weak || k == StatusType.Frail)
                {
                    int v = statuses[k] - 1;
                    if (v <= 0) statuses.Remove(k);
                    else statuses[k] = v;
                }
            }
        }

        public void ResetBlock() => block = 0;
    }

    /// <summary>敌人单位：带意图。</summary>
    public class EnemyUnit : BattleUnit
    {
        public EnemyDef def;
        public int intentIndex;
        public EnemyIntentDef CurrentIntent =>
            (def != null && def.pattern != null && def.pattern.Count > 0)
                ? def.pattern[intentIndex % def.pattern.Count]
                : null;

        public EnemyUnit(EnemyDef d) : base(d.name, d.maxHp)
        {
            def = d;
            intentIndex = 0;
        }

        public void AdvanceIntent()
        {
            if (def == null || def.pattern == null || def.pattern.Count == 0) return;
            intentIndex = def.loopPattern
                ? (intentIndex + 1) % def.pattern.Count
                : Math.Min(intentIndex + 1, def.pattern.Count - 1);
        }

        /// <summary>意图显示文案（数值必须是扣除状态后的最终值，见设计文档 §7.3）。</summary>
        public string IntentText()
        {
            var it = CurrentIntent;
            if (it == null) return "？";

            switch (it.type)
            {
                case IntentType.Attack:
                    return $"攻击 {PreviewDamage(it.value)}";
                case IntentType.AttackMulti:
                    return $"攻击 {PreviewDamage(it.value)}×{it.times}";
                case IntentType.Defend:
                    return $"防御 {it.value}";
                case IntentType.Buff:
                    return "强化";
                case IntentType.Debuff:
                    return "削弱";
                case IntentType.Sleep:
                    return "睡眠";
                default:
                    return "？";
            }
        }

        /// <summary>用自身状态估算真实伤害（供 UI 显示）。</summary>
        private int PreviewDamage(int baseValue)
        {
            int dmg = baseValue + GetStatus(StatusType.Strength);
            if (GetStatus(StatusType.Weak) > 0) dmg = (int)Math.Floor(dmg * 0.75);
            return Math.Max(0, dmg);
        }
    }

    public enum BattlePhase
    {
        NotStarted,
        PlayerTurnStart,
        PlayerAction,
        CardResolving,
        TurnEnding,
        EnemyTurn,
        Victory,
        Defeat
    }

    /// <summary>
    /// 战斗规则引擎。纯逻辑、无 UI 依赖、可单元测试。
    /// 结算顺序严格按设计文档 §6.3。
    /// </summary>
    public class BattleState
    {
        public const int HandLimit = 10;
        /// <summary>
        /// 每回合抽牌数。从 5 降到 3：手牌不再自然堆到上限，
        /// 弃牌环节只在“没出牌 / 用了抽牌效果”时才触发，而不是每回合必做。
        /// </summary>
        public const int DrawPerTurn = 3;

        /// <summary>
        /// 回合结束的手牌处理策略。
        /// A 弃光 = 每回合固定 5 张（传统杀戮尖塔手感）；
        /// B 保留 = 手牌跨回合累积，最多到 HandLimit（本项目默认）。
        /// </summary>
        public enum HandPolicy
        {
            DiscardAll,
            KeepAll
        }

        /// <summary>默认保留，手牌才会真正累积到 10 张。</summary>
        public HandPolicy handPolicy = HandPolicy.KeepAll;

        /// <summary>
        /// 回合开始的抽牌方式（仅 KeepAll 有意义）：
        /// false（默认）= 固定抽 DrawPerTurn(3) 张，封顶 HandLimit(10)。
        /// true         = 补齐到 EndTurnHandLimit 张：保留 2 张就只抽 3 张。
        /// 抽牌数降到 3 后，默认模式已经不会把牌堆满，一般不需要打开这个开关。
        /// </summary>
        public bool drawFillToEndLimit = false;

        public BattleUnit Player { get; private set; }
        public List<EnemyUnit> Enemies { get; private set; } = new List<EnemyUnit>();

        public List<CardInstance> DrawPile { get; private set; } = new List<CardInstance>();
        public List<CardInstance> Hand { get; private set; } = new List<CardInstance>();
        public List<CardInstance> DiscardPile { get; private set; } = new List<CardInstance>();
        public List<CardInstance> ExhaustPile { get; private set; } = new List<CardInstance>();

        public int Energy { get; private set; }
        public int EnergyMax { get; private set; } = 3;
        public int TurnNumber { get; private set; }
        public BattlePhase Phase { get; private set; } = BattlePhase.NotStarted;

        public List<string> Log { get; } = new List<string>();
        public Random Rng { get; private set; }

        public event Action OnStateChanged;
        public event Action<string> OnLogged;

        public bool PlayerTurnActive => Phase == BattlePhase.PlayerAction;

        private void Changed() => OnStateChanged?.Invoke();

        private void LogLine(string s)
        {
            Log.Add(s);
            OnLogged?.Invoke(s);
        }

        // ------------------------------------------------------------------
        // 初始化
        // ------------------------------------------------------------------

        public void Start(int seed, int playerHp, int playerMaxHp, List<CardRefLite> deck, List<EnemyDef> encounter)
        {
            Rng = new Random(seed * 31 + 17);

            Player = new BattleUnit("玩家", playerMaxHp) { hp = Num.ClampHp(playerHp, playerMaxHp) };

            Enemies.Clear();
            foreach (var def in encounter) Enemies.Add(new EnemyUnit(def));

            DrawPile.Clear();
            Hand.Clear();
            DiscardPile.Clear();
            ExhaustPile.Clear();

            foreach (var c in deck)
            {
                var data = CardDb.Get(c.id);
                if (data == null)
                {
                    UnityEngine.Debug.LogWarning($"[Battle] 未知卡牌 id: {c.id}");
                    continue;
                }
                DrawPile.Add(new CardInstance(data, c.upgraded));
            }

            Shuffle(DrawPile);
            EnergyMax = 3;
            Energy = EnergyMax;
            TurnNumber = 0;

            LogLine($"战斗开始：{Enemies.Count} 个敌人");
            StartPlayerTurn();
        }

        private void Shuffle(List<CardInstance> pile)
        {
            for (int i = pile.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                var t = pile[i]; pile[i] = pile[j]; pile[j] = t;
            }
        }

        // ------------------------------------------------------------------
        // 玩家回合
        // ------------------------------------------------------------------

        public void StartPlayerTurn()
        {
            if (Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat) return;

            TurnNumber++;
            Phase = BattlePhase.PlayerTurnStart;

            // 0. 回合结束的手牌处理：
            //    KeepAll（默认）→ 手牌跨回合保留，最多累到 HandLimit(10)；
            //    DiscardAll     → 开局前先清空，每回合重新抽 DrawPerTurn 张。
            // 之所以把弃牌放在这里而不是 EndTurn：结束回合时手牌不消失，看得见更直观。
            if (handPolicy == HandPolicy.DiscardAll && Hand.Count > 0)
            {
                DiscardPile.AddRange(Hand);
                Hand.Clear();
            }

            // 1. 玩家格挡清零
            Player.ResetBlock();

            // 2. 能量重置
            Energy = EnergyMax;

            // 3. 中毒结算（回合开始失去生命，无视格挡）
            int poison = Player.GetStatus(StatusType.Poison);
            if (poison > 0)
            {
                Player.hp = Math.Max(0, Player.hp - poison);
                Player.AddStatus(StatusType.Poison, -1);
                LogLine($"中毒发作，失去 {poison} 点生命");
            }

            // 4. 抽牌。
            //    KeepAll 下按空位抽，避免把溢出的牌白白烧进弃牌堆：
            //    默认封顶 HandLimit(10)，打开 drawFillToEndLimit 则封顶 EndTurnHandLimit(5)。
            int drawN = DrawPerTurn;
            if (handPolicy == HandPolicy.KeepAll)
            {
                int cap = drawFillToEndLimit ? EndTurnHandLimit : HandLimit;
                int space = cap - Hand.Count;
                drawN = space <= 0 ? 0 : Math.Min(DrawPerTurn, space);
                if (drawN < DrawPerTurn)
                    LogLine(drawN == 0 ? "手牌已满，本回合不抽牌" : $"手牌接近上限，本回合只抽 {drawN} 张");
            }
            DrawCards(drawN);

            // 5. 状态递减（易伤/虚弱/脆弱在回合结束时递减，这里不处理）

            Phase = BattlePhase.PlayerAction;
            LogLine($"—— 第 {TurnNumber} 回合 ——");
            CheckBattleEnd();
            Changed();
        }

        public int DrawCards(int count)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (Hand.Count >= HandLimit)
                {
                    LogLine("手牌已满，抽到的牌直接进入弃牌堆");
                    if (DrawPile.Count == 0 && DiscardPile.Count == 0) break;
                    // 仍然消耗这张牌
                    if (DrawPile.Count == 0) ReshuffleDiscardIntoDraw();
                    if (DrawPile.Count == 0) break;
                    DiscardPile.Add(DrawPile[0]);
                    DrawPile.RemoveAt(0);
                    continue;
                }

                if (DrawPile.Count == 0)
                {
                    if (DiscardPile.Count == 0) break;
                    ReshuffleDiscardIntoDraw();
                }
                if (DrawPile.Count == 0) break;

                var card = DrawPile[0];
                DrawPile.RemoveAt(0);
                Hand.Add(card);
                drawn++;
            }
            return drawn;
        }

        private void ReshuffleDiscardIntoDraw()
        {
            DrawPile.AddRange(DiscardPile);
            DiscardPile.Clear();
            Shuffle(DrawPile);
            LogLine("弃牌堆洗回抽牌堆");
        }

        // ------------------------------------------------------------------
        // 出牌
        // ------------------------------------------------------------------

        public bool CanPlay(CardInstance card, int targetIndex, out string reason)
        {
            reason = null;
            if (card == null) { reason = "卡牌不存在"; return false; }
            if (Phase != BattlePhase.PlayerAction) { reason = "不是你行动的时候"; return false; }
            if (Energy < card.Cost) { reason = "能量不足"; return false; }

            if (card.Data.target == TargetMode.SingleEnemy)
            {
                if (targetIndex < 0 || targetIndex >= Enemies.Count || Enemies[targetIndex].IsDead)
                {
                    reason = "需要选择一个存活目标";
                    return false;
                }
            }
            return true;
        }

        public bool PlayCard(int handIndex, int targetIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return false;
            var card = Hand[handIndex];

            if (!CanPlay(card, targetIndex, out string reason))
            {
                LogLine($"无法打出「{card.Name}」：{reason}");
                return false;
            }

            Phase = BattlePhase.CardResolving;
            Energy -= card.Cost;
            Hand.RemoveAt(handIndex);

            LogLine($"打出「{card.Name}」");

            ResolveEffects(card, targetIndex);

            if (card.Data.exhaust) ExhaustPile.Add(card);
            else DiscardPile.Add(card);
            if (card.Data.type == CardType.Power) { /* 能力牌打出后离场，简化处理为进弃牌堆 */ }

            if (Phase != BattlePhase.Victory && Phase != BattlePhase.Defeat)
                Phase = BattlePhase.PlayerAction;

            CheckBattleEnd();
            Changed();
            return true;
        }

        private void ResolveEffects(CardInstance card, int targetIndex)
        {
            var effects = card.Data.GetEffects(card.Upgraded);
            foreach (var e in effects)
            {
                switch (e.op)
                {
                    case EffectOp.Damage:
                    {
                        var target = EnemyAt(targetIndex);
                        if (target != null)
                        {
                            for (int t = 0; t < Num.Max(1, e.times); t++)
                            {
                                if (target.IsDead) break;
                                Attack(Player, target, e.value);
                            }
                        }
                        break;
                    }

                    case EffectOp.DamageAll:
                    {
                        foreach (var en in Enemies.ToArray())
                        {
                            if (!en.IsDead) Attack(Player, en, e.value);
                        }
                        break;
                    }

                    case EffectOp.Block:
                        GainBlock(Player, e.value);
                        break;

                    case EffectOp.Draw:
                        DrawCards(e.value);
                        break;

                    case EffectOp.ApplySelf:
                        Player.AddStatus(e.status, e.value);
                        LogLine($"获得 {e.value} 层{CardData.StatusName(e.status)}");
                        break;

                    case EffectOp.ApplyTarget:
                    {
                        var target = EnemyAt(targetIndex);
                        if (target != null)
                        {
                            target.AddStatus(e.status, e.value);
                            LogLine($"{target.name} 获得 {e.value} 层{CardData.StatusName(e.status)}");
                        }
                        break;
                    }

                    case EffectOp.ApplyAll:
                        foreach (var en in Enemies)
                        {
                            if (!en.IsDead)
                            {
                                en.AddStatus(e.status, e.value);
                                LogLine($"{en.name} 获得 {e.value} 层{CardData.StatusName(e.status)}");
                            }
                        }
                        break;

                    case EffectOp.GainEnergy:
                        Energy += e.value;
                        break;

                    case EffectOp.Heal:
                        Player.hp = Math.Min(Player.maxHp, Player.hp + e.value);
                        break;

                    case EffectOp.LoseHp:
                        Player.hp = Math.Max(0, Player.hp - e.value);
                        break;
                }

                if (Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat) return;
            }
        }

        private EnemyUnit EnemyAt(int idx)
        {
            if (idx < 0 || idx >= Enemies.Count) return null;
            var e = Enemies[idx];
            return e.IsDead ? null : e;
        }

        // ------------------------------------------------------------------
        // 伤害结算链（设计文档 §6.3）
        // ------------------------------------------------------------------

        /// <summary>
        /// 完整伤害流程：
        /// base + 力量 → ×虚弱(0.75) → ×易伤(1.5) → 扣格挡 → 扣生命 → 荆棘反弹
        /// 每步均向下取整。
        /// </summary>
        public int Attack(BattleUnit attacker, BattleUnit target, int baseDamage)
        {
            if (target == null || target.IsDead) return 0;

            // 1~2. 力量
            int dmg = baseDamage + attacker.GetStatus(StatusType.Strength);

            // 3. 攻方虚弱
            if (attacker.GetStatus(StatusType.Weak) > 0)
                dmg = (int)Math.Floor(dmg * 0.75);

            // 4. 受方易伤
            if (target.GetStatus(StatusType.Vulnerable) > 0)
                dmg = (int)Math.Floor(dmg * 1.5);

            if (dmg < 0) dmg = 0;

            // 6. 格挡吸收
            int absorbed = Math.Min(target.block, dmg);
            target.block -= absorbed;
            int toHp = dmg - absorbed;

            // 7. 扣生命
            target.hp = Math.Max(0, target.hp - toHp);

            LogLine($"{attacker.name} 对 {target.name} 造成 {dmg} 伤害" +
                    (absorbed > 0 ? $"（格挡吸收 {absorbed}）" : ""));

            // 8. 荆棘反弹
            if (attacker != target)
            {
                int thorns = target.GetStatus(StatusType.Thorns);
                if (thorns > 0 && toHp > 0 && !attacker.IsDead)
                {
                    attacker.hp = Math.Max(0, attacker.hp - thorns);
                    LogLine($"{target.name} 的荆棘反弹 {thorns} 点伤害给 {attacker.name}");
                }
            }

            // 9. 死亡检查
            if (target.IsDead) LogLine($"{target.name} 被击败");

            CheckBattleEnd();
            return dmg;
        }

        public void GainBlock(BattleUnit unit, int amount)
        {
            int v = amount + unit.GetStatus(StatusType.Dexterity);
            if (unit.GetStatus(StatusType.Frail) > 0)
                v = (int)Math.Floor(v * 0.75);
            if (v < 0) v = 0;
            unit.block += v;
            LogLine($"{unit.name} 获得 {v} 点格挡");
        }

        // ------------------------------------------------------------------
        // 回合结束 / 敌人回合
        // ------------------------------------------------------------------

        /// <summary>回合结束时允许保留的手牌数。超过就要进入弃牌环节。</summary>
        public const int EndTurnHandLimit = 5;

        /// <summary>是否需要在结束回合前手动弃牌（手牌超过 EndTurnHandLimit）。</summary>
        public bool NeedsDiscard => Hand.Count > EndTurnHandLimit;

        /// <summary>本回合玩家必须弃掉的张数。</summary>
        public int DiscardRequired => Math.Max(0, Hand.Count - EndTurnHandLimit);

        /// <summary>
        /// 把指定下标的牌从手牌移入弃牌堆（玩家自选弃牌用）。
        /// 下标降序处理，避免删除后后续下标偏移。
        /// </summary>
        public void DiscardFromHand(List<int> handIndices)
        {
            if (handIndices == null || handIndices.Count == 0) return;

            var sorted = new List<int>(handIndices);
            sorted.Sort();
            sorted.Reverse();

            foreach (int i in sorted)
            {
                if (i < 0 || i >= Hand.Count) continue;
                DiscardPile.Add(Hand[i]);
                Hand.RemoveAt(i);
            }

            LogLine($"弃掉 {sorted.Count} 张牌，手牌剩 {Hand.Count} 张");
            Changed();
        }

        public void EndTurn()
        {
            if (Phase != BattlePhase.PlayerAction) return;

            Phase = BattlePhase.TurnEnding;

            // 手牌保留：不在此处弃牌。超过 EndTurnHandLimit 的部分由玩家
            // 在「弃牌环节」自行选择（见 BattleScreen.ShowDiscardDialog）；
            // 未超限的牌跨回合保留，所以手牌会在 0~5 之间累积。

            // 玩家回合结束状态：再生、易伤递减
            int regen = Player.GetStatus(StatusType.Regen);
            if (regen > 0)
            {
                Player.hp = Math.Min(Player.maxHp, Player.hp + regen);
                Player.AddStatus(StatusType.Regen, -1);
                LogLine($"再生回复 {regen} 点生命");
            }
            Player.TickDownDurations();

            Phase = BattlePhase.EnemyTurn;
            Changed();
        }

        /// <summary>让第 i 个敌人行动一次（由 UI 逐帧驱动，便于播放动画）。</summary>
        public void EnemyAct(int index)
        {
            if (index < 0 || index >= Enemies.Count) return;
            var enemy = Enemies[index];
            if (enemy.IsDead) return;

            var intent = enemy.CurrentIntent;
            if (intent == null) return;

            switch (intent.type)
            {
                case IntentType.Attack:
                case IntentType.AttackMulti:
                    for (int t = 0; t < Num.Max(1, intent.times); t++)
                    {
                        if (Player.IsDead) break;
                        Attack(enemy, Player, intent.value);
                    }
                    break;

                case IntentType.Defend:
                    GainBlock(enemy, intent.value);
                    break;

                case IntentType.Buff:
                    enemy.AddStatus(intent.status, intent.statusValue > 0 ? intent.statusValue : intent.value);
                    LogLine($"{enemy.name} 强化：获得 {intent.statusValue} 层{CardData.StatusName(intent.status)}");
                    break;

                case IntentType.Debuff:
                    Player.AddStatus(intent.status, intent.statusValue > 0 ? intent.statusValue : intent.value);
                    LogLine($"{enemy.name} 对你施加 {intent.statusValue} 层{CardData.StatusName(intent.status)}");
                    break;

                case IntentType.Sleep:
                    LogLine($"{enemy.name} 正在睡眠");
                    break;
            }

            enemy.TickDownDurations();
            enemy.AdvanceIntent();
            CheckBattleEnd();
            Changed();
        }

        /// <summary>敌人回合开始：清空格挡 + 中毒结算。</summary>
        public void BeginEnemyTurn()
        {
            foreach (var e in Enemies)
            {
                if (e.IsDead) continue;
                e.ResetBlock();

                int poison = e.GetStatus(StatusType.Poison);
                if (poison > 0)
                {
                    e.hp = Math.Max(0, e.hp - poison);
                    e.AddStatus(StatusType.Poison, -1);
                    LogLine($"{e.name} 中毒失去 {poison} 点生命");
                    if (e.IsDead) LogLine($"{e.name} 被击败");
                }
            }
            CheckBattleEnd();
            Changed();
        }

        public void FinishEnemyTurn()
        {
            if (Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat) return;
            StartPlayerTurn();
        }

        // ------------------------------------------------------------------
        // 胜负
        // ------------------------------------------------------------------

        public bool IsVictory => Enemies.TrueForAll(e => e.IsDead);
        public bool IsDefeat => Player.IsDead;

        public void CheckBattleEnd()
        {
            if (Phase == BattlePhase.Victory || Phase == BattlePhase.Defeat) return;

            if (Player.IsDead)
            {
                Phase = BattlePhase.Defeat;
                LogLine("你倒下了……");
                Changed();
                return;
            }

            if (IsVictory)
            {
                Phase = BattlePhase.Victory;
                LogLine("战斗胜利！");
                Changed();
            }
        }

        public int AliveEnemyCount => Enemies.FindAll(e => !e.IsDead).Count;

        /// <summary>额外获得/扣除行动点（遗物、卡牌效果用）。</summary>
        public void AddEnergy(int amount)
        {
            Energy = Math.Max(0, Energy + amount);
            if (amount > 0) LogLine($"获得 {amount} 点行动点");
            Changed();
        }

        /// <summary>额外提高行动点上限（遗物用）。</summary>
        public void AddEnergyMax(int amount)
        {
            EnergyMax = Math.Max(1, EnergyMax + amount);
            Energy = EnergyMax;
        }

        // ------------------------------------------------------------------
        // 消耗型道具
        // ------------------------------------------------------------------

        /// <summary>道具是否可用（战斗中 + 玩家回合 + 目标合法）。</summary>
        public bool CanUseItem(ItemDef item, int targetIndex, out string reason)
        {
            reason = null;
            if (item == null) { reason = "道具不存在"; return false; }
            if (Phase != BattlePhase.PlayerAction) { reason = "现在不能使用道具"; return false; }

            if (item.target == TargetMode.SingleEnemy)
            {
                if (targetIndex < 0 || targetIndex >= Enemies.Count || Enemies[targetIndex].IsDead)
                {
                    reason = "需要选择一个存活目标";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 使用道具。效果结算复用卡牌那套 EffectOp，所以治疗/格挡/状态/伤害都一致。
        /// 道具不消耗行动点（药水靠数量限制，不靠能量）。
        /// </summary>
        public bool UseItem(ItemDef item, int targetIndex)
        {
            if (!CanUseItem(item, targetIndex, out string reason))
            {
                LogLine($"无法使用「{(item == null ? "?" : item.name)}」：{reason}");
                return false;
            }

            LogLine($"使用道具「{item.name}」");

            // 直接复用卡牌效果结算
            var fake = new CardInstance(new CardData
            {
                id = item.id,
                name = item.name,
                type = CardType.Skill,
                target = item.target,
                cost = 0,
                effects = item.effects
            }, false);

            ApplyEffects(fake, targetIndex);

            CheckBattleEnd();
            Changed();
            return true;
        }

        private void ApplyEffects(CardInstance card, int targetIndex) => ResolveEffects(card, targetIndex);
    }

    /// <summary>战斗初始化用的轻量卡牌引用（避免 Battle 依赖 Core）。</summary>
    public struct CardRefLite
    {
        public string id;
        public bool upgraded;

        public CardRefLite(string id, bool upgraded)
        {
            this.id = id;
            this.upgraded = upgraded;
        }
    }

    internal static class Num
    {
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int ClampHp(int hp, int maxHp) => hp < 0 ? 0 : (hp > maxHp ? maxHp : hp);
    }
}
