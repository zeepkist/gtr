using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public partial class GhostRenderer : IDisposable
{
    private readonly List<RendererData> _rendererData = new();
    private readonly Material _ghostMaterial;
    private readonly bool _ownsGhostMaterial;
    private Color _lastGhostColor;
    private bool _hasLastGhostColor;
    private bool _disposed;

    public GhostRenderer(
        GameObject gameObject,
        GhostVisualProfile visualProfile,
        SetupModelCar model = null,
        CosmeticsV16 cosmetics = null)
        : this(new[] { gameObject }, visualProfile, model, cosmetics)
    {
    }

    public GhostRenderer(
        IEnumerable<GameObject> gameObjects,
        GhostVisualProfile visualProfile,
        SetupModelCar model = null,
        CosmeticsV16 cosmetics = null)
    {
        _ownsGhostMaterial = visualProfile == GhostVisualProfile.Full;
        _ghostMaterial = RendererData.CreateGhostMaterial(visualProfile);
        var renderers = new HashSet<Renderer>();

        foreach (GameObject gameObject in gameObjects)
            AddRenderers(gameObject, visualProfile, model, cosmetics, renderers);
    }

    private void AddRenderers(
        GameObject gameObject,
        GhostVisualProfile visualProfile,
        SetupModelCar model,
        CosmeticsV16 cosmetics,
        ISet<Renderer> addedRenderers)
    {
        if (gameObject == null)
            return;

        bool includeInactive = visualProfile == GhostVisualProfile.Full;
        SetupModelCar rendererModel = model != null
            ? model
            : gameObject.GetComponent<SetupModelCar>();
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(includeInactive);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                (rendererModel != null &&
                 (!GhostAuxiliaryRenderers.IsCurrentGeneration(renderer, rendererModel) ||
                  !GhostCosmeticRenderers.IsCurrentGeneration(renderer, rendererModel, cosmetics))) ||
                !addedRenderers.Add(renderer))
            {
                continue;
            }

            _rendererData.Add(new RendererData(renderer, visualProfile, _ghostMaterial));
        }
    }

    public void SwitchToNormal()
    {
        _rendererData.ForEach(rendererData => rendererData.SwitchToNormal());
    }

    public void SwitchToGhost()
    {
        _rendererData.ForEach(rendererData => rendererData.SwitchToGhost());
    }

    public void Enable()
    {
        _rendererData.ForEach(rendererData => rendererData.Enable());
    }

    public void Disable()
    {
        _rendererData.ForEach(rendererData => rendererData.Disable());
    }

    public void SetFade(float fade)
    {
        _rendererData.ForEach(rendererData => rendererData.SetFade(fade));
    }

    public void SetGhostColor(Color color)
    {
        if (!_ownsGhostMaterial)
            return;
        if (_hasLastGhostColor && _lastGhostColor == color)
            return;

        _lastGhostColor = color;
        _hasLastGhostColor = true;
        RendererData.SetGhostColor(_ghostMaterial, color);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _rendererData.ForEach(rendererData => rendererData.Dispose());
        _rendererData.Clear();

        if (_ownsGhostMaterial && _ghostMaterial != null)
            Object.Destroy(_ghostMaterial);
    }

    public static void DisposeSharedResources()
    {
        RendererData.DisposeSharedResources();
    }
}
