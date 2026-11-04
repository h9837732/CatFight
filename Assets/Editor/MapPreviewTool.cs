using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CatFight.Map;
using UnityEditor;
using UnityEngine;

namespace CatFight.EditorTools
{
    /// <summary>
    /// 地图生成结果的离屏预览工具（开发期专用，不属于运行时）。
    /// 输出：① PNG 拓扑图 ② 文本结构摘要，用于人工核对生成质量。
    /// </summary>
    public static class MapPreviewTool
    {
        private const float Scale = 0.95f;
        private const float Margin = 120f;      // 未缩放单位
        private const float NodeRadius = 26f;

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [MenuItem("CatFight/Map/Render Preview")]
        public static void RenderMenu()
        {
            foreach (int seed in new[] { 1234, 777, 20260101 })
            {
                string png = Path.Combine(ProjectRoot, $"Docs/Design/preview_seed{seed}.png");
                Render(seed, png);
            }
        }

        [MenuItem("CatFight/Map/Dump Structure")]
        public static void DumpMenu()
        {
            var sb = new StringBuilder();
            foreach (int seed in new[] { 1234, 777, 20260101 })
            {
                sb.AppendLine(Describe(seed));
                sb.AppendLine(new string('-', 72));
            }
            string path = Path.Combine(ProjectRoot, "Docs/Design/map_dump.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[MapPreview] structure dump -> {path}");
        }

