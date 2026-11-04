using System;
using System.Collections.Generic;

namespace CatFight.Map
{
    /// <summary>
    /// 地图不变量校验器。所有"生成器应该保证的事"都在这里断言一遍。
    /// 开发期常开（生成耗时 &lt; 1ms，代价可忽略），Release 可关。
    /// </summary>
    public static class MapValidator
    {
        // ------------------------------------------------------------------
        // 结构
        // ------------------------------------------------------------------

        public static bool ValidateStructure(MapData map, MapConfig cfg, out string error)
        {
            if (map == null) { error = "map 为 null"; return false; }
            if (cfg == null) { error = "config 为 null"; return false; }

            if (map.nodes.Count == 0) { error = "地图没有任何节点"; return false; }

            // 1. 行索引完整性
            for (int r = 0; r < map.rowCount; r++)
            {
                int count = map.RowCount(r);
                if (count < 1) { error = $"第 {r} 行没有节点"; return false; }
                if (count > cfg.maxNodesPerRow)
                {
                    error = $"第 {r} 行有 {count} 个节点，超过上限 {cfg.maxNodesPerRow}";
                    return false;
                }
            }

            // 2. 相邻行宽度差 ≤ 1
            for (int r = 0; r + 1 < map.rowCount; r++)
            {
                int delta = Math.Abs(map.RowCount(r + 1) - map.RowCount(r));
                if (delta > 1)
                {
                    error = $"第 {r} 行({map.RowCount(r)}) 到第 {r + 1} 行({map.RowCount(r + 1)}) 宽度变化 {delta}，超过 1";
                    return false;
                }
            }

            // 3. 汇聚行必须只有 1 个节点
            if (cfg.convergeRows != null)
            {
                for (int i = 0; i < cfg.convergeRows.Length; i++)
                {
                    int r = cfg.convergeRows[i];
                    if (r < 0 || r >= map.rowCount) continue;
                    if (map.RowCount(r) != 1)
                    {
                        error = $"汇聚行 {r} 有 {map.RowCount(r)} 个节点（应为 1）";
                        return false;
                    }
                }
            }

            // 4. 出入度
            for (int i = 0; i < map.nodes.Count; i++)
            {
                var node = map.nodes[i];
                if (node.next == null) node.next = new List<int>();
                if (node.prev == null) node.prev = new List<int>();

                bool isFirstRow = node.row == 0;
                bool isLastRow = node.row == map.rowCount - 1;

                if (!isLastRow && node.next.Count < 1)
                {
                    error = $"节点 {node.id}（第 {node.row} 行第 {node.index} 个）没有出边，玩家会卡死";
                    return false;
                }
                if (node.next.Count > cfg.maxOutDegree)
                {
                    error = $"节点 {node.id} 出度 {node.next.Count} 超过上限 {cfg.maxOutDegree}";
                    return false;
                }
                if (isLastRow && node.next.Count > 0)
                {
                    error = $"末行节点 {node.id} 不应有出边";
                    return false;
                }
                if (!isFirstRow && node.prev.Count < 1)
                {
                    error = $"节点 {node.id}（第 {node.row} 行第 {node.index} 个）没有入边，是孤儿节点";
                    return false;
                }
                if (isFirstRow && node.prev.Count > 0)
                {
                    error = $"起点节点 {node.id} 不应有入边";
                    return false;
                }
            }

            // 5. 边合法性：只连相邻行、无重复、无自环
            var seen = new HashSet<long>();
            for (int i = 0; i < map.edges.Count; i++)
            {
                var e = map.edges[i];
                if (e.from == e.to) { error = $"自环边 {e.from}"; return false; }

                var a = map.GetNode(e.from);
                var b = map.GetNode(e.to);
                if (a == null || b == null) { error = $"边 {e.from}->{e.to} 引用了不存在的节点"; return false; }
                if (b.row != a.row + 1)
                {
                    error = $"边 {e.from}->{e.to} 跨越了非相邻行（{a.row}->{b.row}）";
                    return false;
                }

                long key = ((long)e.from << 32) | (uint)e.to;
                if (!seen.Add(key)) { error = $"重复边 {e.from}->{e.to}"; return false; }
            }

            // 6. 起点可达所有节点
            var reachable = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(map.StartId);
            reachable.Add(map.StartId);
            while (queue.Count > 0)
            {
                var node = map.GetNode(queue.Dequeue());
                for (int i = 0; i < node.next.Count; i++)
                    if (reachable.Add(node.next[i])) queue.Enqueue(node.next[i]);
            }
            if (reachable.Count != map.nodes.Count)
            {
                error = $"从起点只能到达 {reachable.Count}/{map.nodes.Count} 个节点，存在不可达节点";
                return false;
            }

            // 7. 所有节点可达 BOSS
            var toBoss = new HashSet<int>();
            queue.Clear();
            queue.Enqueue(map.BossId);
            toBoss.Add(map.BossId);
            while (queue.Count > 0)
            {
                var node = map.GetNode(queue.Dequeue());
                for (int i = 0; i < node.prev.Count; i++)
                    if (toBoss.Add(node.prev[i])) queue.Enqueue(node.prev[i]);
            }
            if (toBoss.Count != map.nodes.Count)
            {
                error = $"只有 {toBoss.Count}/{map.nodes.Count} 个节点能到达 BOSS，存在死路";
                return false;
            }

            // 8. 无边交叉
            if (!ValidateNoCrossing(map, out error)) return false;

            error = null;
            return true;
        }

