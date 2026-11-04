import io

p = r'F:\CatFight_Unity\CatFight\Assets\Scripts\Battle\BattleScreen.cs'
s = io.open(p, encoding='utf-8').read()

start = s.index('        private void BuildPlayerPanel()')
end = s.index('        private void BuildEnemyArea()')

new = '''        /// <summary>
        /// 玩家区：立绘（图片）→ 血条 → 行动点数，自上而下。
        /// 不使用任何底板 / 状态框，直接绘制在场景背景上。
        /// </summary>
        private void BuildPlayerPanel()
        {
            var root = UiKit.CreateRect(_root, "PlayerArea");
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(48f, 44f);
            root.sizeDelta = new Vector2(340f, 500f);

            const float W = 300f;

            // --- 立绘（图片，不是文字）---
            var portrait = UiKit.CreateImage(root, "PlayerPortrait", Color.white, UiKit.Portrait);
            portrait.preserveAspect = true;
            var prt = portrait.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 176f);
            prt.sizeDelta = new Vector2(W, W);

            // 格挡徽章（叠在立绘右上角）
            var blockCircle = UiKit.CreateImage(root, "BlockCircle", Theme.BlockBlue, UiKit.Circle);
            var brt = blockCircle.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(W * 0.5f - 12f, 176f + W - 86f);
            brt.sizeDelta = new Vector2(82f, 82f);

            _playerBlockText = UiKit.CreateText(blockCircle.transform, "Val", "0", Theme.FontHead, Color.white);
            UiKit.Stretch(_playerBlockText.rectTransform);

            var blockCap = UiKit.CreateText(root, "BlockCap", "格挡", Theme.FontTiny, Theme.InkFaint);
            var bct = blockCap.rectTransform;
            bct.anchorMin = bct.anchorMax = new Vector2(0.5f, 0f);
            bct.pivot = new Vector2(0.5f, 1f);
            bct.anchoredPosition = new Vector2(W * 0.5f - 12f, 176f + W - 90f);
            bct.sizeDelta = new Vector2(82f, 22f);

            // --- 血条（立绘下面）---
            var hbrt = UiKit.CreateRect(root, "HpBarRoot");
            hbrt.anchorMin = hbrt.anchorMax = new Vector2(0.5f, 0f);
            hbrt.pivot = new Vector2(0.5f, 0f);
            hbrt.anchoredPosition = new Vector2(0f, 148f);
            hbrt.sizeDelta = new Vector2(W, 34f);

            _playerHpFill = UiKit.CreateProgressBar(hbrt, "Hp", new Vector2(W, 34f),
                Theme.HpBack, Theme.HpRed, segments: 8);
            var barRoot = _playerHpFill.transform.parent.GetComponent<RectTransform>();
            UiKit.Stretch(barRoot);

            _playerHpText = UiKit.CreateText(hbrt, "HpText", "80/80", Theme.FontSmall, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Stretch(_playerHpText.rectTransform);

            // --- 行动点数（血条下面）---
            var energyCap = UiKit.CreateText(root, "EnergyCap", "行动点", Theme.FontTiny, Theme.InkFaint,
                TextAnchor.MiddleLeft);
            var ecrt = energyCap.rectTransform;
            ecrt.anchorMin = ecrt.anchorMax = new Vector2(0.5f, 0f);
            ecrt.pivot = new Vector2(0f, 0f);
            ecrt.anchoredPosition = new Vector2(-W * 0.5f, 118f);
            ecrt.sizeDelta = new Vector2(120f, 24f);

            _energyText = UiKit.CreateText(root, "EnergyVal", "3/3", Theme.FontTiny, Theme.InkFaint,
                TextAnchor.MiddleRight);
            var evrt = _energyText.rectTransform;
            evrt.anchorMin = evrt.anchorMax = new Vector2(0.5f, 0f);
            evrt.pivot = new Vector2(1f, 0f);
            evrt.anchoredPosition = new Vector2(W * 0.5f, 118f);
            evrt.sizeDelta = new Vector2(120f, 24f);

            _energyRow = UiKit.CreateRect(root, "EnergyRow");
            _energyRow.anchorMin = _energyRow.anchorMax = new Vector2(0.5f, 0f);
            _energyRow.pivot = new Vector2(0f, 0f);
            _energyRow.anchoredPosition = new Vector2(-W * 0.5f, 52f);
            _energyRow.sizeDelta = new Vector2(W, 56f);

            // --- 状态（最下面一行，不要底板）---
            _playerStatusText = UiKit.CreateText(root, "Statuses", "", Theme.FontTiny,
                Theme.Gold, TextAnchor.MiddleLeft);
            var srt = _playerStatusText.rectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
            srt.pivot = new Vector2(0f, 1f);
            srt.anchoredPosition = new Vector2(-W * 0.5f, 44f);
            srt.sizeDelta = new Vector2(W, 40f);
            _playerStatusText.lineSpacing = 1.15f;
        }

'''

s = s[:start] + new + s[end:]
io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('BuildPlayerPanel replaced')
