using System.Collections.Generic;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public static class GhostCharacterRenderers
{
    public sealed class Hierarchy
    {
        private readonly Transform _auxiliaryRoot;
        private readonly IReadOnlyList<Transform> _roots;

        internal Hierarchy(IReadOnlyList<Transform> roots, Transform auxiliaryRoot)
        {
            _roots = roots == null
                ? new List<Transform>().AsReadOnly()
                : new List<Transform>(roots).AsReadOnly();
            _auxiliaryRoot = auxiliaryRoot != null ? auxiliaryRoot : null;
        }

        public bool IsResolved => _roots.Count > 0;
        public Transform Root => _roots.Count == 1 ? _roots[0] : null;
        public IReadOnlyList<Transform> Roots => _roots;

        public bool Contains(Renderer renderer)
        {
            return renderer != null && Contains(renderer.transform);
        }

        public bool Contains(Transform transform)
        {
            if (transform == null || IsSameOrDescendant(transform, _auxiliaryRoot))
                return false;

            foreach (Transform root in _roots)
            {
                if (IsSameOrDescendant(transform, root))
                    return true;
            }

            return false;
        }
    }

    public static Hierarchy Resolve(SetupModelCar model)
    {
        if (model == null)
            return new Hierarchy(null, null);

        Transform modelRoot = model.transform;
        Transform auxiliaryRoot = model.auxObjects != null ? model.auxObjects : null;
        var anchors = new List<Transform>();

        AddAnchor(anchors, model.character != null ? model.character.transform : null, modelRoot, auxiliaryRoot);
        AddAnchor(anchors, model.leftArm != null ? model.leftArm.transform : null, modelRoot, auxiliaryRoot);
        AddAnchor(anchors, model.rightArm != null ? model.rightArm.transform : null, modelRoot, auxiliaryRoot);
        AddAnchor(anchors, model.leftLeg != null ? model.leftLeg.transform : null, modelRoot, auxiliaryRoot);
        AddAnchor(anchors, model.rightLeg != null ? model.rightLeg.transform : null, modelRoot, auxiliaryRoot);
        AddAnchor(anchors, model.hatParent, modelRoot, auxiliaryRoot);

        if (model.character != null)
        {
            AddAnchor(anchors, model.character.rootBone, modelRoot, auxiliaryRoot);
            Transform[] bones = model.character.bones;
            if (bones != null)
            {
                foreach (Transform bone in bones)
                    AddAnchor(anchors, bone, modelRoot, auxiliaryRoot);
            }
        }

        IReadOnlyList<Transform> roots = HierarchyOwnership.ResolveRoots(
            anchors,
            modelRoot,
            auxiliaryRoot,
            transform => transform.parent);
        return new Hierarchy(roots, auxiliaryRoot);
    }

    public static void SetCharacterRenderersActive(SetupModelCar model, bool active)
    {
        if (model == null)
            return;

        Hierarchy hierarchy = Resolve(model);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (hierarchy.Contains(renderer))
                renderer.enabled = active;
        }
    }

    public static void SetNonCharacterRenderersActive(SetupModelCar model, bool active)
    {
        if (model == null)
            return;

        Hierarchy hierarchy = Resolve(model);
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!hierarchy.Contains(renderer))
                renderer.enabled = active;
        }
    }

    public static bool IsCharacterRenderer(Renderer renderer, Hierarchy hierarchy)
    {
        return renderer != null && hierarchy != null && hierarchy.Contains(renderer);
    }

    public static bool IsCharacterRenderer(Renderer renderer, SetupModelCar model)
    {
        return renderer != null && model != null && IsCharacterRenderer(renderer, Resolve(model));
    }

    private static void AddAnchor(
        ICollection<Transform> anchors,
        Transform anchor,
        Transform modelRoot,
        Transform auxiliaryRoot)
    {
        if (anchor == null ||
            modelRoot == null ||
            anchor == modelRoot ||
            !anchor.IsChildOf(modelRoot) ||
            IsSameOrDescendant(anchor, auxiliaryRoot) ||
            IsSameOrDescendant(auxiliaryRoot, anchor) ||
            anchors.Contains(anchor))
        {
            return;
        }

        anchors.Add(anchor);
    }

    private static bool IsSameOrDescendant(Transform transform, Transform root)
    {
        return transform != null &&
               root != null &&
               (transform == root || transform.IsChildOf(root));
    }
}
