using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using JellyField.Core;

namespace JellyField.Rendering
{
    public class JellyPieceView : MonoBehaviour
    {
        private const float WobbleDistance = .11f;
        private const float ExpandStrength = .9f;
        [SerializeField]
        private MeshRenderer[] sections;
        private readonly JellyColor[] sectionColors = new JellyColor[4];
        private readonly Vector3[] positions = new Vector3[4];
        private readonly Vector3[] scales = new Vector3[4];
        private MaterialPropertyBlock properties;
        private Material[] palette;
        private HashSet<JellyColor> previewColors;
        private HashSet<JellyColor> clearingColors;
        private Tween jiggle;
        private Tween dragTilt;
        private bool dragMoving;
        private float lastDragTime;
        private Vector3 dragTarget;
        private Vector3 dragVelocity;
        private Vector3 topOffset;
        private float squash;
        private bool wasDeforming;
        public void SetPalette(Material[] materials)
        {
            palette = materials;
            // Allow room for GPU vertex movement without copying the mesh.
            foreach (var section in sections)
                section.localBounds = new Bounds(Vector3.zero, Vector3.one * 2);
        }

        public void SetPiece(Piece piece, bool immediate = false)
        {
            StopAnimations();
            gameObject.SetActive(piece != null);
            if (piece == null)
                return;
            if (immediate)
            {
                for (int i = 0; i < 4; i++)
                    sectionColors[i] = JellyColor.None;
                int section = 0;
                for (int color = 1; color <= 5; color++)
                {
                    bool present = false;
                    foreach (var cell in piece.Cells)
                        if ((int)cell == color)
                            present = true;
                    if (present)
                        sectionColors[section++] = (JellyColor)color;
                }
            }

            Vector3 expansionDirection = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                int minX = 2, minY = 2, maxX = -1, maxY = -1;
                for (int q = 0; q < 4; q++)
                {
                    if (sectionColors[i] == JellyColor.None || piece.Cells[q] != sectionColors[i])
                        continue;
                    minX = Mathf.Min(minX, q % 2);
                    maxX = Mathf.Max(maxX, q % 2);
                    minY = Mathf.Min(minY, q / 2);
                    maxY = Mathf.Max(maxY, q / 2);
                }

                bool visible = maxX >= 0;
                Vector3 previousScale = scales[i];
                scales[i] = visible ? new Vector3((maxX - minX + 1) * .47f - .014f, .53f, (maxY - minY + 1) * .47f - .014f) : Vector3.zero;
                if (!immediate && visible)
                {
                    // Board rows follow Z; Y is the height above the board.
                    if (scales[i].x > previousScale.x)
                        expansionDirection.x = 1;
                    if (scales[i].z > previousScale.z)
                        expansionDirection.z = 1;
                }

                if (visible)
                {
                    positions[i] = new Vector3((minX + maxX - 1) * .235f, .28f, (1 - minY - maxY) * .235f);
                    sections[i].sharedMaterial = palette[(int)sectionColors[i]];
                }

                sections[i].gameObject.SetActive(visible);
                if (immediate || !Application.isPlaying)
                {
                    sections[i].transform.localPosition = positions[i];
                    sections[i].transform.localScale = scales[i];
                }
                else if (visible)
                {
                    sections[i].transform.DOLocalMove(positions[i], .30f).SetEase(Ease.OutCubic);
                    sections[i].transform.DOScale(scales[i], .30f).SetEase(Ease.OutCubic);
                }
            }

            SetPreviewColors(null);
            if (expansionDirection != Vector3.zero)
                PlayWobble(ExpandStrength, expansionDirection.normalized * WobbleDistance);
            else
                Kick(.9f);
        }

