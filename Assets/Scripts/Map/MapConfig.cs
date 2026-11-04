using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatFight.Map
{
    /// <summary>某一行区间允许的节点类型及权重。</summary>
    [Serializable]
    public class NodeTypeRule
    {
        public int rowStart;
        public int rowEnd;              // 含
        public NodeType[] allowed = new NodeType[0];
        public float[] weights = new float[0];

        public bool Covers(int row) => row >= rowStart && row <= rowEnd;
    }

    /// <summary>全局数量上限，防止生成出"全是精英"的地图。</summary>
    [Serializable]
    public class TypeQuotas
    {
        public int eliteMax = 3;
        public int shopMax = 2;
        public int treasureMax = 1;
        public int restMax = 4;

        public int GetMax(NodeType t)
        {
            switch (t)
            {
                case NodeType.Elite: return eliteMax;
                case NodeType.Shop: return shopMax;
                case NodeType.Treasure: return treasureMax;
                case NodeType.Rest: return restMax;
                default: return int.MaxValue;
            }
        }
    }

    /// <summary>
    /// 地图生成的全部可调参数。见设计文档 §12。
    /// 运行时可调，但同一 seed + 同一配置必须产出同一张图。
    /// </summary>
    [CreateAssetMenu(fileName = "MapConfig", menuName = "CatFight/Map/Map Config")]
    public class MapConfig : ScriptableObject
    {
        [Header("结构")]
        public int rowCount = 14;
        public int maxNodesPerRow = 4;
        public int[] convergeRows = { 6, 12 };
        public float[] rowWidthWeights = { 0.15f, 0.35f, 0.32f, 0.18f };

        [Header("几何")]
        public float rowSpacing = 160f;
        public float mapWidth = 720f;
        [Range(0f, 0.45f)] public float jitterRatio = 0.18f;

        [Header("连边")]
        [Range(0f, 1f)] public float twoEdgeChance = 0.45f;
        public int maxOutDegree = 2;
        public int maxGenerationAttempts = 20;

        [Header("节点类型")]
        public List<NodeTypeRule> rowRules = new List<NodeTypeRule>();
        public TypeQuotas quotas = new TypeQuotas();

        /// <summary>构造一套默认参数（供测试与首次创建使用，不依赖资源文件）。</summary>
        public static MapConfig CreateDefault()
        {
            var cfg = CreateInstance<MapConfig>();
            cfg.rowRules = new List<NodeTypeRule>
            {
                Rule(0, 0, new[] { NodeType.Battle }, new[] { 1f }),
                Rule(1, 1, new[] { NodeType.Battle, NodeType.Event }, new[] { 0.75f, 0.25f }),
                Rule(2, 3, new[] { NodeType.Battle, NodeType.Event, NodeType.Elite }, new[] { 0.55f, 0.30f, 0.15f }),
                Rule(4, 4, new[] { NodeType.Battle, NodeType.Event, NodeType.Shop, NodeType.Rest },
                            new[] { 0.40f, 0.25f, 0.20f, 0.15f }),
                Rule(5, 5, new[] { NodeType.Battle }, new[] { 1f }),
                Rule(6, 6, new[] { NodeType.Rest, NodeType.Treasure }, new[] { 0.75f, 0.25f }),
                Rule(7, 8, new[] { NodeType.Battle, NodeType.Event, NodeType.Elite }, new[] { 0.50f, 0.30f, 0.20f }),
                Rule(9, 9, new[] { NodeType.Shop, NodeType.Event }, new[] { 0.60f, 0.40f }),
                Rule(10, 11, new[] { NodeType.Battle, NodeType.Elite }, new[] { 0.75f, 0.25f }),
                Rule(12, 12, new[] { NodeType.Rest }, new[] { 1f }),
                Rule(13, 13, new[] { NodeType.Battle }, new[] { 1f }),
            };
            return cfg;
        }

        private static NodeTypeRule Rule(int a, int b, NodeType[] allowed, float[] weights)
            => new NodeTypeRule { rowStart = a, rowEnd = b, allowed = allowed, weights = weights };

        public NodeTypeRule GetRule(int row)
        {
            if (rowRules == null) return null;
            for (int i = 0; i < rowRules.Count; i++)
                if (rowRules[i] != null && rowRules[i].Covers(row)) return rowRules[i];
            return null;
        }

        public bool IsTypeAllowedAt(int row, NodeType type)
        {
            var rule = GetRule(row);
            if (rule == null || rule.allowed == null) return false;
            for (int i = 0; i < rule.allowed.Length; i++)
                if (rule.allowed[i] == type) return true;
            return false;
        }

        /// <summary>配置自检：任何矛盾都应在这里被拦住，而不是留到生成时随机失败。</summary>
        public bool TryValidate(out string error)
        {
            error = null;
            if (rowCount < 4) { error = "rowCount 必须 ≥ 4"; return false; }
            if (maxNodesPerRow < 1 || maxNodesPerRow > 9) { error = "maxNodesPerRow 必须在 1..9"; return false; }
            if (maxOutDegree < 1 || maxOutDegree > 3) { error = "maxOutDegree 必须在 1..3"; return false; }
            if (rowWidthWeights == null || rowWidthWeights.Length < maxNodesPerRow)
            {
                error = "rowWidthWeights 长度必须 ≥ maxNodesPerRow";
                return false;
            }
            if (convergeRows != null)
            {
                for (int i = 0; i < convergeRows.Length; i++)
                {
                    if (convergeRows[i] <= 0 || convergeRows[i] >= rowCount - 1)
                    {
                        error = $"convergeRows[{i}]={convergeRows[i]} 必须落在 1..{rowCount - 2}";
                        return false;
                    }
                }
            }
            for (int r = 0; r < rowCount; r++)
            {
                var rule = GetRule(r);
                if (rule == null) { error = $"第 {r} 行没有类型规则"; return false; }
                if (rule.allowed == null || rule.allowed.Length == 0) { error = $"第 {r} 行允许类型为空"; return false; }
                if (rule.weights != null && rule.weights.Length != rule.allowed.Length)
                {
                    error = $"第 {r} 行 weights 与 allowed 长度不一致";
                    return false;
                }
            }
            return true;
        }
    }
}