        /// <summary>生成指定种子的预览图，返回输出路径。</summary>
        public static string Render(int seed, string outputPath)
        {
            MapConfig cfg = MapConfig.CreateDefault();
            try
            {
                MapData map = MapGenerator.Generate(seed, cfg);
                int w = Mathf.RoundToInt((cfg.mapWidth + Margin * 2f) * Scale);
                int h = Mathf.RoundToInt(((cfg.rowCount - 1) * cfg.rowSpacing + Margin * 2f) * Scale);

                var px = new Color32[w * h];
                Color32 paper = Hex("E8DCC0");
                for (int i = 0; i < px.Length; i++) px[i] = paper;

                // 1) 先画边
                Color32 ink = Hex("2B2622");
                for (int i = 0; i < map.edges.Count; i++)
                {
                    var a = map.GetNode(map.edges[i].from);
                    var b = map.GetNode(map.edges[i].to);
                    DrawLine(px, w, h, ToPixel(a.pos, cfg, h), ToPixel(b.pos, cfg, h), ink, 2);
                }

                // 2) 再画节点 + 类型图标
                for (int i = 0; i < map.nodes.Count; i++)
                {
                    var node = map.nodes[i];
                    Vector2Int p = ToPixel(node.pos, cfg, h);
                    Color32 c = TypeColor(node.type);

                    FillCircle(px, w, h, p, Mathf.RoundToInt(NodeRadius * Scale), paper);
                    DrawCircle(px, w, h, p, Mathf.RoundToInt(NodeRadius * Scale), c, 2);
                    DrawIcon(px, w, h, p, node.type, c);
                }

                WritePng(px, w, h, outputPath);
                Debug.Log($"[MapPreview] seed={seed} nodes={map.nodes.Count} edges={map.edges.Count} -> {outputPath}");
                return outputPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        /// <summary>把地图结构写成易读文本，便于在日志/终端里核对。</summary>
        public static string Describe(int seed)
        {
            MapConfig cfg = MapConfig.CreateDefault();
            try
            {
                MapData map = MapGenerator.Generate(seed, cfg);
                var sb = new StringBuilder();
                sb.AppendLine($"seed={map.seed}  rows={map.rowCount}  nodes={map.nodes.Count}  edges={map.edges.Count}  paths={map.EnumeratePaths().Count}");
                sb.AppendLine();

                for (int r = 0; r < map.rowCount; r++)
                {
                    var ids = map.Rows[r];
                    sb.Append($"row{r:D2} y={(-r * 140f):F0} count={ids.Count}  ");
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var n = map.GetNode(ids[i]);
                        sb.Append($"#{i}{Glyph(n.type)}x={n.pos.x:F0}  ");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();

                for (int r = 0; r < map.rowCount; r++)
                {
                    var ids = map.Rows[r];
                    sb.Append($"r{r:D2} ");
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var n = map.GetNode(ids[i]);
                        sb.Append($"[{Glyph(n.type)}]");
                        if (i + 1 < ids.Count) sb.Append("  ");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine();
                sb.AppendLine("edges:");
                for (int r = 0; r + 1 < map.rowCount; r++)
                {
                    var ids = map.Rows[r];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        var n = map.GetNode(ids[i]);
                        var targets = new List<string>();
                        for (int k = 0; k < n.next.Count; k++)
                        {
                            var t = map.GetNode(n.next[k]);
                            targets.Add($"{Glyph(t.type)}r{t.row}.{t.index}");
                        }
                        sb.Append($"  r{r}.{i}{Glyph(n.type)} -> {string.Join(", ", targets)}");
                        sb.AppendLine();
                    }
                }

                var counts = new Dictionary<NodeType, int>();
                for (int i = 0; i < map.nodes.Count; i++)
                {
                    counts.TryGetValue(map.nodes[i].type, out int c);
                    counts[map.nodes[i].type] = c + 1;
                }
                sb.AppendLine();
                sb.Append("counts: ");
                foreach (var kv in counts) sb.Append($"{kv.Key}={kv.Value} ");
                sb.AppendLine();

                return sb.ToString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cfg);
            }
        }

        public static string Glyph(NodeType t)
        {
            switch (t)
            {
                case NodeType.Battle: return "B";
                case NodeType.Elite: return "E";
                case NodeType.Event: return "?";
                case NodeType.Shop: return "$";
                case NodeType.Rest: return "R";
                case NodeType.Treasure: return "T";
                case NodeType.Boss: return "X";
                case NodeType.Start: return "o";
                default: return ".";
            }
        }

        // ------------------------------------------------------------------
        // 绘图基元
        // ------------------------------------------------------------------

        private static Vector2Int ToPixel(Vector2 pos, MapConfig cfg, int h)
        {
            // pos.y 向上递增（起点 y=0 在图像最下方），图像坐标 y 向下递增，所以要翻转
            int x = Mathf.RoundToInt((pos.x + cfg.mapWidth * 0.5f + Margin) * Scale);
            int y = h - 1 - Mathf.RoundToInt((pos.y + Margin) * Scale);
            return new Vector2Int(x, y);
        }

        private static Color32 TypeColor(NodeType t)
        {
            switch (t)
            {
                case NodeType.Battle: return Hex("2B2622");
                case NodeType.Elite: return Hex("7A1F1F");
                case NodeType.Event: return Hex("3A332B");
                case NodeType.Shop: return Hex("8A6A2F");
                case NodeType.Rest: return Hex("B5561E");
                case NodeType.Treasure: return Hex("6B4A2A");
                case NodeType.Boss: return Hex("5A0F14");
                case NodeType.Start: return Hex("8A7F70");
                default: return Hex("000000");
            }
        }

        private static Color32 Hex(string hex)
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }

        private static void Set( Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return;
            px[y * w + x] = c;
        }

        private static void FillCircle(Color32[] px, int w, int h, Vector2Int c, int r, Color32 col)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r) Set(px, w, h, c.x + x, c.y + y, col);
        }

