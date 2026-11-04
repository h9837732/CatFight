using UnityEngine;

namespace CatFight.Battle
{
    /// <summary>
    /// 敌人站位计算。与 UI 布局解耦，专门负责"根据背景图的地平线把敌人摆在地面上"。
    ///
    /// 设计依据：战斗背景是一张街道照片（路面从画面中部向远方收缩）。
    /// 因此敌人不能按 UI 网格摆放，必须让**脚底**落在路面的透视带里，
    /// 否则会出现"敌人在天上飘"的问题。
    ///
    /// 用法：
    ///   var slots = EnemyLayout.Compute(count);
    ///   slots[i] → 归一化坐标（0..1，原点在左下角）与像素尺寸
    /// </summary>
    public static class EnemyLayout
    {
        // ------------------------------------------------------------------
        // 可调参数（对应街道背景）
        // ------------------------------------------------------------------

        /// <summary>地面基线（归一化，0 = 屏幕底边）。敌人脚底落在这条线上。</summary>
        public const float GroundLineY = 0.315f;

        /// <summary>多个敌人时的错落幅度：越靠边越靠后（屏幕上更高一些）。</summary>
        public const float DepthStagger = 0.022f;

        /// <summary>敌人之间的最小水平间隔（归一化）。</summary>
        public const float MinSpacing = 0.16f;

        /// <summary>整组敌人的水平中心（归一化）。略偏右，把左侧让给玩家立绘。</summary>
        public const float GroupCenterX = 0.545f;

        /// <summary>敌人立绘的基准像素尺寸（按 1920×1080 基准分辨率）。</summary>
        public const float BaseWidth = 210f;
        public const float BaseHeight = 250f;

        /// <summary>敌人数量越多，单体越小（避免挤在一起）。</summary>
        private static readonly float[] ScaleByCount = { 1.16f, 1.02f, 0.92f, 0.84f, 0.78f };

        // ------------------------------------------------------------------

        public struct Slot
        {
            /// <summary>脚底锚点的归一化位置（0..1，原点左下）。</summary>
            public Vector2 groundAnchor;
            /// <summary>立绘像素尺寸。</summary>
            public Vector2 size;
            /// <summary>所在行的相对深度（0 = 最前，越大越靠后）。</summary>
            public int depth;
        }

        /// <summary>计算 n 个敌人的站位。</summary>
        public static Slot[] Compute(int count)
        {
            count = Mathf.Max(0, count);
            var slots = new Slot[count];
            if (count == 0) return slots;

            float scale = ScaleByCount[Mathf.Clamp(count - 1, 0, ScaleByCount.Length - 1)];
            Vector2 size = new Vector2(BaseWidth * scale, BaseHeight * scale);

            // 间距：优先 MinSpacing，但保证整组不超出画面
            const float MaxSpan = 0.78f;
            float spacing = count <= 1 ? 0f : Mathf.Min(MinSpacing + 0.02f, MaxSpan / (count - 1));
            float totalSpan = spacing * (count - 1);

            // 从右到左排列（索引 0 在最右，与地图节点顺序一致）
            float startX = GroupCenterX + totalSpan * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = startX - i * spacing;

                // 立体感：越靠中间的敌人略微靠前，两侧稍靠后
                float t = count <= 1 ? 0f : (i - (count - 1) * 0.5f) / Mathf.Max(1f, (count - 1) * 0.5f);
                int depth = Mathf.Abs(t) > 0.5f ? 1 : 0;

                float y = GroundLineY - depth * DepthStagger;

                slots[i] = new Slot
                {
                    groundAnchor = new Vector2(x, y),
                    size = size,
                    depth = depth
                };
            }
            return slots;
        }
    }
}
