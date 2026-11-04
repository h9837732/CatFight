using System;
using UnityEngine;

namespace CatFight.EditorTools
{
    /// <summary>
    /// 命令行调试入口。用 `unity command script run --file ... --entry ...` 调用。
    /// 走的是"编译项目文件并执行静态入口"的通道，不依赖出问题的 script eval。
    /// </summary>
    public static class DebugEntry
    {
        public static object Run(string[] args)
        {
            string cmd = (args != null && args.Length > 0) ? args[0] : "help";
            Debug.Log($"[DebugEntry] cmd={cmd} argc={args?.Length ?? 0}");

            switch (cmd)
            {
                case "ping":
                    return "pong";

                case "describe":
                {
                    int seed = args.Length > 1 ? int.Parse(args[1]) : 1234;
                    string text = MapPreviewTool.Describe(seed);
                    string path = System.IO.Path.Combine(
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")),
                        "Docs/Design", $"map_seed{seed}.txt");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
                    return text;
                }

                case "render":
                {
                    int seed = args.Length > 1 ? int.Parse(args[1]) : 1234;
                    string path = System.IO.Path.Combine(
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")),
                        "Docs/Design", $"preview_seed{seed}.png");
                    return MapPreviewTool.Render(seed, path);
                }

                case "stats":
                {
                    int from = args.Length > 1 ? int.Parse(args[1]) : 1;
                    int to = args.Length > 2 ? int.Parse(args[2]) : 1000;
                    var cfg = Map.MapConfig.CreateDefault();
                    try
                    {
                        int minPaths = int.MaxValue, maxPaths = 0, minNodes = int.MaxValue, maxNodes = 0;
                        for (int s = from; s <= to; s++)
                        {
                            var m = Map.MapGenerator.Generate(s, cfg);
                            int p = m.EnumeratePaths().Count;
                            if (p < minPaths) minPaths = p;
                            if (p > maxPaths) maxPaths = p;
                            if (m.nodes.Count < minNodes) minNodes = m.nodes.Count;
                            if (m.nodes.Count > maxNodes) maxNodes = m.nodes.Count;
                        }
                        return $"seeds {from}..{to}: paths[{minPaths}..{maxPaths}] nodes[{minNodes}..{maxNodes}]";
                    }
                    finally { UnityEngine.Object.DestroyImmediate(cfg); }
                }

                default:
                    return "commands: ping | describe <seed> | render <seed> | stats <from> <to>";
            }
        }
    }
}
