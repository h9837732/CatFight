using System.Collections.Generic;
using CatFight.Map;
using NUnit.Framework;

namespace CatFight.Tests
{
    /// <summary>
    /// 地图生成器的 EditMode 测试。对应设计文档 §13 验收用例 M-01 ~ M-07。
    /// 全部为纯逻辑测试，不依赖场景、不进入 Play 模式。
    /// </summary>
    public class MapGeneratorTests
    {
        private MapConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = MapConfig.CreateDefault();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
            _config = null;
        }

        // ---------------------------------------------------------------
        // M-01：大量随机种子下全部通过校验
        // ---------------------------------------------------------------

        [Test]
        public void Generate_1000Seeds_AllPassValidation()
        {
            const int seedCount = 1000;
            int failures = 0;
            string firstError = null;
            int firstBadSeed = -1;

            for (int seed = 1; seed <= seedCount; seed++)
            {
                MapData map;
                try
                {
                    map = MapGenerator.Generate(seed, _config);
                }
                catch (System.Exception ex)
                {
                    failures++;
                    if (firstError == null) { firstError = ex.Message; firstBadSeed = seed; }
                    continue;
                }

                if (!MapValidator.Validate(map, _config, out string error))
                {
                    failures++;
                    if (firstError == null) { firstError = error; firstBadSeed = seed; }
                }
            }

            Assert.AreEqual(0, failures,
                $"{failures}/{seedCount} 个种子生成失败。首个失败 seed={firstBadSeed}：{firstError}");
        }

        // ---------------------------------------------------------------
        // M-02：无边交叉
        // ---------------------------------------------------------------