        public void ClearColors(HashSet<JellyColor> colors, float duration)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!colors.Contains(sectionColors[i]))
                    continue;
                sections[i].transform.DOKill();
                sections[i].transform.DOScale(Vector3.zero, duration).SetEase(Ease.InOutSine);
            }

            Kick(1.3f);
        }

        public Vector3 ColorPosition(JellyColor color)
        {
            for (int i = 0; i < sectionColors.Length; i++)
                if (sectionColors[i] == color)
                    return sections[i].transform.position;
            return transform.position;
        }

        public void Kick(float amount)
        {
            if (!Application.isPlaying || !gameObject.activeInHierarchy)
                return;
            jiggle?.Kill();
            jiggle = DOTween.Sequence()
                .Append(DOTween.To(() => squash, value => squash = value, .18f * amount, .14f).SetEase(Ease.OutSine))
                .Append(DOTween.To(() => squash, value => squash = value, -.07f * amount, .22f).SetEase(Ease.InOutSine))
                .Append(DOTween.To(() => squash, value => squash = value, .025f * amount, .18f).SetEase(Ease.InOutSine))
                .Append(DOTween.To(() => squash, value => squash = value, 0, .20f).SetEase(Ease.InOutSine));
        }

        public void PlayPlacement()
        {
            PlayWobble(1f, new Vector3(WobbleDistance, 0, .025f));
        }

        private void PlayWobble(float strength, Vector3 offset)
        {
            if (!Application.isPlaying || !gameObject.activeInHierarchy)
                return;
            Kick(strength);
            dragTilt?.Kill();
            // Bend the top gently from side to side; the shader keeps the base fixed.
            Vector3 sway = offset * strength;
            dragTilt = DOTween.Sequence()
                .Append(DOTween.To(() => topOffset, value => topOffset = value, sway, .14f).SetEase(Ease.OutSine))
                .Append(DOTween.To(() => topOffset, value => topOffset = value, -sway * .6f, .20f).SetEase(Ease.InOutSine))
                .Append(DOTween.To(() => topOffset, value => topOffset = value, sway * .25f, .18f).SetEase(Ease.InOutSine))
                .Append(DOTween.To(() => topOffset, value => topOffset = value, Vector3.zero, .20f).SetEase(Ease.InOutSine));
        }

        public void DragMotion(Vector3 movement)
        {
            movement.y = 0;
            Vector3 speed = movement / Mathf.Max(Time.deltaTime, .001f);
            if (speed.sqrMagnitude < .01f)
            {
                if (Time.time - lastDragTime > .09f)
                    EndDragMotion();
                return;
            }

            if (!dragMoving)
            {
                dragTilt?.Kill();
                dragVelocity = Vector3.zero;
                Kick(.65f);
            }

            dragMoving = true;
            lastDragTime = Time.time;
            // Slow drags bend less. SmoothDamp keeps direction changes continuous.
            dragTarget = Vector3.ClampMagnitude(-speed * .045f, .26f);
        }

        public void EndDragMotion()
        {
            if (!dragMoving)
                return;
            dragMoving = false;
            dragTarget = Vector3.zero;
            dragVelocity = Vector3.zero;
            dragTilt?.Kill();
            dragTilt = DOTween.To(() => topOffset, value => topOffset = value, Vector3.zero, .75f).SetEase(Ease.OutElastic, 1, .4f);
        }

        private void LateUpdate()
        {
            if (dragMoving)
                topOffset = Vector3.SmoothDamp(topOffset, dragTarget, ref dragVelocity, .11f);
            bool deforming = topOffset != Vector3.zero || squash != 0;
            if (deforming || wasDeforming)
                UpdateDeformation();
            wasDeforming = deforming;
        }

        private void UpdateDeformation()
        {
            if (properties == null)
                properties = new MaterialPropertyBlock();
            foreach (var section in sections)
            {
                section.GetPropertyBlock(properties);
                properties.SetVector("_TopOffset", topOffset);
                properties.SetFloat("_Squash", squash);
                section.SetPropertyBlock(properties);
            }
        }

        public void SetPreviewColors(HashSet<JellyColor> colors)
        {
            previewColors = colors;
            RefreshHighlights();
        }

        public void SetClearingColors(HashSet<JellyColor> colors)
        {
            clearingColors = colors;
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            if (properties == null)
                properties = new MaterialPropertyBlock();
            for (int i = 0; i < sections.Length; i++)
            {
                sections[i].GetPropertyBlock(properties);
                Color color = sections[i].sharedMaterial.GetColor("_BaseColor");
                // Moving the drag preview must not turn off the current clear wave's glow.
                bool highlighted = (previewColors != null && previewColors.Contains(sectionColors[i])) ||
                    (clearingColors != null && clearingColors.Contains(sectionColors[i]));
                properties.SetColor("_BaseColor", highlighted ? Color.Lerp(color, Color.white, .45f) : color);
                sections[i].SetPropertyBlock(properties);
            }
        }

        public void StopAnimations()
        {
            if (!Application.isPlaying)
                return;
            transform.DOKill();
            jiggle?.Kill();
            dragTilt?.Kill();
            jiggle = null;
            dragTilt = null;
            dragMoving = false;
            lastDragTime = 0;
            dragTarget = Vector3.zero;
            dragVelocity = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            topOffset = Vector3.zero;
            squash = 0;
            wasDeforming = false;
            previewColors = null;
            clearingColors = null;
            foreach (var section in sections)
                section.transform.DOKill();
            UpdateDeformation();
            RefreshHighlights();
        }

        private void OnDisable()
        {
            StopAnimations();
        }

    }
}
