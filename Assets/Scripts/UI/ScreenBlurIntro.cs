using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CatFight.UI
{
    /// <summary>
    /// 入场过渡：全屏“先模糊后清晰”。
    ///
    /// 做法：用 ScreenCapture.CaptureScreenshotIntoRenderTexture 取一张当前画面的
    /// 低分辨率快照（低分辨率 + 双线性放大本身就是模糊），再叠一层卷积模糊，
    /// 然后把 alpha 从 1 淡到 0，露出下面的实时画面。
    ///
    /// 之所以不用 GrabPass / 后处理栈：ScreenSpaceOverlay 的 UI 直接画在 backbuffer 上，
    /// GrabPass 在 overlay 阶段不可靠；截图方案是确定能用的。
    /// </summary>
    public class ScreenBlurIntro : MonoBehaviour
    {
        [Tooltip("整体时长（秒）")]
        public float duration = 1.5f;

        /// <summary>调试用：大于 0 时覆盖 duration，方便截图验证动画中间帧。</summary>
        public static float DebugDurationOverride = 0f;

        [Tooltip("开始时额外叠一层黑（0 = 不叠黑）")]
        public float startBlackAlpha = 0.5f;

        [Tooltip("快照降采样倍数，越大越糊")]
        public int downsample = 10;

        [Tooltip("截图前先等多少帧，让布局稳定（这段时间由黑层遮住）")]
        public int settleFrames = 5;

        [Tooltip("初始模糊强度（像素级）")]
        public float startBlurSize = 5f;

        private GameObject _root;
        private RawImage _blurImage;
        private Image _black;
        private RenderTexture _rt;
        private Material _mat;

        /// <summary>开始播放。parent 一般是目标 Canvas 的根，遮罩会作为最上层子物体。</summary>
        public void Play(Transform parent)
        {
            BuildUi(parent);
            if (DebugDurationOverride > 0f) duration = DebugDurationOverride;
            StartCoroutine(Run());
        }

        private void BuildUi(Transform parent)
        {
            _root = new GameObject("IntroBlurOverlay", typeof(RectTransform));
            var rt = _root.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            UiKit.Stretch(rt);
            rt.SetAsLastSibling();

            // 黑底（先盖住，避免露出未准备好的画面）
            var blackGo = new GameObject("Black", typeof(RectTransform));
            var brt = blackGo.GetComponent<RectTransform>();
            brt.SetParent(rt, false);
            UiKit.Stretch(brt);
            _black = blackGo.AddComponent<Image>();
            _black.color = new Color(0f, 0f, 0f, startBlackAlpha);
            _black.raycastTarget = false;

            // 模糊层
            var blurGo = new GameObject("Blur", typeof(RectTransform));
            var brrt = blurGo.GetComponent<RectTransform>();
            brrt.SetParent(rt, false);
            UiKit.Stretch(brrt);
            _blurImage = blurGo.AddComponent<RawImage>();
            _blurImage.color = Color.white;
            _blurImage.raycastTarget = false;

            var sh = Shader.Find("CatFight/UIBlur");
            if (sh != null)
            {
                _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                _mat.SetFloat("_BlurSize", startBlurSize);
                _blurImage.material = _mat;
            }

            // 截图前先隐藏自己，否则会把黑层与空白层一起拍进去
            _root.SetActive(false);
        }

        private IEnumerator Run()
        {
            // 先等几帧让布局与首次渲染稳定，再截图（这段时间由黑层遮住，看不出来）
            for (int i = 0; i < Mathf.Max(1, settleFrames); i++) yield return null;
            yield return new WaitForEndOfFrame();

            CaptureInto(_blurImage);

            _root.SetActive(true);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);

                // 消除黑层稍快，模糊收敛稍慢 → “先淡黑、再逐渐看清”
                float blackK = Mathf.Clamp01(k * 3f);
                float blurK = 1f - Mathf.Pow(1f - k, 2.2f);

                if (_black != null)
                    _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(startBlackAlpha, 0f, blackK));

                if (_blurImage != null)
                    _blurImage.color = new Color(1f, 1f, 1f, 1f - blurK);

                if (_mat != null)
                    _mat.SetFloat("_BlurSize", Mathf.Lerp(startBlurSize, 0f, blurK));

                yield return null;
            }

            Cleanup();
        }

        /// <summary>
        /// 把当前屏幕截成一张低分辨率纹理作为模糊源。
        ///
        /// 做法分两步，都是为了绕开已知坑：
        ///   1. Texture2D.ReadPixels 在 WaitForEndOfFrame 之后可靠地读到 backbuffer；
        ///      （Graphics.Blit(null, rt) 的 null 源会用默认白纹理，截出来是一片白）
        ///   2. 再把大图 Blit 到一张 1/N 的小 RT —— 下采样本身就是模糊，双线性放大后就是柔焦效果。
        /// </summary>
        private void CaptureInto(RawImage target)
        {
            int sw = Screen.width;
            int sh = Screen.height;
            if (sw <= 0 || sh <= 0) return;

            int w = Mathf.Max(16, sw / Mathf.Max(1, downsample));
            int h = Mathf.Max(16, sh / Mathf.Max(1, downsample));

            // 1) 读全屏
            var full = new Texture2D(sw, sh, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, sw, sh), 0, 0);
            full.Apply(false, false);

            // 2) 下采样到小 RT（双线性）
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            _rt.Create();
            Graphics.Blit(full, _rt);

            Destroy(full);

            if (target != null) target.texture = _rt;
        }

        private void Cleanup()
        {
            if (_root != null) Destroy(_root);
            _root = null;

            if (_blurImage != null) _blurImage.texture = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_mat != null) { Destroy(_mat); _mat = null; }

            Destroy(this);
        }
    }
}
