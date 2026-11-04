using UnityEngine;

namespace CatFight.Core
{
    /// <summary>
    /// 音频管理：战斗开场音 → 循环战斗曲，音量由设置实时控制。
    /// 一个场景只保留一个实例，切场景自动淡出。
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        private AudioSource _music;      // 循环曲
        private AudioSource _stinger;    // 开场音（不循环）
        private float _masterVolume = 0.8f;
        private float _fadeTarget = 1f;
        private float _fadeSpeed = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.volume = 0f;

            _stinger = gameObject.AddComponent<AudioSource>();
            _stinger.loop = false;
            _stinger.playOnAwake = false;

            EnsureAudioListener();
            ApplySettings();

            // 切场景后如果新场景没有 AudioListener，补给新的（避免后续静音）
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            EnsureAudioListener();
            RefreshVolumes();
        }

        /// <summary>
        /// 如果没有 AudioListener，音频根本不会出声（典型情况：相机是代码创建的，
        /// 忘了加 AudioListener）。这里做一道兜底，避免“静音但找不到原因”。
        /// </summary>
        private static void EnsureAudioListener()
        {
            if (Object.FindFirstObjectByType<AudioListener>() != null) return;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.gameObject.AddComponent<AudioListener>();
                Debug.Log("[Audio] 相机缺少 AudioListener，已自动补上");
            }
            else
            {
                var go = new GameObject("AudioListener");
                go.AddComponent<AudioListener>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[Audio] 场景内无相机也无 AudioListener，已创建独立 AudioListener");
            }
        }

        /// <summary>从存档设置同步主音量。</summary>
        public void ApplySettings()
        {
            var s = GameSession.Instance != null ? GameSession.Instance.Settings : null;
            if (s != null) _masterVolume = Mathf.Clamp01(s.masterVolume);
            RefreshVolumes();
        }

        public void SetMasterVolume(float v)
        {
            _masterVolume = Mathf.Clamp01(v);
            RefreshVolumes();

            var session = GameSession.Instance;
            if (session != null)
            {
                session.Settings.masterVolume = _masterVolume;
                SaveService.SaveSettings(session.Settings);
            }
        }

        public float MasterVolume => _masterVolume;

        private void RefreshVolumes()
        {
            if (_music != null) _music.volume = _masterVolume * (muted ? 0f : 0.70f);
            if (_stinger != null) _stinger.volume = _masterVolume * (muted ? 0f : 0.90f);
        }

        private bool muted;

        // ------------------------------------------------------------------
        // 战斗音乐
        // ------------------------------------------------------------------

        /// <summary>进入战斗：开场音当前奏（只取开头几秒），随即交叉淡入循环曲。</summary>
        public void PlayBattle()
        {
            if (_music == null) return;

            _music.clip = UI.Assets2.BattleLoop;
            _music.volume = 0f;
            if (_music.clip != null) _music.Play();

            var start = UI.Assets2.BattleStart;
            if (start != null)
            {
                _stinger.Stop();
                _stinger.clip = start;
                _stinger.volume = _masterVolume * (muted ? 0f : 0.90f);
                _stinger.Play();

                // 开场音只做前奏：IntroSeconds 后开始推循环曲，再 StopIntroSeconds 关掉开场音
                CancelInvoke(nameof(RaiseMusic));
                CancelInvoke(nameof(StopIntro));

                float intro = Mathf.Min(IntroSeconds, Mathf.Max(0.5f, start.length * 0.5f));
                Invoke(nameof(RaiseMusic), intro);
                Invoke(nameof(StopIntro), intro + IntroSeconds + 1.5f);
            }
            else
            {
                RaiseMusic();
            }
        }

        /// <summary>开场音当作前奏播放的秒数，之后交叉到循环曲。</summary>
        private const float IntroSeconds = 4f;

        private void StopIntro()
        {
            if (_stinger != null) _stinger.Stop();
        }

        private void RaiseMusic()
        {
            if (_music != null) _music.volume = _masterVolume * (muted ? 0f : 0.70f);
            _fadeTarget = 1f;
        }

        /// <summary>离开战斗：淡出。</summary>
        public void StopBattle(float fadeSeconds = 0.5f)
        {
            CancelInvoke(nameof(RaiseMusic));
            CancelInvoke(nameof(StopIntro));
            if (_stinger != null) _stinger.Stop();
            if (_music != null) _music.Stop();
        }

        public void StopAll() => StopBattle(0f);

        public bool IsMuted => muted;

        public void SetMuted(bool value)
        {
            muted = value;
            RefreshVolumes();
        }
    }
}
