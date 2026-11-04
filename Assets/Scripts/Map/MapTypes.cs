using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatFight.Map
{
    /// <summary>节点类型。全部对玩家可见（见设计文档 §7.7）。</summary>
    public enum NodeType
    {
        Battle = 0,
        Elite = 1,
        Event = 2,
        Shop = 3,
        Rest = 4,
        Treasure = 5,
        Boss = 6,
        /// <summary>起点：空节点，不显示图标，玩家初始所在位置。</summary>
        Start = 7
    }

    /// <summary>地图上的一个关卡节点。</summary>
    [Serializable]
    public class MapNode
    {
        public int id;          // = row * 100 + index
        public int row;
        public int index;
        public NodeType type;
        public Vector2 pos;     // 地图局部坐标（y 向上递增：第 0 行在最下方）
        public List<int> next = new List<int>();
        public List<int> prev = new List<int>();

        public int Row => id / 100;
        public int Index => id % 100;
    }

    /// <summary>连接两个节点的边。单向（行号递增）。</summary>
    [Serializable]
    public class MapEdge
    {
        public int from;
        public int to;
        public bool crossedOut;     // 未选分支被墨迹划掉

        public MapEdge() { }
        public MapEdge(int from, int to) { this.from = from; this.to = to; }
    }

    /// <summary>整张地图的不可变数据。</summary>
    [Serializable]
    public class MapData
    {
        public int seed;
        public int generatorVersion;
        public int rowCount;
        public List<MapNode> nodes = new List<MapNode>();
        public List<MapEdge> edges = new List<MapEdge>();

        [NonSerialized] private List<int>[] _rows;
        [NonSerialized] private Dictionary<int, int> _idToIndex;

        /// <summary>每行的节点 id 列表（索引由 BuildIndex 重建）。</summary>
        public List<int>[] Rows => _rows;

        public void BuildIndex()
        {
            _rows = new List<int>[rowCount];
            for (int r = 0; r < rowCount; r++) _rows[r] = new List<int>(4);
            _idToIndex = new Dictionary<int, int>(nodes.Count);

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                _idToIndex[n.id] = i;
                if (n.row >= 0 && n.row < rowCount) _rows[n.row].Add(n.id);
            }
            for (int r = 0; r < rowCount; r++) _rows[r].Sort();
        }

        private void EnsureIndex()
        {
            if (_rows == null || _idToIndex == null || _rows.Length != rowCount) BuildIndex();
        }

        public int RowCount(int row) { EnsureIndex(); return _rows[row].Count; }

        public int NodeIdAt(int row, int index) { EnsureIndex(); return _rows[row][index]; }

        public MapNode GetNode(int id)
        {
            EnsureIndex();
            return _idToIndex.TryGetValue(id, out int i) ? nodes[i] : null;
        }

        public int StartId { get { EnsureIndex(); return _rows[0][0]; } }
        public int BossId { get { EnsureIndex(); return _rows[rowCount - 1][_rows[rowCount - 1].Count - 1]; } }

        public void AddEdge(int from, int to)
        {
            var a = GetNode(from);
            var b = GetNode(to);
            if (a == null || b == null) throw new ArgumentException($"AddEdge: unknown node {from} -> {to}");
            if (a.next.Contains(to)) return;               // 去重
            a.next.Add(to);
            b.prev.Add(from);
            edges.Add(new MapEdge(from, to));
        }

        /// <summary>起点到终点的全部路径（DFS，带数量上限防爆）。</summary>
        public List<List<int>> EnumeratePaths(int maxPaths = 20000)
        {
            EnsureIndex();
            var result = new List<List<int>>();
            var stack = new List<int>();
            Dfs(StartId, result, stack, maxPaths);
            return result;
        }

        private void Dfs(int id, List<List<int>> acc, List<int> stack, int maxPaths)
        {
            if (acc.Count >= maxPaths) return;
            stack.Add(id);
            var node = GetNode(id);
            if (node.next.Count == 0)
            {
                acc.Add(new List<int>(stack));
            }
            else
            {
                for (int i = 0; i < node.next.Count; i++) Dfs(node.next[i], acc, stack, maxPaths);
            }
            stack.RemoveAt(stack.Count - 1);
        }
    }

    /// <summary>存档内容：只存种子 + 已走路径（见设计文档 §10）。</summary>
    [Serializable]
    public class MapState
    {
        public int seed;
        public int generatorVersion = MapGenerator.CurrentVersion;
        public List<int> visitedPath = new List<int>();

        public int CurrentId => visitedPath.Count > 0 ? visitedPath[visitedPath.Count - 1] : -1;
    }
}