        [Test]
        public void Generate_EdgesNeverCross()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(seed, _config);
                Assert.IsTrue(MapValidator.ValidateNoCrossing(map, out string error),
                    $"seed {seed} 出现交叉边：{error}");
            }
        }

        // ---------------------------------------------------------------
        // M-03 / M-04：行宽与汇聚约束
        // ---------------------------------------------------------------

        [Test]
        public void Generate_RowWidthsWithinLimits_AndAnchorsAreSingle()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(seed, _config);

                Assert.AreEqual(_config.rowCount, map.rowCount, $"seed {seed} 行数不符");

                for (int r = 0; r < map.rowCount; r++)
                {
                    int count = map.RowCount(r);
                    Assert.GreaterOrEqual(count, 1, $"seed {seed} 第 {r} 行为空");
                    Assert.LessOrEqual(count, _config.maxNodesPerRow,
                        $"seed {seed} 第 {r} 行 {count} 个节点超上限");
                }

                for (int r = 0; r + 1 < map.rowCount; r++)
                {
                    int delta = System.Math.Abs(map.RowCount(r + 1) - map.RowCount(r));
                    Assert.LessOrEqual(delta, 1,
                        $"seed {seed} 第 {r}→{r + 1} 行宽度变化 {delta}");
                }

                foreach (int anchor in _config.convergeRows)
                    Assert.AreEqual(1, map.RowCount(anchor), $"seed {seed} 汇聚行 {anchor} 不是 1 个节点");
            }
        }

        // ---------------------------------------------------------------
        // M-05：连通性（无孤儿、无死路）
        // ---------------------------------------------------------------

        [Test]
        public void Generate_NoOrphansAndNoDeadEnds()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(seed, _config);
                int lastRow = map.rowCount - 1;

                for (int i = 0; i < map.nodes.Count; i++)
                {
                    var node = map.nodes[i];
                    if (node.row < lastRow)
                        Assert.GreaterOrEqual(node.next.Count, 1, $"seed {seed} 节点 {node.id} 无出边（死路）");
                    if (node.row > 0)
                        Assert.GreaterOrEqual(node.prev.Count, 1, $"seed {seed} 节点 {node.id} 无入边（孤儿）");
                    Assert.LessOrEqual(node.next.Count, _config.maxOutDegree,
                        $"seed {seed} 节点 {node.id} 出度 {node.next.Count} 超过上限 {_config.maxOutDegree}");
                }
            }
        }

        // ---------------------------------------------------------------
        // M-06：每条路径至少一个休息点
        // ---------------------------------------------------------------

        [Test]
        public void Generate_EveryPathHasAtLeastOneRest()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(seed, _config);
                var paths = map.EnumeratePaths();

                Assert.Greater(paths.Count, 0, $"seed {seed} 没有任何路径");

                foreach (var path in paths)
                {
                    bool hasRest = false;
                    foreach (int id in path)
                        if (map.GetNode(id).type == NodeType.Rest) { hasRest = true; break; }

                    Assert.IsTrue(hasRest,
                        $"seed {seed} 路径 [{string.Join(",", path)}] 上没有休息点");
                }
            }
        }

        // ---------------------------------------------------------------
        // M-07：同种子必须完全一致（确定性）
        // ---------------------------------------------------------------

        [Test]
        public void Generate_SameSeed_ProducesIdenticalMap()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var a = MapGenerator.Generate(seed, _config);
                var b = MapGenerator.Generate(seed, _config);

                Assert.AreEqual(a.seed, b.seed, $"seed {seed} 实际使用种子不同");
                Assert.AreEqual(a.nodes.Count, b.nodes.Count, $"seed {seed} 节点数不同");
                Assert.AreEqual(a.edges.Count, b.edges.Count, $"seed {seed} 边数不同");

                for (int i = 0; i < a.nodes.Count; i++)
                {
                    var na = a.nodes[i];
                    var nb = b.nodes[i];
                    Assert.AreEqual(na.id, nb.id);
                    Assert.AreEqual(na.type, nb.type, $"seed {seed} 节点 {na.id} 类型不同");
                    Assert.AreEqual(na.pos.x, nb.pos.x, 1e-5f, $"seed {seed} 节点 {na.id} x 不同");
                    Assert.AreEqual(na.pos.y, nb.pos.y, 1e-5f, $"seed {seed} 节点 {na.id} y 不同");
                    CollectionAssert.AreEqual(na.next, nb.next, $"seed {seed} 节点 {na.id} 出边不同");
                }
            }
        }

        // ---------------------------------------------------------------
        // 不同种子应产出不同地图（否则"随机"是假的）
        // ---------------------------------------------------------------

        [Test]
        public void Generate_DifferentSeeds_ProduceDifferentMaps()
        {
            var signatures = new HashSet<string>();

            for (int seed = 1; seed <= 50; seed++)
            {
                var map = MapGenerator.Generate(seed, _config);
                var sb = new System.Text.StringBuilder();
                sb.Append(map.seed).Append('|');
                for (int r = 0; r < map.rowCount; r++) sb.Append(map.RowCount(r)).Append(',');
                for (int i = 0; i < map.nodes.Count; i++)
                    sb.Append((int)map.nodes[i].type).Append('.');
                signatures.Add(sb.ToString());
            }

            // 拓扑 + 类型组合应该足够多样；允许少量重复
            Assert.GreaterOrEqual(signatures.Count, 30,
                $"50 个种子只产生了 {signatures.Count} 种不同地图，随机性不足");
        }

        // ---------------------------------------------------------------
        // 类型可见性：所有节点必须有一个确定的、非默认兜底的类型
        // ---------------------------------------------------------------

        [Test]
        public void Generate_AllNodeTypesAreExplicitAndVisible()
        {
            var map = MapGenerator.Generate(20260101, _config);

            for (int i = 0; i < map.nodes.Count; i++)
            {
                var node = map.nodes[i];
                Assert.IsTrue(System.Enum.IsDefined(typeof(NodeType), node.type),
                    $"节点 {node.id} 类型非法");
            }

            // 关键类型必须真的出现过（否则玩家看不到商店/休息点图标）
            var counts = new Dictionary<NodeType, int>();
            for (int i = 0; i < map.nodes.Count; i++)
            {
                counts.TryGetValue(map.nodes[i].type, out int c);
                counts[map.nodes[i].type] = c + 1;
            }

            Assert.IsTrue(counts.ContainsKey(NodeType.Rest), "地图上没有休息点");
            Assert.IsTrue(counts.ContainsKey(NodeType.Shop), "地图上没有商店");
            Assert.IsTrue(counts.ContainsKey(NodeType.Boss), "地图上没有 BOSS");
            Assert.IsTrue(counts.ContainsKey(NodeType.Battle), "地图上没有普通战斗");
        }

        // ---------------------------------------------------------------
        // 起点与开局分支（地图自下而上）
        // ---------------------------------------------------------------

        [Test]
        public void Generate_StartNodeIsEmptyAndFirstRowBranches()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = MapGenerator.Generate(seed, _config);

                // 起点必须是空节点
                Assert.AreEqual(1, map.RowCount(0), $"seed {seed} 起点应有且仅有 1 个节点");
                var start = map.GetNode(map.StartId);
                Assert.AreEqual(NodeType.Start, start.type, $"seed {seed} 起点类型应为 Start");

                // 起点上方第一行必须是两条路
                Assert.AreEqual(2, map.RowCount(1), $"seed {seed} 第一行分支数应为 2");

                // 起点必须连到第一行两个节点
                Assert.AreEqual(2, start.next.Count, $"seed {seed} 起点应连出 2 条边");

                // 坐标：行号越大 y 越大（自下而上）
                for (int r = 0; r + 1 < map.rowCount; r++)
                {
                    var a = map.GetNode(map.NodeIdAt(r, 0));
                    var b = map.GetNode(map.NodeIdAt(r + 1, 0));
                    Assert.Greater(b.pos.y, a.pos.y,
                        $"seed {seed} 第 {r + 1} 行应位于第 {r} 行上方（y 递增）");
                }
            }
        }

        // ---------------------------------------------------------------
        // 配置矛盾必须被拦住，而不是留到运行时随机失败
        // ---------------------------------------------------------------

        [Test]
        public void Generate_InvalidConfig_ThrowsWithClearMessage()
        {
            var bad = MapConfig.CreateDefault();
            try
            {
                bad.convergeRows = new[] { 0, 6 };     // 0 非法
                Assert.IsFalse(bad.TryValidate(out string err));
                Assert.IsNotNull(err);

                var ex = Assert.Throws<System.InvalidOperationException>(
                    () => MapGenerator.Generate(1, bad));
                StringAssert.Contains("MapConfig", ex.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bad);
            }
        }
    }
}
