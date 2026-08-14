using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostCosmeticRenderers
{
    public static bool IsCurrentGeneration(
        Renderer renderer,
        SetupModelCar model,
        CosmeticsV16 cosmetics)
    {
        if (renderer == null)
            return false;
        if (model?.hatParent == null ||
            renderer.transform == model.hatParent ||
            !renderer.transform.IsChildOf(model.hatParent) ||
            cosmetics == null)
        {
            return true;
        }

        Transform generatedRoot = GhostGeneratedRenderers.GetDirectChild(
            model.hatParent,
            renderer.transform);
        return generatedRoot != null &&
               GeneratedChildSelection.IsCurrentCosmeticRoot(
                   generatedRoot.GetSiblingIndex(),
                   model.hatParent.childCount,
                   cosmetics.hat != null,
                   cosmetics.glasses != null);
    }
}
