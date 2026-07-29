using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests
{
    public class MaterialPhysicsStateResolverTests
    {
        [Theory]
        [InlineData("Tarmac", MaterialPhysicsState.Tarmac)]
        [InlineData("Grass", MaterialPhysicsState.Grass)]
        [InlineData("Sand", MaterialPhysicsState.Sand)]
        [InlineData("Soap", MaterialPhysicsState.Soap)]
        [InlineData("Wood", MaterialPhysicsState.Wood)]
        [InlineData("Mud", MaterialPhysicsState.Mud)]
        [InlineData("Ice 0.05", MaterialPhysicsState.Ice1)]
        [InlineData("Ice 0.10", MaterialPhysicsState.Ice2)]
        [InlineData("Ice 0.15", MaterialPhysicsState.Ice3)]
        public void FromPhysicsName_MapsKnownMaterialPhysics(
            string physicsName,
            MaterialPhysicsState expected)
        {
            Assert.Equal(expected, MaterialPhysicsStateResolver.FromPhysicsName(physicsName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Ice")]
        [InlineData("Metal")]
        public void FromPhysicsName_ReturnsNoneForUnknownMaterialPhysics(string physicsName)
        {
            Assert.Equal(
                MaterialPhysicsState.None,
                MaterialPhysicsStateResolver.FromPhysicsName(physicsName));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void ShouldIncludeWheel_RequiresEnabledAndGrounded(
            bool enabled,
            bool grounded,
            bool expected)
        {
            Assert.Equal(expected, MaterialPhysicsStateResolver.ShouldIncludeWheel(enabled, grounded));
        }

        [Fact]
        public void GetEffectiveState_UsesSoapOverride()
        {
            Assert.Equal(
                MaterialPhysicsState.Soap,
                MaterialPhysicsStateResolver.GetEffectiveState(true, "Wood"));
            Assert.Equal(
                MaterialPhysicsState.Wood,
                MaterialPhysicsStateResolver.GetEffectiveState(false, "Wood"));
        }

        [Fact]
        public void Combine_PreservesMixedWheelSurfaces()
        {
            MaterialPhysicsState state = MaterialPhysicsStateResolver.Combine(
                MaterialPhysicsStateResolver.FromPhysicsName("Wood"),
                MaterialPhysicsStateResolver.FromPhysicsName("Mud"));
            state = MaterialPhysicsStateResolver.Combine(
                state,
                MaterialPhysicsStateResolver.FromPhysicsName("Ice 0.15"));

            Assert.Equal(
                MaterialPhysicsState.Wood |
                MaterialPhysicsState.Mud |
                MaterialPhysicsState.Ice3,
                state);
        }

        [Fact]
        public void Combine_LeavesAirborneStateAsNone()
        {
            Assert.Equal(
                MaterialPhysicsState.None,
                MaterialPhysicsStateResolver.Combine(
                    MaterialPhysicsState.None,
                    MaterialPhysicsState.None));
        }
    }
}
