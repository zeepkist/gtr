using TNRD.Zeepkist.GTR.Ghosting.Playback;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public sealed class HierarchyOwnershipTests
{
    [Fact]
    public void ResolveRootsUsesNearestCommonCharacterAncestor()
    {
        Node model = new("Model");
        Node character = new("Character", model);
        Node body = new("Body", character);
        Node leftArm = new("Left Arm", character);
        Node armature = new("Armature", character);
        Node rootBone = new("Top", armature);
        Node hatParent = new("Hat Holder", rootBone);

        IReadOnlyList<Node> roots = Resolve(model, null, body, leftArm, rootBone, hatParent);

        Assert.Equal(new[] { character }, roots);
    }

    [Fact]
    public void ResolveRootsRejectsModelRootAndFallsBackToExactAnchors()
    {
        Node model = new("Model");
        Node body = new("Body", new Node("Character", model));
        Node detachedLimb = new("Limb", new Node("Unexpected Branch", model));

        IReadOnlyList<Node> roots = Resolve(model, null, body, detachedLimb);

        Assert.Equal(new[] { body, detachedLimb }, roots);
        Assert.DoesNotContain(model, roots);
    }

    [Fact]
    public void ResolveRootsExcludesAuxiliaryBranchesAndRootsContainingThem()
    {
        Node model = new("Model");
        Node character = new("Character", model);
        Node body = new("Body", character);
        Node mixedBranch = new("Mixed", model);
        Node auxiliary = new("Auxiliary", mixedBranch);
        Node fakeFace = new("Face", auxiliary);

        IReadOnlyList<Node> roots = Resolve(model, auxiliary, body, mixedBranch, fakeFace);

        Assert.Equal(new[] { body }, roots);
    }

    [Fact]
    public void ResolveRootsRejectsCommonAncestorContainingAuxiliaryRoot()
    {
        Node model = new("Model");
        Node character = new("Character", model);
        Node body = new("Body", character);
        Node leftArm = new("Left Arm", character);
        Node auxiliary = new("Auxiliary", character);

        IReadOnlyList<Node> roots = Resolve(model, auxiliary, body, leftArm);

        Assert.Equal(new[] { body, leftArm }, roots);
        Assert.DoesNotContain(character, roots);
    }

    [Fact]
    public void OwnershipDependsOnHierarchyNotNames()
    {
        Node model = new("Model");
        Node character = new("Unlabelled Branch", model);
        Node body = new("Mesh 42", character);
        Node rootBone = new("Joint 17", character);
        Node renamedHat = new("Cosmetic 99", new Node("Holder", rootBone));
        Node soapbox = new("Player Body Arm Leg Headlight", model);
        Node auxiliary = new("Auxiliary", model);
        Node fakeFace = new("Face Smile Heart Head", auxiliary);
        IReadOnlyList<Node> roots = Resolve(model, auxiliary, body, rootBone);

        Assert.Single(roots);
        Assert.True(IsOwned(renamedHat, roots));
        Assert.False(IsOwned(soapbox, roots));
        Assert.False(IsOwned(fakeFace, roots));
    }

    [Fact]
    public void ExactFallbackCollapsesNestedAnchors()
    {
        Node model = new("Model");
        Node character = new("Character", model);
        Node body = new("Body", character);
        Node bodyChild = new("Body Child", body);
        Node detachedLimb = new("Limb", new Node("Unexpected Branch", model));

        IReadOnlyList<Node> roots = Resolve(model, null, body, bodyChild, detachedLimb);

        Assert.Equal(new[] { body, detachedLimb }, roots);
    }

    private static IReadOnlyList<Node> Resolve(
        Node model,
        Node auxiliary,
        params Node[] anchors)
    {
        return HierarchyOwnership.ResolveRoots(anchors, model, auxiliary, node => node.Parent);
    }

    private static bool IsOwned(Node node, IEnumerable<Node> roots)
    {
        return roots.Any(root => HierarchyOwnership.IsSameOrDescendant(node, root, current => current.Parent));
    }

    private sealed class Node
    {
        public Node(string name, Node parent = null)
        {
            Name = name;
            Parent = parent;
        }

        public string Name { get; }
        public Node Parent { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
