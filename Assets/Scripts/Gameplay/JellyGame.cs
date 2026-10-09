using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using JellyField.Core;
using JellyField.Data;
using JellyField.Rendering;
using JellyField.UI;

namespace JellyField.Gameplay
{
    public class JellyGame : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action Won;
        public event Action Lost;
        public bool IsLastLevel => levelIndex == levels.Length - 1;

        private const int HighlightMs = 280;
        private const int ClearMs = 380;
        [Header("Objects already placed in the scene")]
        [SerializeField]
        private Camera gameCamera;
        [SerializeField]
        private GameHud hud;
        [SerializeField]
        private JellySlotView[] boardSlots;
        [SerializeField]
        private JellySlotView[] traySlots;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("placementHighlight")]
        private Transform cellHighlight;
        [SerializeField]
        private AudioSource audioSource;
        [Header("Drag feedback")]
        [SerializeField, Range(0, .15f)]
        [Tooltip("Distance above the pointer, as a fraction of screen height.")]
        [UnityEngine.Serialization.FormerlySerializedAs("dragPointerOffset")]
        private float dragOffset = .06f;
        [SerializeField]
        private AudioClip pickupSound;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("returnToTraySound")]
        private AudioClip returnSound;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("placeOnBoardSound")]
        private AudioClip placeSound;
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("vibrationEnabled")]
        private bool vibrate = true;
        [Header("Level and colors")]
        [SerializeField]
        private Campaign campaign;
        [SerializeField, Min(0)]
        [UnityEngine.Serialization.FormerlySerializedAs("startingLevel")]
        private int startLevel;
        [SerializeField]
        private Material[] jellyMaterials;
        private GameSession session;
        private LevelDefinition[] levels;
        private int levelIndex;
        private int selected = -1;
        private bool busy;
        private bool ended;
        private readonly Queue<int> pendingMoves = new Queue<int>();
        private readonly HashSet<int> pendingCells = new HashSet<int>();
        private ClearWave clearingWave;
        private bool dragging;
        private bool leftTray;
        private int previewCell = -1;
        private int pointerId;
        private Vector3 lastDragPoint;
        private Rect lastSafeArea;
        private Vector2 lastScreenSize;
        private int lastBoardSize;
        private float trayZ = -3.8f;
        private float traySpacing = 1.5f;
        private CancellationTokenSource levelCts;
        private void Start()
        {
            Application.targetFrameRate = 60;
            levels = campaign.Create();
            foreach (var level in levels)
                if (level.Mask.Length > boardSlots.Length || level.TraySize > traySlots.Length)
                    throw new InvalidOperationException($"Level '{level.Name}' exceeds the board or tray slots saved in this scene.");

            hud.Initialize(jellyMaterials);
            foreach (var slot in boardSlots)
                slot.Jelly.SetPalette(jellyMaterials);
            foreach (var slot in traySlots)
                slot.Jelly.SetPalette(jellyMaterials);
            LoadLevel(Mathf.Clamp(startLevel, 0, levels.Length - 1));
        }

        private void LoadLevel(int index)
        {
            CancelLevel();
            pendingMoves.Clear();
            pendingCells.Clear();
            levelCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            busy = false;
            ended = false;
            clearingWave = null;
            dragging = false;
            leftTray = false;
            selected = -1;
            levelIndex = index;
            session = new GameSession(levels[index]);
            // Reuse the 36 board slots and 2 tray slots saved in the scene.
            for (int i = 0; i < boardSlots.Length; i++)
            {
                bool used = i < session.Board.Mask.Length && session.Board.Mask[i];
                boardSlots[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                boardSlots[i].transform.localPosition = CellPosition(i);
                boardSlots[i].Jelly.SetPiece(session.Board.Pieces[i], true);
            }

            for (int i = 0; i < traySlots.Length; i++)
            {
                traySlots[i].gameObject.SetActive(i < session.Tray.Length);
                if (i < session.Tray.Length)
                    RefreshTray(i);
            }

            cellHighlight.gameObject.SetActive(false);
            hud.HideResult();
            hud.Refresh(session, levelIndex);
            ResizeCamera();
        }

        private void CancelLevel()
        {
            ClearPreview();
            levelCts?.Cancel();
            hud.CancelCollection();
            levelCts?.Dispose();
            levelCts = null;
            foreach (var slot in boardSlots)
                slot.Jelly.StopAnimations();
            foreach (var slot in traySlots)
                slot.Jelly.StopAnimations();
        }

        private void OnDestroy()
        {
            CancelLevel();
        }

        public void Retry()
        {
            LoadLevel(levelIndex);
        }

        public void Next()
        {
            if (session.Won)
                LoadLevel((levelIndex + 1) % levels.Length);
        }

        private void RefreshTray(int slot)
        {
            traySlots[slot].transform.localPosition = TrayPosition(slot);
            traySlots[slot].Jelly.transform.localPosition = Vector3.zero;
            traySlots[slot].Jelly.SetPiece(session.Tray[slot], true);
        }

        private Vector3 CellPosition(int cell)
        {
            float x = (cell % session.Level.Width - (session.Level.Width - 1) * .5f) * 1.08f;
            float z = ((session.Level.Height - 1) * .5f - cell / session.Level.Width) * 1.08f + .4f;
            return new Vector3(x, 0, z);
        }

        private Vector3 TrayPosition(int slot)
        {
            return new Vector3((slot - (session.Tray.Length - 1) * .5f) * traySpacing, 0, trayZ);
        }

        private void ResizeCamera()
        {
            Rect safe = Screen.safeArea;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (safe.width <= 0 || safe.height <= 0)
                return;
            if (safe == lastSafeArea && screenSize == lastScreenSize && lastBoardSize == session.Level.Width)
                return;
            lastSafeArea = safe;
            lastScreenSize = screenSize;
            lastBoardSize = session.Level.Width;
            gameCamera.rect = new Rect(safe.x / Screen.width, safe.y / Screen.height, safe.width / Screen.width, safe.height / Screen.height);
            // Leave space around the board for the goals above and tray below.
            // Include room for the raised jelly and its wobble, not just the tiles.
            float halfBoard = (session.Level.Width - 1) * .54f + .75f;
            Vector3 up = gameCamera.transform.up;
            float halfHeight = Mathf.Abs(up.y) * .75f + Mathf.Abs(up.z) * halfBoard;
            gameCamera.orthographicSize = Mathf.Max(5.6f, halfBoard / (.76f * safe.width / safe.height), halfHeight / .38f);
            Vector3 center = new Vector3(0, .5f, .4f);
            float currentHeight = Vector3.Dot(center - gameCamera.transform.position, up);
            gameCamera.transform.position += up * (currentHeight + .05f * gameCamera.orthographicSize);
            traySpacing = gameCamera.orthographicSize * 2 * (safe.width / safe.height) * .4f;
            // Tray centers sit at 30% and 70% across the safe viewport.
            Ray ray = gameCamera.ViewportPointToRay(new Vector3(.5f, .145f, 0));
            Plane plane = new Plane(Vector3.up, new Vector3(0, .28f, 0));
            if (plane.Raycast(ray, out float distance))
                trayZ = ray.GetPoint(distance).z;
            for (int i = 0; i < traySlots.Length; i++)
                traySlots[i].transform.localPosition = TrayPosition(i);
            Physics.SyncTransforms();
        }

        private Vector3 PointerWorld(Vector2 screenPosition)
        {
            Ray ray = gameCamera.ScreenPointToRay(screenPosition);
            Plane plane = new Plane(Vector3.up, new Vector3(0, .28f, 0));
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        private int NearestCell(Vector3 point)
        {
            int x = Mathf.RoundToInt(point.x / 1.08f + (session.Level.Width - 1) * .5f);
            int y = Mathf.RoundToInt((session.Level.Height - 1) * .5f - (point.z - .4f) / 1.08f);
            if (x < 0 || x >= session.Level.Width || y < 0 || y >= session.Level.Height)
                return -1;
            int cell = y * session.Level.Width + x;
            return session.Board.CanPlace(cell) ? cell : -1;
        }

        private void Update()
        {
            if (session == null)
                return;
            ResizeCamera();
            if (selected >= 0 && (session.Won || (!busy && session.Lost)))
                CancelDrag();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragging ||
                !(data.pointerCurrentRaycast.module is PhysicsRaycaster) ||
                session == null || session.Won || (!busy && session.Lost))
                return;

            for (int i = 0; i < session.Tray.Length; i++)
            {
                if (data.pointerCurrentRaycast.gameObject != traySlots[i].gameObject || session.Tray[i] == null)
                    continue;
                CancelDrag();
                selected = i;
                pointerId = data.pointerId;
                dragging = true;
                lastDragPoint = PointerWorld(data.position + Vector2.up * (Screen.height * dragOffset));
                traySlots[i].Jelly.Kick(1.2f);
                return;
            }

            if (selected >= 0)
            {
                int cell = NearestCell(PointerWorld(data.position));
                if (cell >= 0)
                    Commit(selected, cell);
                else
                    CancelDrag();
            }
        }

        public void OnBeginDrag(PointerEventData data)
        {
            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != pointerId)
                return;
            if (session.Won || (!busy && session.Lost))
            {
                CancelDrag();
                return;
            }
            if (!leftTray && data.pointerCurrentRaycast.gameObject != traySlots[selected].gameObject)
            {
                leftTray = true;
                PlayFeedback(pickupSound);
            }

            Vector3 point = PointerWorld(data.position + Vector2.up * (Screen.height * dragOffset));
            traySlots[selected].Jelly.transform.position = new Vector3(point.x, .55f, point.z);
            traySlots[selected].Jelly.DragMotion(point - lastDragPoint);
            lastDragPoint = point;
            int cell = data.pointerCurrentRaycast.module is UnityEngine.UI.GraphicRaycaster ? -1 : NearestCell(point);
            ShowPreview(cell);
            cellHighlight.gameObject.SetActive(cell >= 0);
            if (cell >= 0)
                cellHighlight.localPosition = CellPosition(cell) + Vector3.up * .02f;
        }

        public void OnPointerUp(PointerEventData data)
        {
            // A tap keeps the piece selected; a drag is completed by OnEndDrag.
            if (dragging && data.pointerId == pointerId && !data.dragging)
                dragging = false;
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (!dragging || data.pointerId != pointerId)
                return;
            OnDrag(data);
            if (!dragging)
                return;
            traySlots[selected].Jelly.EndDragMotion();
            ClearPreview();
            int cell = NearestCell(lastDragPoint);
            bool overUi = data.pointerCurrentRaycast.module is UnityEngine.UI.GraphicRaycaster;
            if (!overUi && cell >= 0 && Commit(selected, cell))
                return;
            if (leftTray)
                PlayFeedback(returnSound);
            traySlots[selected].Jelly.Kick(.8f);
            CancelDrag();
        }

        private void CancelDrag()
        {
            ClearPreview();
            if (selected >= 0)
            {
                traySlots[selected].Jelly.EndDragMotion();
                traySlots[selected].Jelly.transform.localPosition = Vector3.zero;
            }

            selected = -1;
            dragging = false;
            leftTray = false;
            cellHighlight.gameObject.SetActive(false);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
                CancelDrag();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                CancelDrag();
        }

        private bool Commit(int slot, int cell)
        {
            if (!session.Place(slot, cell))
                return false;
            ClearPreview();
            selected = -1;
            dragging = false;
            leftTray = false;
            cellHighlight.gameObject.SetActive(false);
            traySlots[slot].Jelly.SetPiece(null, true);
            traySlots[slot].Jelly.transform.localPosition = Vector3.zero;
            boardSlots[cell].Jelly.SetPiece(session.Board.Pieces[cell], true);
            boardSlots[cell].Jelly.PlayPlacement();
            PlayFeedback(placeSound);
            // Refill now so another drag can start while the board animates.
            session.Refill(slot);
            RefreshTray(slot);
            pendingMoves.Enqueue(cell);
            pendingCells.Add(cell);
            if (!busy)
            {
                busy = true;
                ResolvePlacement(levelCts.Token).Forget();
            }

            return true;
        }

        private void ShowPreview(int cell)
        {
            if (cell == previewCell)
                return;
            ClearPreview();
            if (cell < 0 || selected < 0)
                return;
            previewCell = cell;
            var previewBoard = session.Board;
            if (clearingWave != null)
            {
                // Colors already disappearing must not promise another match.
                previewBoard = previewBoard.Clone();
                previewBoard.Apply(clearingWave);
            }

            var preview = previewBoard.PreviewPlacement(cell, session.Tray[selected], pendingCells);
            foreach (var pair in preview.Removed)
            {
                var view = pair.Key == cell ? traySlots[selected].Jelly : boardSlots[pair.Key].Jelly;
                view.SetPreviewColors(pair.Value);
            }
        }

        private void ClearPreview()
        {
            if (previewCell < 0)
                return;
            foreach (var slot in boardSlots)
                slot.Jelly.SetPreviewColors(null);
            foreach (var slot in traySlots)
                slot.Jelly.SetPreviewColors(null);
            previewCell = -1;
        }

        private async UniTask ResolvePlacement(CancellationToken token)
        {
            try
            {
                while (pendingMoves.Count > 0 && !session.Won)
                {
                    pendingCells.Remove(pendingMoves.Dequeue());
                    await UniTask.Delay(180, cancellationToken: token);
                    int chain = 0;
                    while (true)
                    {
                        ClearWave wave = session.Board.FindMatches(pendingCells);
                        if (wave.Total == 0)
                            break;
                        chain++;
                        ClearPreview();
                        clearingWave = wave;
                        foreach (var pair in wave.Removed)
                            boardSlots[pair.Key].Jelly.SetClearingColors(pair.Value);
                        await UniTask.Delay(HighlightMs, cancellationToken: token);
                        var collections = new List<UniTask>();
                        foreach (var pair in wave.Removed)
                            foreach (var color in pair.Value)
                                collections.Add(hud.CollectColor(color, boardSlots[pair.Key].Jelly.ColorPosition(color), gameCamera, token));
                        foreach (var pair in wave.Removed)
                            boardSlots[pair.Key].Jelly.ClearColors(pair.Value, ClearMs / 1000f);
                        PlayPop(1 + chain * .12f);
                        await UniTask.Delay(ClearMs, cancellationToken: token);
                        ClearPreview();
                        session.Board.Apply(wave);
                        clearingWave = null;
                        session.Score(wave);
                        foreach (var pair in wave.Removed)
                            boardSlots[pair.Key].Jelly.SetPiece(session.Board.Pieces[pair.Key]);
                        await UniTask.WhenAll(collections);
                        token.ThrowIfCancellationRequested();
                        hud.Refresh(session, levelIndex);
                        await UniTask.Delay(300, cancellationToken: token);
                    }
                }

                hud.Refresh(session, levelIndex);
                if (!ended && (session.Won || session.Lost))
                {
                    ended = true;
                    CancelDrag();
                    if (session.Won)
                        Won?.Invoke();
                    else
                        Lost?.Invoke();
                }
            }
            catch (OperationCanceledException)when (token.IsCancellationRequested)
            {
            }
            finally
            {
                // A cancelled turn must not unlock a newly loaded level's turn.
                if (!token.IsCancellationRequested)
                {
                    clearingWave = null;
                    pendingMoves.Clear();
                    pendingCells.Clear();
                    busy = false;
                }
            }
        }

        private void PlayPop(float pitch)
        {
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(audioSource.clip);
        }

        private void PlayFeedback(AudioClip clip)
        {
            audioSource.pitch = 1;
            audioSource.PlayOneShot(clip);

            if (!vibrate)
                return;
#if UNITY_ANDROID && !UNITY_EDITOR
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var window = currentActivity.Call<AndroidJavaObject>("getWindow");
                using var view = window.Call<AndroidJavaObject>("getDecorView");
                const int ClockTick = 4;
                view.Call<bool>("performHapticFeedback", ClockTick);
            }));
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
