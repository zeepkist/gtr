using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostAuxiliaryRenderers
{
    public static bool IsCurrentGeneration(Renderer renderer, SetupModelCar model)
    {
        if (renderer == null)
            return false;
        if (model?.auxObjects == null ||
            renderer.transform == model.auxObjects ||
            !renderer.transform.IsChildOf(model.auxObjects))
        {
            return true;
        }

        Transform generatedRoot = GhostGeneratedRenderers.GetDirectChild(
            model.auxObjects,
            renderer.transform);
        return generatedRoot != null &&
               GeneratedChildSelection.IsCurrentAuxiliaryRoot(
                   generatedRoot.GetSiblingIndex(),
                   model.auxObjects.childCount);
    }
}

internal static class GhostGeneratedRenderers
{
    public static Transform GetDirectChild(Transform parent, Transform descendant)
    {
        if (parent == null || descendant == null || descendant == parent)
            return null;

        Transform current = descendant;
        while (current != null && current.parent != parent)
            current = current.parent;

        return current != null && current.parent == parent ? current : null;
    }
}
