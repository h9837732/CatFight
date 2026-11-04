using UnityEngine;

namespace CatFight.UI
{
    /// <summary>全局视觉参数。占位素材阶段的配色与字号都集中在这里，换美术时只改此文件。</summary>
    public static class Theme
    {
        public static readonly Color Paper      = Hex("E8DCC0");
        public static readonly Color PaperDark  = Hex("D6C6A4");
        public static readonly Color Ink        = Hex("2B2622");
        public static readonly Color InkSoft    = Hex("4A423A");
        public static readonly Color InkFaint   = Hex("8A7F70");

        public static readonly Color PanelBg    = Hex("1A1A1E");
        public static readonly Color PanelBg2   = Hex("242428");

        public static readonly Color HpRed      = Hex("C0392B");
        public static readonly Color HpBack     = Hex("2A1512");
        public static readonly Color BlockBlue  = Hex("4A9CE8");
        public static readonly Color EnergyCyan = Hex("2FB6C4");
        public static readonly Color Gold       = Hex("D9A441");

        public static readonly Color Elite      = Hex("7A1F1F");
        public static readonly Color Shop       = Hex("8A6A2F");
        public static readonly Color Rest       = Hex("B5561E");
        public static readonly Color Treasure   = Hex("6B4A2A");
        public static readonly Color Boss       = Hex("5A0F14");
        public static readonly Color EventCol   = Hex("3A332B");
        public static readonly Color BattleCol  = Hex("2B2622");

        public static readonly Color Attack     = Hex("8C3A2E");
        public static readonly Color Skill      = Hex("2E5C8C");
        public static readonly Color Power      = Hex("6B4A8C");

        public static readonly Color Ok         = Hex("4E9E5A");
        public static readonly Color Warn       = Hex("C9A227");
        public static readonly Color Bad        = Hex("B23A3A");

        // 字号（基准 1920×1080）
        public const int FontHuge   = 72;
        public const int FontTitle  = 48;
        public const int FontHead   = 34;
        public const int FontBody   = 26;
        public const int FontSmall  = 20;
        public const int FontTiny   = 16;

        public static Color Hex(string hex)
        {
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }

        public static Color HexA(string hex, float alpha)
        {
            var c = Hex(hex);
            c.a = alpha;
            return c;
        }

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>节点类型 → 颜色。</summary>
        public static Color ColorFor(Map.NodeType t)
        {
            switch (t)
            {
                case Map.NodeType.Battle:   return BattleCol;
                case Map.NodeType.Elite:    return Elite;
                case Map.NodeType.Event:    return EventCol;
                case Map.NodeType.Shop:     return Shop;
                case Map.NodeType.Rest:     return Rest;
                case Map.NodeType.Treasure: return Treasure;
                case Map.NodeType.Boss:     return Boss;
                default: return Ink;
            }
        }

        /// <summary>节点类型 → 单字标签（无美术阶段的占位图形）。</summary>
        public static string GlyphFor(Map.NodeType t)
        {
            switch (t)
            {
                case Map.NodeType.Battle:   return "战";
                case Map.NodeType.Elite:    return "精";
                case Map.NodeType.Event:    return "？";
                case Map.NodeType.Shop:     return "商";
                case Map.NodeType.Rest:     return "休";
                case Map.NodeType.Treasure: return "宝";
                case Map.NodeType.Boss:     return "王";
                default: return "?";
            }
        }

        /// <summary>节点类型 → 中文名。</summary>
        public static string NameFor(Map.NodeType t)
        {
            switch (t)
            {
                case Map.NodeType.Battle:   return "战斗";
                case Map.NodeType.Elite:    return "精英";
                case Map.NodeType.Event:    return "事件";
                case Map.NodeType.Shop:     return "商店";
                case Map.NodeType.Rest:     return "休息点";
                case Map.NodeType.Treasure: return "宝箱";
                case Map.NodeType.Boss:     return "首领";
                default: return "未知";
            }
        }

        public static string DescFor(Map.NodeType t)
        {
            switch (t)
            {
                case Map.NodeType.Battle:   return "普通战斗，胜利后获得卡牌奖励";
                case Map.NodeType.Elite:    return "强敌，必定掉落遗物";
                case Map.NodeType.Event:    return "未知遭遇，多选项分支";
                case Map.NodeType.Shop:     return "购买卡牌与道具，或移除一张卡";
                case Map.NodeType.Rest:     return "恢复生命，或升级一张卡";
                case Map.NodeType.Treasure: return "免费获得遗物或道具";
                case Map.NodeType.Boss:     return "本幕最终战";
                default: return "";
            }
        }
    }
}
