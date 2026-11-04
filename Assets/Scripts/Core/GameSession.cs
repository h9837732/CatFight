using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatFight.Core
{
    /// <summary>
    /// 跨场景的单局上下文。挂在 DontDestroyOnLoad 上，场景切换时保留。
    /// 只存"当前这一局的 RunState"和"战斗入口参数"，不存战斗中间态。
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public RunState Run { get; private set; }
        public SaveService.Settings Settings { get; private set; }

        /// <summary>进入战斗场景时携带的遭遇配置。</summary>
        public BattleRequest PendingBattle { get; private set; }

        [Serializable]
        public class BattleRequest
        {
            public string encounterId;      // 遭遇表 id
            public int sourceNodeId;        // 触发它的地图节点
            public NodeKind kind;           // Battle / Elite / Boss
            public int streak;              // 连续战斗计数，用于难度微调
        }

        public enum NodeKind { Battle, Elite, Boss, Event, Shop, Rest, Treasure }

        public static event Action OnRunChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Settings = SaveService.LoadSettings();
            Application.targetFrameRate = 60;

            // 音频总控
            if (AudioDirector.Instance == null)
            {
                gameObject.AddComponent<AudioDirector>();
            }
        }

        // ------------------------------------------------------------------
        // Run 生命周期
        // ------------------------------------------------------------------

        public void StartNewRun(int? seed = null)
        {
            int s = seed ?? Environment.TickCount & 0x7FFFFFFF;
            Run = RunState.NewRun(s);
            SaveService.SaveRun(Run);
            NotifyRunChanged();
            Debug.Log($"[Session] 新局开始 seed={s}");
        }

        public bool ContinueRun()
        {
            var loaded = SaveService.LoadRun();
            if (loaded == null) return false;
            Run = loaded;
            NotifyRunChanged();
            Debug.Log($"[Session] 继续 seed={Run.seed} 已走 {Run.visitedPath.Count} 步 hp={Run.hp}/{Run.maxHp}");
            return true;
        }

        public void EndRun(bool victory)
        {
            Run = null;
            SaveService.DeleteRun();
            NotifyRunChanged();
        }

        public void SetRun(RunState run)
        {
            Run = run;
            SaveService.SaveRun(Run);
            NotifyRunChanged();
        }

        public void NotifyRunChanged() => OnRunChanged?.Invoke();

        // ------------------------------------------------------------------
        // 场景流转
        // ------------------------------------------------------------------

        public const string SceneMainMenu = "MainMenu";
        public const string SceneMap = "Map";
        public const string SceneBattle = "Battle";

        public void GoMainMenu() => SceneManager.LoadScene(SceneMainMenu);

        public void GoMap()
        {
            PendingBattle = null;
            SceneManager.LoadScene(SceneMap);
        }

        public void GoBattle(BattleRequest request)
        {
            PendingBattle = request;
            SceneManager.LoadScene(SceneBattle);
        }

        public void ClearPendingBattle() => PendingBattle = null;

        /// <summary>战斗结束回调：把战斗结果写回 Run，然后回地图。</summary>
        public void FinishBattle(int hpAfter, int goldGained, bool victory)
        {
            if (Run != null)
            {
                Run.hp = Mathf.Clamp(hpAfter, 0, Run.maxHp);
                Run.gold += goldGained;
                if (victory)
                {
                    // 走到当前节点，记入已走路径
                    if (PendingBattle != null && PendingBattle.sourceNodeId >= 0)
                    {
                        if (!Run.visitedPath.Contains(PendingBattle.sourceNodeId))
                            Run.visitedPath.Add(PendingBattle.sourceNodeId);
                    }
                    SaveService.SaveRun(Run);
                }
                NotifyRunChanged();
            }
            PendingBattle = null;
        }
    }
}
