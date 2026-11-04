using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CatFight.Battle
{
    /// <summary>
    /// 手牌单张卡的位移动画。
    ///
    /// 为什么单独一个组件：
    /// 手牌需要同时处理「发牌入场」「选中抬起」「悬停膨胀」三种位移来源，
    /// 如果各自用协程写，会互相打架（比如发牌途中鼠标划过）。
    /// 这里统一成「每帧向目标值收敛」，任何一方只需改 target，动画自然接管。
    ///
    /// 悬停判定不在这里做：手牌叠压时「谁在最上面」由 BattleScreen 的
    /// 兄弟顺序决定，用统一的手动射线判定比逐张 IPointerEnter 更可靠。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HandCardMotion : MonoBehaviour
    {
        /// <summary>目标锚点位置（相对手牌行）。</summary>
        public Vector2 targetPos;
        /// <summary>目标缩放。</summary>
        public float targetScale = 1f;

        /// <summary>收敛速度，越大越快。发牌用较小的值做出滑动感。</summary>
        public float speed = 14f;

        /// <summary>手牌下标（用于调试与去重）。</summary>
        public int handIndex;

        private RectTransform _rt;
        private bool _animating;      // 发牌入场阶段：强制按缓动曲线插值
        private Vector2 _animFrom;
        private float _animT;
        private float _animDur;
        private float _animDelay;

        private void Awake()
        {
            _rt = (RectTransform)transform;
        }

        /// <summary>发牌入场：延迟 delay 秒后，从 from 滑到 targetPos。</summary>
        public void PlayEnter(Vector2 from, float duration, float delay = 0f)
        {
            _rt.anchoredPosition = from;
            _rt.localScale = Vector3.one * (targetScale * 0.82f);
            _animFrom = from;
            _animT = 0f;
            _animDur = Mathf.Max(0.01f, duration);
            _animDelay = Mathf.Max(0f, delay);
            _animating = true;
        }

        /// <summary>跳过动画直接到位（例如重建手牌后）。</summary>
        public void Snap()
        {
            _animating = false;
            _rt.anchoredPosition = targetPos;
            _rt.localScale = Vector3.one * targetScale;
        }

        /// <summary>从指定位置开始（用于「出牌后其余牌平顺重排」）。</summary>
        public void StartFrom(Vector2 pos)
        {
            _animating = false;
            _rt.anchoredPosition = pos;
            _rt.localScale = Vector3.one * targetScale;
        }

        private void Update()
        {
            if (_animating)
            {
                if (_animDelay > 0f)
                {
                    _animDelay -= Time.deltaTime;
                    return;                      // 还没轮到它：原地藏着
                }

                _animT += Time.deltaTime;
                float k = Mathf.Clamp01(_animT / _animDur);
                // ease-out cubic：起手快、收尾稳
                float e = 1f - Mathf.Pow(1f - k, 3f);
                _rt.anchoredPosition = Vector2.LerpUnclamped(_animFrom, targetPos, e);
                _rt.localScale = Vector3.one * Mathf.Lerp(targetScale * 0.82f, targetScale, e);
                if (k >= 1f) _animating = false;
                return;
            }

            // 指数收敛：与帧率无关，且不会有弹簧过冲
            float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
            _rt.anchoredPosition = Vector2.Lerp(_rt.anchoredPosition, targetPos, t);
            float s = Mathf.Lerp(_rt.localScale.x, targetScale, t);
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// 通用悬停提示：鼠标进入时回调显示、离开时隐藏。
    /// 给遗物格、道具格这种「小图标 + 需要说明」的东西用。
    /// </summary>
    public class HoverTipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action onEnter;
        public Action onExit;

        public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke();
    }
}
