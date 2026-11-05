using System.Collections.Generic;
using UnityEngine;

namespace CatFight.UI
{
    /// <summary>
    /// 素材登记处。所有正式素材走 Resources 加载，缺失时自动退回程序生成的占位图，
    /// 所以任何一张图没放进来都不会导致界面报错或空白。
    /// </summary>
    public static class Assets2
    {
        // --- 缓存 ---
        private static Sprite _cardBg;
        private static Sprite _cardArtAttack, _cardArtSkill, _cardArtDefend;
        private static Sprite _battleBg;
        private static Sprite _player;
        private static Sprite[] _enemies;
        private static Sprite _nodeCombat, _nodeElite;
        private static AudioClip _battleStart, _battleLoop;
        private static bool _audioLoaded;
        private static Material _whiteKeyMat;

        private static Sprite Load(string path)
        {
            var s = Resources.Load<Sprite>(path);
            if (s == null) Debug.LogWarning($"[Assets2] 缺少素材：Resources/{path}");
            return s;
        }

        /// <summary>
        /// 白底抠图材质。素材（猫图/立绘/卡图）带白背，直接贴在照片或彩色背景上会露出白块，
        /// 用这个材质把接近白色的部分变透明。找不到 shader 时回退 null（即默认材质）。
        /// </summary>
        public static Material WhiteKeyMaterial
        {
            get
            {
                if (_whiteKeyMat != null) return _whiteKeyMat;

                var sh = Shader.Find("CatFight/WhiteKeyUI");
                if (sh == null)
                {
                    Debug.LogWarning("[Assets2] 未找到 CatFight/WhiteKeyUI 着色器，素材白底不会被抠除");
                    return null;
                }
                _whiteKeyMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                return _whiteKeyMat;
            }
        }

        /// <summary>给 Image 应用白底抠图（带空引用保护）。</summary>
        public static void ApplyWhiteKey(UnityEngine.UI.Image img, float threshold = 0.82f, float softness = 0.14f)
        {
            if (img == null) return;
            var mat = WhiteKeyMaterial;
            if (mat == null) return;

            var inst = new Material(mat) { hideFlags = HideFlags.HideAndDontSave };
            inst.SetFloat("_Threshold", threshold);
            inst.SetFloat("_Softness", softness);
            img.material = inst;
        }

        // ------------------------------------------------------------------
        // 卡面
        // ------------------------------------------------------------------

        /// <summary>卡面底纹（猫毛纹理）。</summary>
        public static Sprite CardBg => _cardBg != null ? _cardBg : (_cardBg = Load("Art/Cards/card_bg"));

        /// <summary>
        /// 卡面主图。三级降级：
        /// card_art_&lt;id&gt;（专属图）→ card_art_&lt;art&gt;（卡牌指定的四张底图之一）→ card_art_&lt;type&gt;（类型退路）。
        ///
        /// 为什么要 art 这一层：素材只有 4 张猫脸，光按类型分的话同类型卡牌会全部撞脸。
        /// 在 art 里按语义挑一张，同类型内部就能拉开区别，而且不用新素材。
        /// </summary>
        public static Sprite CardArt(string cardId, string art, Battle.CardType type)
        {
            if (!string.IsNullOrEmpty(cardId))
            {
                var byId = LoadCardArt("Art/Cards/card_art_" + cardId);
                if (byId != null) return byId;
            }
            if (!string.IsNullOrEmpty(art))
            {
                var byArt = LoadCardArt("Art/Cards/card_art_" + art);
                if (byArt != null) return byArt;
            }
            return CardArt(type);
        }

        /// <summary>卡图缓存（按资源路径）。避免每次重建手牌都 Resources.Load 一遍。</summary>
        private static readonly Dictionary<string, Sprite> _cardArtCache = new Dictionary<string, Sprite>();

        private static Sprite LoadCardArt(string path)
        {
            if (_cardArtCache.TryGetValue(path, out var hit)) return hit;
            var s = Load(path);
            _cardArtCache[path] = s;          // null 也缓存，避免反复重试
            return s;
        }

        /// <summary>卡面主图：攻击 = 张嘴猫，技能 = 技能猫，防御/能力 = 防御猫。</summary>
        public static Sprite CardArt(Battle.CardType type)
        {
            switch (type)
            {
                case Battle.CardType.Attack:
                    if (_cardArtAttack == null) _cardArtAttack = Load("Art/Cards/card_art_attack");
                    return _cardArtAttack;
                case Battle.CardType.Skill:
                    if (_cardArtSkill == null) _cardArtSkill = Load("Art/Cards/card_art_skill");
                    return _cardArtSkill;
                default:
                    if (_cardArtDefend == null) _cardArtDefend = Load("Art/Cards/card_art_defend");
                    return _cardArtDefend;
            }
        }

        // ------------------------------------------------------------------
        // 场景
        // ------------------------------------------------------------------

        public static Sprite BattleBg => _battleBg != null ? _battleBg : (_battleBg = Load("Art/Bg/battle_bg"));

        public static Sprite PlayerPortrait => _player != null ? _player : (_player = Load("Art/player_portrait"));

        /// <summary>按索引取敌人立绘（0..3 循环）。</summary>
        public static Sprite Enemy(int index)
        {
            if (_enemies == null || _enemies.Length == 0)
            {
                _enemies = new Sprite[4];
                for (int i = 0; i < 4; i++)
                    _enemies[i] = Resources.Load<Sprite>($"Art/Enemies/enemy_{i + 1}");
            }
            if (_enemies.Length == 0) return null;
            return _enemies[Mathf.Abs(index) % _enemies.Length];
        }

        // ------------------------------------------------------------------
        // 地图节点
        // ------------------------------------------------------------------

        public static Sprite NodeCombat => _nodeCombat != null ? _nodeCombat : (_nodeCombat = Load("Art/MapNodes/node_combat"));
        public static Sprite NodeElite => _nodeElite != null ? _nodeElite : (_nodeElite = Load("Art/MapNodes/node_elite"));

        /// <summary>该节点类型是否有专属立绘。</summary>
        public static Sprite NodeSprite(Map.NodeType t)
        {
            switch (t)
            {
                case Map.NodeType.Battle: return NodeCombat;
                case Map.NodeType.Elite: return NodeElite;
                default: return null;
            }
        }

        // ------------------------------------------------------------------
        // 音频
        // ------------------------------------------------------------------

        public static AudioClip BattleStart
        {
            get
            {
                if (!_audioLoaded) LoadAudio();
                return _battleStart;
            }
        }

        public static AudioClip BattleLoop
        {
            get
            {
                if (!_audioLoaded) LoadAudio();
                return _battleLoop;
            }
        }

        private static void LoadAudio()
        {
            _audioLoaded = true;
            _battleStart = Resources.Load<AudioClip>("Audio/battle_start");
            _battleLoop = Resources.Load<AudioClip>("Audio/battle_loop");
        }

        public static void ClearCache()
        {
            _cardBg = null; _cardArtAttack = null; _cardArtSkill = null; _cardArtDefend = null;
            _battleBg = null; _player = null; _enemies = null;
            _nodeCombat = null; _nodeElite = null;
            _battleStart = null; _battleLoop = null; _audioLoaded = false;
            _whiteKeyMat = null;
            UiKit.InvalidatePortrait();
        }
    }
}
