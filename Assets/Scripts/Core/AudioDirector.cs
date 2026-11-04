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

            ApplySettings();
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
            if (_music != null) _music.volume = _masterVolume * (muted ? 0f : 0.55f);
            if (_stinger != null) _stinger.volume = _masterVolume * (muted ? 0f : 0.9f);
        }

        private bool muted;

        // ------------------------------------------------------------------
        // 战斗音乐
        // ------------------------------------------------------------------

        /// <summary>进入战斗：播开场音，然后无缝接循环曲。</summary>
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
                _stinger.volume = _masterVolume * (muted ? 0f : 0.9f);
                _stinger.Play();

                // 开场音播完后再把循环曲音量推上来，避免重叠
                CancelInvoke(nameof(RaiseMusic));
                Invoke(nameof(RaiseMusic), Mathf.Max(0.1f, start.length - 0.6f));
            }
            else
            {
                RaiseMusic();
            }
        }

        private void RaiseMusic()
        {
            if (_music != null) _music.volume = _masterVolume * (muted ? 0f : 0.55f);
            _fadeTarget = 1f;
        }

        /// <summary>离开战斗：淡出。</summary>
        public void StopBattle(float fadeSeconds = 0.5f)
        {
            CancelInvoke(nameof(RaiseMusic));
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
