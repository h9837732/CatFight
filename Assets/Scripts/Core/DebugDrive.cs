using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CatFight.Core
{
    /// <summary>
    /// 开发期调试驱动：不依赖鼠标事件，直接调用 UI 按钮的 onClick。
    /// 用于在无人工操作的情况下验证「主菜单 → 地图 → 战斗」整条链路。
    /// 只做读取与转发，不含任何玩法逻辑，发行版建议剔除或保留在 Dev 构建。
    /// </summary>
    public static class DebugDrive
    {
        /// <summary>按名字点击按钮（会先找精确名，再找前缀匹配的第一个可交互按钮）。</summary>
        public static string Click(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var btn = go.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.Invoke();
                    return $"clicked {name}";
                }
                return $"{name} 没有 Button 组件";
            }

            // 前缀匹配（如 Node_）
            Button[] all = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name.StartsWith(name) && all[i].interactable)
                {
                    string n = all[i].gameObject.name;
                    all[i].onClick.Invoke();
                    return $"clicked {n} (prefix)";
                }
            }
            return $"找不到按钮：{name}";
        }

        /// <summary>列出当前所有可交互按钮，便于确认该点哪个。</summary>
        public static string ListButtons()
        {
            var sb = new StringBuilder();
            Button[] all = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
            var seen = new HashSet<string>();
            foreach (var b in all)
            {
                if (!b.interactable) continue;
                if (!seen.Add(b.gameObject.name)) continue;
                sb.Append(b.gameObject.name).Append(' ');
            }
            return sb.Length == 0 ? "(没有可交互按钮)" : sb.ToString();
        }

        /// <summary>当前场景与界面概况。</summary>
        public static string Info()
        {
            var sb = new StringBuilder();
            sb.Append("scene=").Append(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            sb.Append(" frame=").Append(Time.frameCount);

            var session = GameSession.Instance;
            if (session != null && session.Run != null)
            {
                var r = session.Run;
                sb.Append($" | hp={r.hp}/{r.maxHp} gold={r.gold} deck={r.deck.Count} path={r.visitedPath.Count}");
            }
            else
            {
                sb.Append(" | run=null");
            }
            sb.Append(" | buttons=").Append(ListButtons());
            return sb.ToString();
        }

        /// <summary>读取某个 Text 的内容（校验数值展示是否正确）。</summary>
        public static string TextOf(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go == null) return "(未找到 " + objectName + ")";
            var t = go.GetComponent<Text>();
            return t != null ? t.text : "(无 Text 组件)";
        }
    }
}
