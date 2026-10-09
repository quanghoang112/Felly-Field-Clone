using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JellyField.Core;
using JellyField.Gameplay;

namespace JellyField.UI
{
    public class GameHud : MonoBehaviour
    {
        [SerializeField]
        private JellyGame game;
        [SerializeField]
        private RectTransform safe;
        [SerializeField]
        private TextMeshProUGUI levelLabel, resultTitle;
        [SerializeField]
        private TextMeshProUGUI[] goalLabels;
        [SerializeField]
        private Image[] goalIcons;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("collectionRoot")]
        private RectTransform collectRoot;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("collectionIcons")]
        private Image[] collectIcons = Array.Empty<Image>();
        [SerializeField]
        private GameObject result;
        [SerializeField]
        private Button nextButton;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("restartCampaignButton")]
        private Button restartButton;
        private Material[] palette;
        private Rect lastSafe;
        private Vector2 lastSize;
        private readonly int[] goalColors = new int[2];
        private readonly int[] shownGoals = new int[2];
        private int effectVersion;
        public void Initialize(Material[] materials)
        {
            palette = materials;
            HideResult();
            UpdateSafeArea();
        }

        public void Refresh(GameSession game, int level)
        {
            levelLabel.text = "LEVEL " + (level + 1);
            int index = 0;
            for (int c = 1; c <= 5; c++)
                if (game.Level.Goals[c] > 0)
                {
                    goalLabels[index].transform.parent.gameObject.SetActive(true);
                    goalIcons[index].color = palette[c].GetColor("_BaseColor");
                    goalColors[index] = c;
                    shownGoals[index] = game.Remaining[c];
                    goalLabels[index].text = game.Remaining[c].ToString();
                    index++;
                }

            for (int i = index; i < 2; i++)
            {
                goalColors[i] = 0;
                goalLabels[i].transform.parent.gameObject.SetActive(false);
            }

            // Keep a single objective visually centered.
            var first = (RectTransform)goalLabels[0].transform.parent;
            first.anchorMin = new Vector2(index == 1 ? .26f : 0, 0);
            first.anchorMax = new Vector2(index == 1 ? .74f : .48f, 1);
        }

        public async UniTask CollectColor(JellyColor color, Vector3 origin, Camera camera, CancellationToken token)
        {
            int goal = Array.IndexOf(goalColors, (int)color);
            if (goal < 0 || shownGoals[goal] <= 0)
                return;
            Image icon = Array.Find(collectIcons, item => !item.gameObject.activeSelf);
            if (icon == null)
                return;
            int version = effectVersion;
            var rect = icon.rectTransform;
            var canvas = collectRoot.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(collectRoot, camera.WorldToScreenPoint(origin), uiCamera, out var start);
            rect.localPosition = new Vector3(start.x, start.y, 0);
            rect.localScale = Vector3.one;
            icon.color = goalIcons[goal].color;
            icon.gameObject.SetActive(true);
            Vector3 target = collectRoot.InverseTransformPoint(goalIcons[goal].transform.position);
            try
            {
                float duration = .45f + Array.IndexOf(collectIcons, icon) * .025f;
                await rect.DOLocalJump(target, 110, 1, duration).SetEase(Ease.InQuad).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                token.ThrowIfCancellationRequested();
                if (version != effectVersion)
                    return;
                if (shownGoals[goal] > 0)
                {
                    shownGoals[goal]--;
                    goalLabels[goal].text = shownGoals[goal].ToString();
                    var number = goalLabels[goal].transform;
                    number.DOKill();
                    number.localScale = Vector3.one;
                    _ = number.DOPunchScale(Vector3.one * .3f, .24f, 1, .4f);
                }
            }
            finally
            {
                if (version == effectVersion)
                    icon.gameObject.SetActive(false);
            }
        }

        public void CancelCollection()
        {
            effectVersion++;
            foreach (var icon in collectIcons)
            {
                icon.transform.DOKill();
                icon.gameObject.SetActive(false);
            }

            foreach (var label in goalLabels)
            {
                label.transform.DOKill();
                label.transform.localScale = Vector3.one;
            }
        }

        private void OnEnable()
        {
            game.Won += OnWon;
            game.Lost += OnLost;
        }

        private void OnDisable()
        {
            game.Won -= OnWon;
            game.Lost -= OnLost;
            CancelCollection();
        }

        private void OnWon()
        {
            ShowResult(true, game.IsLastLevel);
        }

        private void OnLost()
        {
            ShowResult(false, false);
        }

        public void HideResult()
        {
            result.SetActive(false);
        }

        private void ShowResult(bool won, bool last)
        {
            result.SetActive(true);
            resultTitle.text = won ? "LEVEL COMPLETE" : "NO SPACE LEFT";
            nextButton.gameObject.SetActive(won && !last);
            restartButton.gameObject.SetActive(won && last);
        }

        private void Update()
        {
            if (lastSafe != Screen.safeArea || lastSize != new Vector2(Screen.width, Screen.height))
                UpdateSafeArea();
        }

        private void UpdateSafeArea()
        {
            lastSafe = Screen.safeArea;
            lastSize = new Vector2(Screen.width, Screen.height);
            safe.anchorMin = lastSafe.min / lastSize;
            safe.anchorMax = lastSafe.max / lastSize;
        }
    }
}
