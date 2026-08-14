using System.Collections.Generic;
using UnityEngine;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class GhostCharacterRig
{
    private sealed class RigPart
    {
        public RigPart(Transform transform)
        {
            Transform = transform;
            OriginalParent = transform.parent;
            OriginalSiblingIndex = transform.GetSiblingIndex();
            OriginalLocalPosition = transform.localPosition;
            OriginalLocalRotation = transform.localRotation;
            OriginalLocalScale = transform.localScale;
        }

        public Transform Transform { get; }
        public Transform OriginalParent { get; }
        public int OriginalSiblingIndex { get; }
        public Vector3 OriginalLocalPosition { get; }
        public Quaternion OriginalLocalRotation { get; }
        public Vector3 OriginalLocalScale { get; }
    }

    private sealed class PoseSnapshot
    {
        public PoseSnapshot(Transform transform)
        {
            Transform = transform;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
        }

        public Transform Transform { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
    }

    private readonly GameObject _root;
    private readonly Transform _modelTransform;
    private readonly Vector3 _localPosition;
    private readonly Quaternion _localRotation;
    private readonly IReadOnlyList<PoseSnapshot> _poseSnapshots;
    private readonly IReadOnlyList<RigPart> _rigParts;
    private readonly LimbPoseController _limbPoseController;

    private GhostCharacterRig(
        GameObject root,
        Transform modelTransform,
        Vector3 localPosition,
        Quaternion localRotation,
        IReadOnlyList<PoseSnapshot> poseSnapshots,
        IReadOnlyList<RigPart> rigParts,
        LimbPoseController limbPoseController)
    {
        _root = root;
        _modelTransform = modelTransform;
        _localPosition = localPosition;
        _localRotation = localRotation;
        _poseSnapshots = poseSnapshots;
        _rigParts = rigParts;
        _limbPoseController = limbPoseController;
    }

    public GameObject Root => _root;
    public Quaternion RagdollRotationOffset => _localRotation;

    public static GhostCharacterRig Create(SetupModelCar model)
    {
        if (model == null)
            return null;

        GhostCharacterRenderers.Hierarchy hierarchy = GhostCharacterRenderers.Resolve(model);
        if (!hierarchy.IsResolved)
            return null;

        Transform sourceRoot = GetSourceRoot(model, hierarchy);
        if (sourceRoot == null)
            return null;

        Transform modelTransform = model.transform;
        Vector3 localPosition = modelTransform.InverseTransformPoint(sourceRoot.position);
        Quaternion localRotation = Quaternion.Inverse(modelTransform.rotation) * sourceRoot.rotation;

        var root = new GameObject("Ghost Character Rig");
        Object.DontDestroyOnLoad(root.transform.root.gameObject);
        root.transform.SetPositionAndRotation(sourceRoot.position, sourceRoot.rotation);

        LimbPoseController limbPoseController = LimbPoseController.Create(model);
        var characterParts = new HashSet<Transform>();
        foreach (Transform characterRoot in hierarchy.Roots)
            AddPart(characterParts, characterRoot);

        foreach (Transform poseTarget in LimbPoseController.GetPoseTargets(model))
        {
            if (!hierarchy.Contains(poseTarget) && IsSafeStandalonePart(poseTarget, model))
                AddPart(characterParts, poseTarget);
        }

        var rigParts = new List<RigPart>();
        foreach (Transform part in GetTopLevelParts(characterParts))
            rigParts.Add(new RigPart(part));

        foreach (RigPart rigPart in rigParts)
            rigPart.Transform.SetParent(root.transform, true);

        IReadOnlyList<PoseSnapshot> poseSnapshots = CapturePose(root.transform);
        limbPoseController.CaptureSeatedPose();
        return new GhostCharacterRig(
            root,
            modelTransform,
            localPosition,
            localRotation,
            poseSnapshots,
            rigParts,
            limbPoseController);
    }

    public void AlignToSeated(Transform soapbox)
    {
        if (soapbox == null)
            return;

        AlignToWorld(
            soapbox.TransformPoint(_localPosition),
            soapbox.rotation * _localRotation);
    }

    public void AlignToWorld(Vector3 position, Quaternion rotation)
    {
        if (_root == null)
            return;

        _root.transform.SetPositionAndRotation(position, rotation);
    }

    public Quaternion GetRagdollWorldRotation(Quaternion recordedRotation)
    {
        return recordedRotation * RagdollRotationOffset;
    }

    public void ApplySeatedPose(bool armsUp = false)
    {
        ApplyPose(_poseSnapshots);
        _limbPoseController?.ApplySeated(armsUp);
    }

    public void ApplyStandingRagdollPose()
    {
        ApplySeatedPose();
        _limbPoseController?.ApplyStandingRagdollPose();
    }

    public void SetActive(bool active)
    {
        if (_root != null)
            _root.SetActive(active);
    }

    public void RestoreToModel()
    {
        ApplySeatedPose(false);
        AlignToSeated(_modelTransform);

        var restoredParts = new List<RigPart>();
        foreach (RigPart rigPart in _rigParts)
        {
            if (rigPart.Transform == null)
                continue;

            if (rigPart.OriginalParent == null)
            {
                DetachFromRigRoot(rigPart.Transform);
                continue;
            }

            rigPart.Transform.SetParent(rigPart.OriginalParent, false);
            rigPart.Transform.localPosition = rigPart.OriginalLocalPosition;
            rigPart.Transform.localRotation = rigPart.OriginalLocalRotation;
            rigPart.Transform.localScale = rigPart.OriginalLocalScale;
            restoredParts.Add(rigPart);
        }

        var siblingGroups = new Dictionary<Transform, List<RigPart>>();
        foreach (RigPart rigPart in restoredParts)
        {
            if (rigPart.Transform == null || rigPart.OriginalParent == null)
                continue;

            if (!siblingGroups.TryGetValue(rigPart.OriginalParent, out List<RigPart> siblings))
            {
                siblings = new List<RigPart>();
                siblingGroups.Add(rigPart.OriginalParent, siblings);
            }

            siblings.Add(rigPart);
        }

        foreach (List<RigPart> siblings in siblingGroups.Values)
        {
            siblings.Sort((left, right) => left.OriginalSiblingIndex.CompareTo(right.OriginalSiblingIndex));
            foreach (RigPart rigPart in siblings)
            {
                if (rigPart.Transform == null || rigPart.OriginalParent == null)
                    continue;

                rigPart.Transform.SetSiblingIndex(rigPart.OriginalSiblingIndex);
            }
        }
    }

    private void DetachFromRigRoot(Transform transform)
    {
        if (transform == null || _root == null || !transform.IsChildOf(_root.transform))
            return;

        Transform fallbackParent = _modelTransform;
        if (fallbackParent != null &&
            (fallbackParent == transform || fallbackParent.IsChildOf(transform)))
            fallbackParent = null;

        transform.SetParent(fallbackParent, true);
    }

    public void Destroy()
    {
        if (_root != null)
            Object.Destroy(_root);
    }

    public static bool ApplySeatedArmsUpPose(SetupModelCar model)
    {
        LimbPoseController controller = LimbPoseController.Create(model);
        if (!controller.IsAvailable)
            return false;

        controller.CaptureSeatedPose();
        controller.ApplySeated(true);
        return true;
    }

    public static bool ApplyStandingRagdollPose(SetupModelCar model)
    {
        LimbPoseController controller = LimbPoseController.Create(model);
        if (!controller.IsAvailable)
            return false;

        controller.CaptureSeatedPose();
        controller.ApplyStandingRagdollPose();
        return true;
    }

    public static Quaternion GetRagdollRotationOffset(SetupModelCar model)
    {
        if (model == null)
            return Quaternion.identity;

        GhostCharacterRenderers.Hierarchy hierarchy = GhostCharacterRenderers.Resolve(model);
        Transform sourceRoot = GetSourceRoot(model, hierarchy);
        return sourceRoot != null
            ? Quaternion.Inverse(model.transform.rotation) * sourceRoot.rotation
            : Quaternion.identity;
    }

    private static Transform GetSourceRoot(
        SetupModelCar model,
        GhostCharacterRenderers.Hierarchy hierarchy)
    {
        if (model == null || hierarchy == null || !hierarchy.IsResolved)
            return null;

        if (model.character != null && hierarchy.Contains(model.character.transform))
            return model.character.transform;

        foreach (Transform characterRoot in hierarchy.Roots)
        {
            if (characterRoot != null)
                return characterRoot;
        }

        return null;
    }

    private static IEnumerable<Transform> GetTopLevelParts(ISet<Transform> parts)
    {
        foreach (Transform part in parts)
        {
            if (!HasAncestorInSet(part, parts))
                yield return part;
        }
    }

    private static void AddPart(ISet<Transform> parts, Transform transform)
    {
        if (transform != null)
            parts.Add(transform);
    }

    private static bool IsSafeStandalonePart(Transform part, SetupModelCar model)
    {
        if (part == null || model == null || model.transform == null || part == model.transform)
            return false;
        if (!part.IsChildOf(model.transform))
            return false;

        Transform auxiliaryRoot = model.auxObjects;
        return auxiliaryRoot == null ||
               (part != auxiliaryRoot &&
                !part.IsChildOf(auxiliaryRoot) &&
                !auxiliaryRoot.IsChildOf(part));
    }

    private static IReadOnlyList<PoseSnapshot> CapturePose(Transform root)
    {
        var snapshots = new List<PoseSnapshot>();
        if (root == null)
            return snapshots;

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform == root)
                continue;

            snapshots.Add(new PoseSnapshot(transform));
        }

        return snapshots;
    }

    private static void ApplyPose(IEnumerable<PoseSnapshot> snapshots)
    {
        foreach (PoseSnapshot snapshot in snapshots)
        {
            if (snapshot.Transform == null)
                continue;

            snapshot.Transform.localPosition = snapshot.LocalPosition;
            snapshot.Transform.localRotation = snapshot.LocalRotation;
            snapshot.Transform.localScale = snapshot.LocalScale;
        }
    }

    private static bool HasAncestorInSet(Transform transform, ISet<Transform> parts)
    {
        Transform parent = transform.parent;
        while (parent != null)
        {
            if (parts.Contains(parent))
                return true;

            parent = parent.parent;
        }

        return false;
    }

    private sealed class LimbPoseController
    {
        private const float ArmsUpBlend = 0.35f;
        private const float RagdollArmHorizontal = 1.6f;
        private const float RagdollArmVertical = 0.6f;
        private const float RagdollArmForward = 0.0f;
        private const float RagdollLegHorizontal = 0.48f;
        private const float RagdollLegVertical = -1.2f;
        private const float RagdollLegForward = 0.0f;
        private static bool _loggedUnavailable;

        private readonly LimbPose _leftArm;
        private readonly LimbPose _rightArm;
        private readonly LimbPose _leftLeg;
        private readonly LimbPose _rightLeg;

        private LimbPoseController(
            LimbPose leftArm,
            LimbPose rightArm,
            LimbPose leftLeg,
            LimbPose rightLeg)
        {
            _leftArm = leftArm;
            _rightArm = rightArm;
            _leftLeg = leftLeg;
            _rightLeg = rightLeg;
        }

        public bool IsAvailable =>
            _leftArm.IsAvailable ||
            _rightArm.IsAvailable ||
            _leftLeg.IsAvailable ||
            _rightLeg.IsAvailable;

        public static LimbPoseController Create(SetupModelCar model)
        {
            if (model == null)
                return Unavailable(model);

            NetworkedZeepkistGhost prefab = ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab;
            if (prefab == null || prefab.ghostModel == null)
                return Unavailable(model);

            Quaternion leftArmOffset = CreateRelativeRotation(prefab.downLeft, prefab.upLeft, ArmsUpBlend);
            Quaternion rightArmOffset = CreateRelativeRotation(prefab.downRight, prefab.upRight, ArmsUpBlend);
            LimbPoseController controller = new(
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualLeftArm,
                    model.leftArm?.transform,
                    leftArmOffset,
                    CreateRagdollWorldPosition(model, model.leftArm?.transform, -1, RagdollArmHorizontal, RagdollArmVertical, RagdollArmForward)),
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualRightArm,
                    model.rightArm?.transform,
                    rightArmOffset,
                    CreateRagdollWorldPosition(model, model.rightArm?.transform, 1, RagdollArmHorizontal, RagdollArmVertical, RagdollArmForward)),
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualLeftLeg,
                    model.leftLeg?.transform,
                    CreateLegStandingRotation(model.leftLeg?.transform),
                    CreateRagdollWorldPosition(model, model.leftLeg?.transform, -1, RagdollLegHorizontal, RagdollLegVertical, RagdollLegForward)),
                LimbPose.Create(
                    model,
                    prefab.ghostModel,
                    prefab.visualRightLeg,
                    model.rightLeg?.transform,
                    CreateLegStandingRotation(model.rightLeg?.transform),
                    CreateRagdollWorldPosition(model, model.rightLeg?.transform, 1, RagdollLegHorizontal, RagdollLegVertical, RagdollLegForward)));

            if (!controller.IsAvailable)
                LogUnavailable(model);

            return controller;
        }

        public static IEnumerable<Transform> GetPoseTargets(SetupModelCar model)
        {
            if (model == null)
                yield break;

            NetworkedZeepkistGhost prefab = ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab;
            if (prefab == null || prefab.ghostModel == null)
                yield break;

            Transform leftArm = ResolveTarget(model, prefab.ghostModel, prefab.visualLeftArm, model.leftArm?.transform);
            if (leftArm != null)
                yield return leftArm;

            Transform rightArm = ResolveTarget(model, prefab.ghostModel, prefab.visualRightArm, model.rightArm?.transform);
            if (rightArm != null)
                yield return rightArm;

            Transform leftLeg = ResolveTarget(model, prefab.ghostModel, prefab.visualLeftLeg, model.leftLeg?.transform);
            if (leftLeg != null)
                yield return leftLeg;

            Transform rightLeg = ResolveTarget(model, prefab.ghostModel, prefab.visualRightLeg, model.rightLeg?.transform);
            if (rightLeg != null)
                yield return rightLeg;
        }

        public void CaptureSeatedPose()
        {
            _leftArm.CaptureSeatedPose();
            _rightArm.CaptureSeatedPose();
            _leftLeg.CaptureSeatedPose();
            _rightLeg.CaptureSeatedPose();
        }

        public void ApplySeated(bool armsUp)
        {
            _leftArm.ApplyRotationOffset(armsUp);
            _rightArm.ApplyRotationOffset(armsUp);
            _leftLeg.ApplySeated();
            _rightLeg.ApplySeated();
        }

        public void ApplyStandingRagdollPose()
        {
            _leftArm.ApplyPosePositionAndRotation();
            _rightArm.ApplyPosePositionAndRotation();
            _leftLeg.ApplyPosePositionAndRotation();
            _rightLeg.ApplyPosePositionAndRotation();
        }

        private static LimbPoseController Unavailable(SetupModelCar model)
        {
            LogUnavailable(model);
            return new LimbPoseController(
                LimbPose.Unavailable,
                LimbPose.Unavailable,
                LimbPose.Unavailable,
                LimbPose.Unavailable);
        }

        private static void LogUnavailable(SetupModelCar model)
        {
            if (_loggedUnavailable)
                return;

            _loggedUnavailable = true;
            Debug.LogWarning($"GTR ghost limb pose unavailable for model '{model?.name ?? "null"}'.");
        }

        private static Quaternion CreateRelativeRotation(Transform from, Transform to, float blend)
        {
            if (from == null || to == null)
                return Quaternion.identity;

            return Quaternion.Slerp(
                Quaternion.identity,
                Quaternion.Inverse(from.localRotation) * to.localRotation,
                blend);
        }

        private static Quaternion CreateLegStandingRotation(Transform fallbackTarget)
        {
            if (fallbackTarget == null)
                return Quaternion.identity;

            Vector3 currentDirection = fallbackTarget.localRotation * Vector3.forward;
            return Quaternion.FromToRotation(currentDirection, Vector3.down);
        }

        private static PoseWorldPosition CreateRagdollWorldPosition(
            SetupModelCar model,
            Transform target,
            int side,
            float horizontal,
            float vertical,
            float forward)
        {
            if (model?.character == null || target == null)
                return PoseWorldPosition.Unavailable;

            Bounds bounds = model.character.bounds;
            Vector3 worldPosition = bounds.center +
                                    model.transform.right * (bounds.extents.x * horizontal * side) +
                                    Vector3.up * (bounds.extents.y * vertical) +
                                    model.transform.forward * (bounds.extents.z * forward);
            return new PoseWorldPosition(true, worldPosition);
        }

        public static Transform ResolveTarget(
            SetupModelCar targetModel,
            SetupModelCar prefabModel,
            Transform prefabTransform,
            Transform fallback)
        {
            Transform resolved = Resolve(targetModel, prefabModel, prefabTransform);
            return resolved != null ? resolved : fallback;
        }

        private static Transform Resolve(SetupModelCar targetModel, SetupModelCar prefabModel, Transform prefabTransform)
        {
            if (targetModel == null || prefabModel == null || prefabTransform == null)
                return null;

            string path = GetRelativePath(prefabModel.transform, prefabTransform);
            if (string.IsNullOrEmpty(path))
                return null;

            return targetModel.transform.Find(path);
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (root == null || child == null || child == root)
                return null;

            string path = child.name;
            Transform current = child.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return current == root ? path : null;
        }
    }

    private readonly struct PoseLocalPosition
    {
        public static PoseLocalPosition Unavailable => new(false, Vector3.zero);

        public PoseLocalPosition(bool available, Vector3 value)
        {
            Available = available;
            Value = value;
        }

        public bool Available { get; }
        public Vector3 Value { get; }
    }

    private readonly struct PoseWorldPosition
    {
        public static PoseWorldPosition Unavailable => new(false, Vector3.zero);

        public PoseWorldPosition(bool available, Vector3 value)
        {
            Available = available;
            Value = value;
        }

        public bool Available { get; }
        public Vector3 Value { get; }
    }

    private sealed class LimbPose
    {
        public static LimbPose Unavailable { get; } = new(null, Quaternion.identity, PoseWorldPosition.Unavailable);

        private readonly Transform _target;
        private readonly Quaternion _poseRotationOffset;
        private readonly PoseWorldPosition _poseWorldPosition;
        private PoseLocalPosition _poseLocalPosition;
        private Vector3 _seatedLocalPosition;
        private Quaternion _seatedLocalRotation;
        private Vector3 _seatedLocalScale;
        private bool _hasSeatedPose;

        private LimbPose(
            Transform target,
            Quaternion poseRotationOffset,
            PoseWorldPosition poseWorldPosition)
        {
            _target = target;
            _poseRotationOffset = poseRotationOffset;
            _poseWorldPosition = poseWorldPosition;
            _poseLocalPosition = PoseLocalPosition.Unavailable;
        }

        public bool IsAvailable => _target != null;

        public static LimbPose Create(
            SetupModelCar targetModel,
            SetupModelCar prefabModel,
            Transform prefabTarget,
            Transform fallbackTarget,
            Quaternion poseRotationOffset,
            PoseWorldPosition poseWorldPosition)
        {
            Transform target = LimbPoseController.ResolveTarget(targetModel, prefabModel, prefabTarget, fallbackTarget);
            return target != null ? new LimbPose(target, poseRotationOffset, poseWorldPosition) : Unavailable;
        }

        public void CaptureSeatedPose()
        {
            if (_target == null)
                return;

            _seatedLocalPosition = _target.localPosition;
            _seatedLocalRotation = _target.localRotation;
            _seatedLocalScale = _target.localScale;
            _poseLocalPosition = _poseWorldPosition.Available && _target.parent != null
                ? new PoseLocalPosition(
                    true,
                    _target.parent.InverseTransformPoint(_poseWorldPosition.Value))
                : PoseLocalPosition.Unavailable;
            _hasSeatedPose = true;
        }

        public void ApplySeated()
        {
            if (_target == null || !_hasSeatedPose)
                return;

            _target.localPosition = _seatedLocalPosition;
            _target.localRotation = _seatedLocalRotation;
            _target.localScale = _seatedLocalScale;
        }

        public void ApplyRotationOffset(bool enabled)
        {
            if (_target == null || !_hasSeatedPose)
                return;

            _target.localPosition = _seatedLocalPosition;
            _target.localRotation = enabled
                ? _seatedLocalRotation * _poseRotationOffset
                : _seatedLocalRotation;
            _target.localScale = _seatedLocalScale;
        }

        public void ApplyPosePositionAndRotation()
        {
            if (_target == null || !_hasSeatedPose)
                return;

            _target.localPosition = _poseLocalPosition.Available
                ? _poseLocalPosition.Value
                : _seatedLocalPosition;
            _target.localRotation = _seatedLocalRotation * _poseRotationOffset;
            _target.localScale = _seatedLocalScale;
        }
    }
}
