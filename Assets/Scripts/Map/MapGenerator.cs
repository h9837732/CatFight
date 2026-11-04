using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatFight.Map
{
    /// <summary>
    /// 地图生成器。纯逻辑、无场景依赖、确定性（同 seed 同配置 = 同地图）。
    ///
    /// 生成顺序（见设计文档 §5）：
    ///   A. 行宽序列  —— 以"锚点行"为界逐段生长，天然满足 |Δwidth| ≤ 1 与汇聚约束
    ///   B. 节点坐标  —— 行内均分 + 有界抖动（保持 x 单调，连边算法依赖它）
    ///   C. 连边      —— 区间覆盖式单调匹配（数学上排除边交叉），再在不产生交叉处追加边
    ///   D. 校验      —— 失败则 seed+1 重试
    ///   E. 类型分配  —— 行级允许集 + 全局配额 + 路径校验
    /// </summary>
    public static class MapGenerator
    {
        /// <summary>生成算法版本。算法变更必须递增，用于存档兼容判定。</summary>
        public const int CurrentVersion = 1;

        public static MapData Generate(int seed, MapConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (!config.TryValidate(out string cfgError))
                throw new InvalidOperationException("MapConfig 配置非法：" + cfgError);

            int attemptSeed = seed;
            string lastError = null;

            for (int attempt = 0; attempt < config.maxGenerationAttempts; attempt++)
            {
                MapData map = GenerateOnce(attemptSeed, config);

                if (!MapValidator.ValidateStructure(map, config, out string structuralError))
                {
                    lastError = structuralError;
                    attemptSeed = unchecked(attemptSeed + 1);
                    continue;
                }

                AssignTypes(map, config, new System.Random(unchecked(attemptSeed * 397) ^ 0x5F3759));

                if (!MapValidator.ValidateTypes(map, config, out string typeError))
                {
                    lastError = typeError;
                    attemptSeed = unchecked(attemptSeed + 1);
                    continue;
                }

                map.seed = attemptSeed;
                map.BuildIndex();
                return map;
            }

            throw new InvalidOperationException(
                $"地图生成失败：{config.maxGenerationAttempts} 次尝试均未通过校验（起始 seed {seed}）。最后一次错误：{lastError}");
        }

        // ------------------------------------------------------------------
        // 结构
        // ------------------------------------------------------------------

        private static MapData GenerateOnce(int seed, MapConfig cfg)
        {
            var rng = new System.Random(seed);
            int rowCount = cfg.rowCount;
            var map = new MapData { seed = seed, generatorVersion = CurrentVersion, rowCount = rowCount };

            int[] counts = BuildRowCounts(rng, cfg);

            // B. 节点坐标
            for (int r = 0; r < rowCount; r++)
            {
                int count = counts[r];
                float cell = cfg.mapWidth / count;
                for (int i = 0; i < count; i++)
                {
                    float centerX = (i + 0.5f) * cell - cfg.mapWidth * 0.5f;
                    float jitter = (float)((rng.NextDouble() * 2.0 - 1.0) * cfg.jitterRatio * cell);
                    map.nodes.Add(new MapNode
                    {
                        id = r * 100 + i,
                        row = r,
                        index = i,
                        type = NodeType.Battle,
                        pos = new Vector2(centerX + jitter, -r * cfg.rowSpacing)
                    });
                }
            }
            map.BuildIndex();

            // C. 连边
            for (int r = 0; r < rowCount - 1; r++) ConnectRows(map, cfg, rng, r);

            // BOSS 行固定为 Boss 类型（此时只占位，类型分配会跳过非末行规则）
            var boss = map.GetNode(map.BossId);
            if (boss != null) boss.type = NodeType.Boss;

            return map;
        }

        /// <summary>A. 行宽序列：锚点行（起点、汇聚行、终点）固定为 1，段内随机生长。</summary>
        private static int[] BuildRowCounts(System.Random rng, MapConfig cfg)
        {
            int rowCount = cfg.rowCount;
            var anchors = new List<int> { 0, rowCount - 1 };
            if (cfg.convergeRows != null)
            {
                for (int i = 0; i < cfg.convergeRows.Length; i++)
                {
                    int r = cfg.convergeRows[i];
                    if (r > 0 && r < rowCount - 1 && !anchors.Contains(r)) anchors.Add(r);
                }
            }
            anchors.Sort();

            var counts = new int[rowCount];
            for (int i = 0; i < rowCount; i++) counts[i] = 1;

            for (int k = 0; k + 1 < anchors.Count; k++)
            {
                int a = anchors[k];
                int b = anchors[k + 1];
                for (int r = a + 1; r < b; r++)
                {
                    int prev = counts[r - 1];
                    int maxByStart = prev + 1;          // 相对上一行最多 +1
                    int maxByEnd = (b - r) + 1;         // 剩余行内必须能降回 1
                    int hi = Math.Min(cfg.maxNodesPerRow, Math.Min(maxByStart, maxByEnd));
                    int lo = Math.Max(1, prev - 1);
                    if (hi < lo) hi = lo;

                    int pick = WeightedIndex(rng, cfg.rowWidthWeights, cfg.maxNodesPerRow) + 1;
                    counts[r] = Math.Min(hi, Math.Max(lo, pick));
                }
            }
            return counts;
        }

        /// <summary>
        /// C. 连边。基础部分用"区间覆盖"的单调分配，保证：
        ///   ① 每个源节点至少 1 条出边  ② 每个目标节点至少 1 条入边  ③ 无交叉（单调分配必然无交叉）
        /// 之后在不产生交叉的前提下随机追加一条相邻边，制造分叉感。
        /// </summary>
        private static void ConnectRows(MapData map, MapConfig cfg, System.Random rng, int row)
        {
            var cur = map.Rows[row];
            var nxt = map.Rows[row + 1];
            int m = cur.Count;
            int n = nxt.Count;
            if (m == 0 || n == 0) return;

            var lo = new int[m];
            var hi = new int[m];

            if (n >= m)
            {
                int baseCount = n / m;
                int rem = n % m;
                int cursor = 0;
                for (int i = 0; i < m; i++)
                {
                    int c = baseCount + (i < rem ? 1 : 0);
                    lo[i] = cursor;
                    hi[i] = cursor + c - 1;
                    cursor += c;
                }
            }
            else
            {
                for (int i = 0; i < m; i++)
                {
                    int j = (int)((long)i * n / m);
                    lo[i] = j;
                    hi[i] = j;
                }
            }

            for (int i = 0; i < m; i++)
                for (int t = lo[i]; t <= hi[i]; t++)
                    map.AddEdge(cur[i], nxt[t]);

            // 追加边（仍须单调，否则交叉）—— 并受 maxOutDegree 约束
            var outDeg = new int[m];
            for (int i = 0; i < m; i++) outDeg[i] = hi[i] - lo[i] + 1;

            for (int i = 0; i < m; i++)
            {
                if (rng.NextDouble() >= cfg.twoEdgeChance) continue;
                if (outDeg[i] >= cfg.maxOutDegree) continue;      // 已满，不再追加

                int right = hi[i] + 1;
                int left = lo[i] - 1;
                int cand = rng.NextDouble() < 0.5 ? right : left;
                if (cand < 0 || cand >= n) cand = cand == right ? left : right;
                if (cand < 0 || cand >= n) continue;
                if (Crosses(map, cur, nxt, i, cand)) continue;

                map.AddEdge(cur[i], nxt[cand]);
                outDeg[i]++;
            }
        }

        /// <summary>检查"源 i → 目标 t"是否会与已有边交叉。目标索引 = id % 100。</summary>
        private static bool Crosses(MapData map, List<int> cur, List<int> nxt, int srcIdx, int tgtIdx)
        {
            for (int s = 0; s < cur.Count; s++)
            {
                var node = map.GetNode(cur[s]);
                if (node == null) continue;
                for (int k = 0; k < node.next.Count; k++)
                {
                    int ti = node.next[k] % 100;
                    if (s < srcIdx && ti > tgtIdx) return true;
                    if (s > srcIdx && ti < tgtIdx) return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // E. 类型分配
        // ------------------------------------------------------------------

        private static void AssignTypes(MapData map, MapConfig cfg, System.Random rng)
        {
            var used = new Dictionary<NodeType, int>();
            for (int r = 0; r < map.rowCount; r++)
            {
                var ids = map.Rows[r];
                var rule = cfg.GetRule(r);
                for (int i = 0; i < ids.Count; i++)
                {
                    var node = map.GetNode(ids[i]);
                    if (r == map.rowCount - 1) { node.type = NodeType.Boss; Increment(used, NodeType.Boss); continue; }
                    node.type = PickType(rng, cfg, map, rule, r, used);
                    Increment(used, node.type);
                }
            }

            EnsureMinimums(map, cfg, rng, used);
            EnforcePathConstraints(map, cfg);
        }

        private static NodeType PickType(System.Random rng, MapConfig cfg, MapData map,
                                         NodeTypeRule rule, int row, Dictionary<NodeType, int> used)
        {
            if (rule == null || rule.allowed == null || rule.allowed.Length == 0) return NodeType.Battle;

            var candidates = new List<int>(rule.allowed.Length);
            var weights = new List<float>(rule.allowed.Length);

            for (int i = 0; i < rule.allowed.Length; i++)
            {
                NodeType t = rule.allowed[i];
                if (used.TryGetValue(t, out int count) && count >= cfg.quotas.GetMax(t)) continue;
                if (t == NodeType.Elite && RowHasType(map, row - 1, NodeType.Elite)) continue;   // 精英不连续行

                candidates.Add(i);
                weights.Add(rule.weights != null && i < rule.weights.Length ? Math.Max(0.0001f, rule.weights[i]) : 1f);
            }

            if (candidates.Count == 0) return rule.allowed[0];
            return rule.allowed[candidates[WeightedIndex(rng, weights)]];
        }

        private static bool RowHasType(MapData map, int row, NodeType type)
        {
            if (row < 0 || row >= map.rowCount) return false;
            var ids = map.Rows[row];
            for (int i = 0; i < ids.Count; i++)
                if (map.GetNode(ids[i]).type == type) return true;
            return false;
        }

        /// <summary>保证关键类型至少出现 N 次（否则一局可能完全没有商店/休息点）。</summary>
        private static void EnsureMinimums(MapData map, MapConfig cfg, System.Random rng, Dictionary<NodeType, int> used)
        {
            EnsureAtLeast(map, cfg, rng, used, NodeType.Shop, 1);
            EnsureAtLeast(map, cfg, rng, used, NodeType.Rest, 2);
            EnsureAtLeast(map, cfg, rng, used, NodeType.Elite, 2);
        }

        private static void EnsureAtLeast(MapData map, MapConfig cfg, System.Random rng,
                                          Dictionary<NodeType, int> used, NodeType target, int minimum)
        {
            used.TryGetValue(target, out int have);
            if (have >= minimum) return;

            var candidates = new List<MapNode>();
            for (int r = 0; r < map.rowCount - 1; r++)
            {
                if (!cfg.IsTypeAllowedAt(r, target)) continue;
                var ids = map.Rows[r];
                for (int i = 0; i < ids.Count; i++)
                {
                    var node = map.GetNode(ids[i]);
                    if (node.type == target) continue;
                    if (!IsReplaceable(node.type)) continue;
                    if (target == NodeType.Elite && RowHasType(map, r - 1, NodeType.Elite)) continue;
                    candidates.Add(node);
                }
            }
            if (candidates.Count == 0) return;

            while (have < minimum && candidates.Count > 0)
            {
                int k = rng.Next(candidates.Count);
                var node = candidates[k];
                candidates.RemoveAt(k);

                Decrement(used, node.type);
                node.type = target;
                Increment(used, target);
                have = used[target];
            }
        }

        private static bool IsReplaceable(NodeType t)
            => t == NodeType.Battle || t == NodeType.Event || t == NodeType.Shop
               || t == NodeType.Rest || t == NodeType.Elite || t == NodeType.Treasure;

        /// <summary>枚举所有路径，确保每条路径上至少有一个休息点（否则"选错一次血线救不回来"）。</summary>
        private static void EnforcePathConstraints(MapData map, MapConfig cfg)
        {
            for (int guard = 0; guard < 60; guard++)
            {
                var paths = map.EnumeratePaths(5000);
                bool changed = false;

                for (int p = 0; p < paths.Count; p++)
                {
                    var path = paths[p];
                    if (PathHasRest(map, path)) continue;

                    // 从中段向外扩散，找第一个允许 Rest 的可替换节点
                    int mid = path.Count / 2;
                    for (int step = 0; step < path.Count; step++)
                    {
                        int k = mid + (step % 2 == 0 ? step / 2 : -(step + 1) / 2);
                        if (k <= 0 || k >= path.Count - 1) continue;

                        var node = map.GetNode(path[k]);
                        if (node == null) continue;
                        if (node.type == NodeType.Rest) break;
                        if (!cfg.IsTypeAllowedAt(node.row, NodeType.Rest)) continue;
                        if (!IsReplaceable(node.type)) continue;

                        node.type = NodeType.Rest;
                        changed = true;
                        break;
                    }
                }

                if (!changed) return;
            }
        }

        private static bool PathHasRest(MapData map, List<int> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                var node = map.GetNode(path[i]);
                if (node != null && node.type == NodeType.Rest) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------

        /// <summary>按权重返回 0-based 索引（权重来自数组前 count 项）。</summary>
        private static int WeightedIndex(System.Random rng, float[] weights, int count)
        {
            if (weights == null || count <= 0) return 0;
            int n = Math.Min(count, weights.Length);
            float total = 0f;
            for (int i = 0; i < n; i++) total += Math.Max(0f, weights[i]);
            if (total <= 0f) return rng.Next(n);

            double roll = rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < n; i++)
            {
                acc += Math.Max(0f, weights[i]);
                if (roll < acc) return i;
            }
            return n - 1;
        }

        private static int WeightedIndex(System.Random rng, List<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0f, weights[i]);
            if (total <= 0f) return rng.Next(weights.Count);

            double roll = rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                acc += Math.Max(0f, weights[i]);
                if (roll < acc) return i;
            }
            return weights.Count - 1;
        }

        private static void Increment(Dictionary<NodeType, int> d, NodeType t)
        {
            d.TryGetValue(t, out int c);
            d[t] = c + 1;
        }

        private static void Decrement(Dictionary<NodeType, int> d, NodeType t)
        {
            d.TryGetValue(t, out int c);
            d[t] = Math.Max(0, c - 1);
        }
    }
}