        /// <summary>
        /// 边交叉检测：对同一对相邻行内的任意两条边 (s1→t1)、(s2→t2)，
        /// 若 s1 &lt; s2 且 t1 &gt; t2，则两条墨线必然交叉。
        /// </summary>
        public static bool ValidateNoCrossing(MapData map, out string error)
        {
            for (int r = 0; r + 1 < map.rowCount; r++)
            {
                var cur = map.Rows[r];
                var pairs = new List<KeyValuePair<int, int>>();   // (srcIndex, tgtIndex)

                for (int s = 0; s < cur.Count; s++)
                {
                    var node = map.GetNode(cur[s]);
                    for (int k = 0; k < node.next.Count; k++)
                        pairs.Add(new KeyValuePair<int, int>(s, node.next[k] % 100));
                }

                for (int a = 0; a < pairs.Count; a++)
                {
                    for (int b = a + 1; b < pairs.Count; b++)
                    {
                        int s1 = pairs[a].Key, t1 = pairs[a].Value;
                        int s2 = pairs[b].Key, t2 = pairs[b].Value;
                        bool crossed = (s1 < s2 && t1 > t2) || (s1 > s2 && t1 < t2);
                        if (crossed)
                        {
                            error = $"第 {r}→{r + 1} 行存在交叉边：({s1}→{t1}) 与 ({s2}→{t2})";
                            return false;
                        }
                    }
                }
            }
            error = null;
            return true;
        }

        // ------------------------------------------------------------------
        // 类型
        // ------------------------------------------------------------------

        public static bool ValidateTypes(MapData map, MapConfig cfg, out string error)
        {
            if (map == null) { error = "map 为 null"; return false; }
            if (cfg == null) { error = "config 为 null"; return false; }

            int lastRow = map.rowCount - 1;

            // 0. 起点必须是 Start（空节点）
            var startIds = map.Rows[0];
            for (int i = 0; i < startIds.Count; i++)
            {
                if (map.GetNode(startIds[i]).type != NodeType.Start)
                {
                    error = $"起点节点 {startIds[i]} 类型不是 Start";
                    return false;
                }
            }

            // 1. 末行必须是 Boss
            var lastIds = map.Rows[lastRow];
            for (int i = 0; i < lastIds.Count; i++)
            {
                if (map.GetNode(lastIds[i]).type != NodeType.Boss)
                {
                    error = $"末行节点 {lastIds[i]} 不是 BOSS";
                    return false;
                }
            }

            // 2. 每行类型必须在该行允许集内
            for (int r = 0; r < lastRow; r++)
            {
                var ids = map.Rows[r];
                for (int i = 0; i < ids.Count; i++)
                {
                    var node = map.GetNode(ids[i]);
                    if (!cfg.IsTypeAllowedAt(r, node.type))
                    {
                        error = $"第 {r} 行节点 {node.id} 的类型 {node.type} 不在允许集内";
                        return false;
                    }
                }
            }

            // 3. 配额
            var counts = new Dictionary<NodeType, int>();
            for (int i = 0; i < map.nodes.Count; i++)
            {
                var t = map.nodes[i].type;
                counts.TryGetValue(t, out int c);
                counts[t] = c + 1;
            }
            foreach (var kv in counts)
            {
                int max = cfg.quotas.GetMax(kv.Key);
                if (kv.Value > max)
                {
                    error = $"类型 {kv.Key} 出现 {kv.Value} 次，超过配额 {max}";
                    return false;
                }
            }

            // 4. 每条路径至少一个休息点
            var paths = map.EnumeratePaths(5000);
            if (paths.Count == 0) { error = "地图没有任何可用路径"; return false; }

            for (int p = 0; p < paths.Count; p++)
            {
                bool hasRest = false;
                var path = paths[p];
                for (int i = 0; i < path.Count; i++)
                {
                    var node = map.GetNode(path[i]);
                    if (node != null && node.type == NodeType.Rest) { hasRest = true; break; }
                }
                if (!hasRest)
                {
                    error = $"路径 [{string.Join(",", path)}] 上没有任何休息点";
                    return false;
                }
            }

            error = null;
            return true;
        }

        public static bool Validate(MapData map, MapConfig cfg, out string error)
            => ValidateStructure(map, cfg, out error) && ValidateTypes(map, cfg, out error);
    }
}
