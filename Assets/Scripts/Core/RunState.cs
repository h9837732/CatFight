using System;
using System.Collections.Generic;

namespace CatFight.Core
{
    /// <summary>卡组中的一张卡（存档用，只记引用与升级状态）。</summary>
    [Serializable]
    public class CardRef
    {
        public string id;
        public bool upgraded;

        public CardRef() { }
        public CardRef(string id, bool upgraded = false) { this.id = id; this.upgraded = upgraded; }
    }

    /// <summary>
    /// 一局游戏（Run）的全部持久状态。存档内容就是它。
    /// 不包含任何战斗中间态——战斗结束就丢弃。
    /// </summary>
    [Serializable]
    public class RunState
    {
        public int version = 1;

        // --- 地图 ---
        public int seed;
        public int generatorVersion = 1;
        public List<int> visitedPath = new List<int>();     // 已访问节点 id 序列
        public int act = 1;

        // --- 玩家 ---
        public string characterId = "ironclad";
        public int hp = 80;
        public int maxHp = 80;
        public int gold = 99;

        // --- 收藏 ---
        public List<CardRef> deck = new List<CardRef>();
        public List<string> relics = new List<string>();
        /// <summary>消耗型道具（药水）。战斗中可使用，用后消失。
        /// 存档字段后加，旧档读入时为 null，由 SaveService 初始化为空表。</summary>
        public List<string> items = new List<string>();

        public int CurrentNodeId => visitedPath.Count > 0 ? visitedPath[visitedPath.Count - 1] : -1;
        public int RowsCleared => visitedPath.Count;

        public static RunState NewRun(int seed)
        {
            var run = new RunState
            {
                seed = seed,
                maxHp = 80,
                hp = 80,
                gold = 99,
            };
            run.deck.AddRange(StarterDeck());
            // 开局带一瓶治疗药水，保证道具欄一开始就有东西可用
            run.items.Add("item_heal_potion");
            // 遗物不预设：开局由玩家三选一（见 MapScreen.ShowRelicChoice）
            return run;
        }

        /// <summary>初始卡组：5 打击 + 4 防御 + 1 重击。</summary>
        public static List<CardRef> StarterDeck()
        {
            var list = new List<CardRef>();
            for (int i = 0; i < 5; i++) list.Add(new CardRef("strike"));
            for (int i = 0; i < 4; i++) list.Add(new CardRef("defend"));
            list.Add(new CardRef("bash"));
            return list;
        }
    }
}
