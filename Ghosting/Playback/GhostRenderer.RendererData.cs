using System;
using System.Collections.Generic;
using UnityEngine;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostRenderer
{
    private class RendererData : IDisposable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int ColorTintId = Shader.PropertyToID("_ColorTint");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static Material _bulkGhostMaterial;

        private readonly Renderer _renderer;
        private readonly Material[] _originalSharedMaterials;
        private readonly Material[] _normalMaterials;
        private readonly Material[] _ghostMaterials;
        private readonly Dictionary<Material, MaterialRenderState> _normalMaterialStates = new();
        private readonly bool _ownsMaterials;
        private float _lastFade = float.NaN;

        public RendererData(
            Renderer renderer,
            GhostVisualProfile visualProfile,
            Material ghostMaterial)
        {
            _renderer = renderer;
            _ownsMaterials = visualProfile == GhostVisualProfile.Full;
            _originalSharedMaterials = _renderer.sharedMaterials;
            _normalMaterials = _ownsMaterials
                ? _renderer.materials
                : _originalSharedMaterials;
            _ghostMaterials = new Material[_normalMaterials.Length];

            foreach (Material material in _normalMaterials)
            {
                if (material != null && !_normalMaterialStates.ContainsKey(material))
                    _normalMaterialStates.Add(material, MaterialRenderState.Capture(material));
            }

            for (int i = 0; i < _ghostMaterials.Length; i++)
                _ghostMaterials[i] = ghostMaterial;
        }

        public void SwitchToNormal()
        {
            if (_renderer == null)
                return;

            if (_ownsMaterials)
                _renderer.materials = _normalMaterials;
            else
                _renderer.sharedMaterials = _normalMaterials;
        }

        public void SwitchToGhost()
        {
            if (_renderer == null)
                return;

            if (_ownsMaterials)
                _renderer.materials = _ghostMaterials;
            else
                _renderer.sharedMaterials = _ghostMaterials;
        }

        public void Enable()
        {
            if (_renderer != null)
                _renderer.enabled = true;
        }

        public void Disable()
        {
            if (_renderer != null)
                _renderer.enabled = false;
        }

        public void SetFade(float fade)
        {
            if (!_ownsMaterials || _renderer == null)
                return;
            if (Mathf.Approximately(_lastFade, fade))
                return;

            _lastFade = fade;

            foreach (Material normalMaterial in _normalMaterials)
                SetMaterialAlpha(normalMaterial, fade, _normalMaterialStates);
        }

        public void Dispose()
        {
            if (!_ownsMaterials)
                return;

            if (_renderer != null)
                _renderer.sharedMaterials = _originalSharedMaterials;

            foreach (Material material in _normalMaterialStates.Keys)
            {
                if (material != null)
                    Object.Destroy(material);
            }
        }

        public static Material CreateGhostMaterial(GhostVisualProfile visualProfile)
        {
            if (visualProfile == GhostVisualProfile.Full)
            {
                return new Material(
                    ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostFader.fadeThisMaterial);
            }

            return GetBulkGhostMaterial();
        }

        public static void SetGhostColor(Material material, Color color)
        {
            if (material != null && material.HasProperty(ColorId))
                material.color = color;
        }

        public static void DisposeSharedResources()
        {
            if (_bulkGhostMaterial == null)
                return;

            Object.Destroy(_bulkGhostMaterial);
            _bulkGhostMaterial = null;
        }

        private static Material GetBulkGhostMaterial()
        {
            if (_bulkGhostMaterial != null)
                return _bulkGhostMaterial;

            _bulkGhostMaterial = new Material(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostFader.fadeThisMaterial)
            {
                enableInstancing = true,
                color = Color.white with { a = 0.3f }
            };
            return _bulkGhostMaterial;
        }

        private static void SetMaterialAlpha(
            Material material,
            float alpha,
            IReadOnlyDictionary<Material, MaterialRenderState> originalStates)
        {
            if (material == null)
                return;
            if (!originalStates.TryGetValue(material, out MaterialRenderState originalState))
                return;

            if (!Mathf.Approximately(alpha, 1f))
            {
                EnableTransparentRendering(material);
                originalState.ApplyFade(material, alpha);
                return;
            }

            originalState.Apply(material);
        }

        private static void EnableTransparentRendering(Material material)
        {
            if (material.HasProperty(ModeId))
                material.SetFloat(ModeId, 3f);
            if (material.HasProperty(SrcBlendId))
                material.SetInt(SrcBlendId, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty(DstBlendId))
                material.SetInt(DstBlendId, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty(ZWriteId))
                material.SetInt(ZWriteId, 0);

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private readonly struct MaterialRenderState
        {
            private MaterialRenderState(
                int renderQueue,
                float? mode,
                int? srcBlend,
                int? dstBlend,
                int? zWrite,
                bool alphaTest,
                bool alphaBlend,
                bool alphaPremultiply,
                Color? color,
                Color? baseColor,
                Color? tintColor,
                Color? colorTint)
            {
                RenderQueue = renderQueue;
                Mode = mode;
                SrcBlend = srcBlend;
                DstBlend = dstBlend;
                ZWrite = zWrite;
                AlphaTest = alphaTest;
                AlphaBlend = alphaBlend;
                AlphaPremultiply = alphaPremultiply;
                MainColor = color;
                BaseColor = baseColor;
                TintColor = tintColor;
                ColorTint = colorTint;
            }

            private int RenderQueue { get; }
            private float? Mode { get; }
            private int? SrcBlend { get; }
            private int? DstBlend { get; }
            private int? ZWrite { get; }
            private bool AlphaTest { get; }
            private bool AlphaBlend { get; }
            private bool AlphaPremultiply { get; }
            private Color? MainColor { get; }
            private Color? BaseColor { get; }
            private Color? TintColor { get; }
            private Color? ColorTint { get; }

            public static MaterialRenderState Capture(Material material)
            {
                return new MaterialRenderState(
                    material.renderQueue,
                    material.HasProperty(ModeId) ? material.GetFloat(ModeId) : null,
                    material.HasProperty(SrcBlendId) ? material.GetInt(SrcBlendId) : null,
                    material.HasProperty(DstBlendId) ? material.GetInt(DstBlendId) : null,
                    material.HasProperty(ZWriteId) ? material.GetInt(ZWriteId) : null,
                    material.IsKeywordEnabled("_ALPHATEST_ON"),
                    material.IsKeywordEnabled("_ALPHABLEND_ON"),
                    material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"),
                    GetColor(material, ColorId),
                    GetColor(material, BaseColorId),
                    GetColor(material, TintColorId),
                    GetColor(material, ColorTintId));
            }

            public void Apply(Material material)
            {
                material.renderQueue = RenderQueue;
                if (Mode.HasValue && material.HasProperty(ModeId))
                    material.SetFloat(ModeId, Mode.Value);
                if (SrcBlend.HasValue && material.HasProperty(SrcBlendId))
                    material.SetInt(SrcBlendId, SrcBlend.Value);
                if (DstBlend.HasValue && material.HasProperty(DstBlendId))
                    material.SetInt(DstBlendId, DstBlend.Value);
                if (ZWrite.HasValue && material.HasProperty(ZWriteId))
                    material.SetInt(ZWriteId, ZWrite.Value);

                SetKeyword(material, "_ALPHATEST_ON", AlphaTest);
                SetKeyword(material, "_ALPHABLEND_ON", AlphaBlend);
                SetKeyword(material, "_ALPHAPREMULTIPLY_ON", AlphaPremultiply);
                ApplyColors(material, 1f);
            }

            public void ApplyFade(Material material, float fade)
            {
                ApplyColors(material, fade);
            }

            private static void SetKeyword(Material material, string keyword, bool enabled)
            {
                if (enabled)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            private static Color? GetColor(Material material, int propertyId)
            {
                return material.HasProperty(propertyId)
                    ? material.GetColor(propertyId)
                    : null;
            }

            private void ApplyColors(Material material, float alphaMultiplier)
            {
                ApplyColor(material, ColorId, MainColor, alphaMultiplier);
                ApplyColor(material, BaseColorId, BaseColor, alphaMultiplier);
                ApplyColor(material, TintColorId, TintColor, alphaMultiplier);
                ApplyColor(material, ColorTintId, ColorTint, alphaMultiplier);
            }

            private static void ApplyColor(
                Material material,
                int propertyId,
                Color? originalColor,
                float alphaMultiplier)
            {
                if (!originalColor.HasValue || !material.HasProperty(propertyId))
                    return;

                Color color = originalColor.Value;
                color.a *= alphaMultiplier;
                material.SetColor(propertyId, color);
            }
        }
    }
}