        private static void DrawCircle(Color32[] px, int w, int h, Vector2Int c, int r, Color32 col, int thickness)
        {
            int inner = r - thickness;
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int d = x * x + y * y;
                    if (d <= r * r && d > inner * inner) Set(px, w, h, c.x + x, c.y + y, col);
                }
        }

        private static void DrawLine(Color32[] px, int w, int h, Vector2Int a, Vector2Int b, Color32 col, int thickness)
        {
            int dx = Mathf.Abs(b.x - a.x), sx = a.x < b.x ? 1 : -1;
            int dy = -Mathf.Abs(b.y - a.y), sy = a.y < b.y ? 1 : -1;
            int err = dx + dy;
            int x = a.x, y = a.y;
            int half = thickness / 2;

            while (true)
            {
                for (int oy = -half; oy <= half; oy++)
                    for (int ox = -half; ox <= half; ox++)
                        Set(px, w, h, x + ox, y + oy, col);

                if (x == b.x && y == b.y) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
            }
        }

        private static void FillRect(Color32[] px, int w, int h, int x0, int y0, int x1, int y1, Color32 col)
        {
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
            if (y0 > y1) { int t = y0; y0 = y1; y1 = t; }
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++) Set(px, w, h, x, y, col);
        }

        private static void DrawIcon(Color32[] px, int w, int h, Vector2Int c, NodeType t, Color32 col)
        {
            int s = Mathf.RoundToInt(NodeRadius * Scale * 0.55f);
            switch (t)
            {
                case NodeType.Battle:                       // 交叉双剑 → X
                    DrawLine(px, w, h, new Vector2Int(c.x - s, c.y - s), new Vector2Int(c.x + s, c.y + s), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x - s, c.y + s), new Vector2Int(c.x + s, c.y - s), col, 2);
                    break;

                case NodeType.Elite:                        // 爪痕 → 三角
                    DrawLine(px, w, h, new Vector2Int(c.x, c.y + s), new Vector2Int(c.x - s, c.y - s), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x, c.y + s), new Vector2Int(c.x + s, c.y - s), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x - s, c.y - s), new Vector2Int(c.x + s, c.y - s), col, 2);
                    FillCircle(px, w, h, new Vector2Int(c.x, c.y - s / 2), Mathf.Max(1, s / 5), col);
                    break;

                case NodeType.Event:                        // 问号 → 菱形 + 点
                    DrawLine(px, w, h, new Vector2Int(c.x, c.y + s), new Vector2Int(c.x + s, c.y), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x + s, c.y), new Vector2Int(c.x, c.y - s), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x, c.y - s), new Vector2Int(c.x - s, c.y), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x - s, c.y), new Vector2Int(c.x, c.y + s), col, 2);
                    break;

                case NodeType.Shop:                         // 钱袋 → 方形
                    DrawLine(px, w, h, new Vector2Int(c.x - s, c.y + s), new Vector2Int(c.x + s, c.y + s), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x + s, c.y + s), new Vector2Int(c.x + s, c.y - s), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x + s, c.y - s), new Vector2Int(c.x - s, c.y - s), col, 2);
                    DrawLine(px, w, h, new Vector2Int(c.x - s, c.y - s), new Vector2Int(c.x - s, c.y + s), col, 2);
                    break;

                case NodeType.Rest:                         // 篝火 → 实心三角（向上）
                    for (int yy = -s; yy <= s; yy++)
                    {
                        int span = Mathf.RoundToInt((yy + s) * 0.5f);
                        for (int xx = -span; xx <= span; xx++) Set(px, w, h, c.x + xx, c.y + yy, col);
                    }
                    break;

                case NodeType.Treasure:                     // 宝箱 → 实心方块
                    FillRect(px, w, h, c.x - s, c.y - s, c.x + s, c.y + s, col);
                    FillRect(px, w, h, c.x - s + 2, c.y - 1, c.x + s - 2, c.y + 1, Hex("E8DCC0"));
                    break;

                case NodeType.Boss:                         // 首领 → 双环 + 实心
                    FillCircle(px, w, h, c, Mathf.Max(2, s - 2), col);
                    DrawCircle(px, w, h, c, s + 6, col, 2);
                    break;
            }
        }

        private static void WritePng(Color32[] px, int w, int h, string path)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply(false, false);
                byte[] bytes = tex.EncodeToPNG();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, bytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
