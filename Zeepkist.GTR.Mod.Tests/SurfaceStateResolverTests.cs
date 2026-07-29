using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

public enum SurfaceParticleType
{
    Tarmac = 0,
    Grass = 1,
    Sand = 2,
    Ice = 3,
    Snow = 4,
    Wood = 5,
    Metal = 6,
    Mud = 7,
    Flesh = 8,
    Soap = 9
}

namespace Zeepkist.GTR.Mod.Tests
{
    public class SurfaceStateResolverTests
    {
        [Theory]
        [InlineData(SurfaceParticleType.Tarmac, SurfaceState.Tarmac)]
        [InlineData(SurfaceParticleType.Grass, SurfaceState.Grass)]
        [InlineData(SurfaceParticleType.Sand, SurfaceState.Sand)]
        [InlineData(SurfaceParticleType.Ice, SurfaceState.Ice)]
        [InlineData(SurfaceParticleType.Snow, SurfaceState.Snow)]
        [InlineData(SurfaceParticleType.Wood, SurfaceState.Wood)]
        [InlineData(SurfaceParticleType.Metal, SurfaceState.Metal)]
        [InlineData(SurfaceParticleType.Mud, SurfaceState.Mud)]
        [InlineData(SurfaceParticleType.Flesh, SurfaceState.Flesh)]
        [InlineData(SurfaceParticleType.Soap, SurfaceState.Soap)]
        public void FromParticleType_MapsKnownSurface(
            SurfaceParticleType particleType,
            SurfaceState expected)
        {
            Assert.Equal(expected, SurfaceStateResolver.FromParticleType(particleType));
        }

        [Fact]
        public void FromParticleType_ReturnsNoneForUnknownSurface()
        {
            Assert.Equal(
                SurfaceState.None,
                SurfaceStateResolver.FromParticleType((SurfaceParticleType)byte.MaxValue));
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
            Assert.Equal(expected, SurfaceStateResolver.ShouldIncludeWheel(enabled, grounded));
        }

        [Fact]
        public void GetEffectiveParticleType_UsesSoapOverride()
        {
            Assert.Equal(
                SurfaceParticleType.Soap,
                SurfaceStateResolver.GetEffectiveParticleType(true, SurfaceParticleType.Wood));
            Assert.Equal(
                SurfaceParticleType.Wood,
                SurfaceStateResolver.GetEffectiveParticleType(false, SurfaceParticleType.Wood));
        }

        [Fact]
        public void Combine_PreservesMixedWheelSurfaces()
        {
            SurfaceState state = SurfaceStateResolver.Combine(
                SurfaceStateResolver.FromParticleType(SurfaceParticleType.Wood),
                SurfaceStateResolver.FromParticleType(SurfaceParticleType.Mud));
            state = SurfaceStateResolver.Combine(
                state,
                SurfaceStateResolver.FromParticleType(SurfaceParticleType.Flesh));

            Assert.Equal(SurfaceState.Wood | SurfaceState.Mud | SurfaceState.Flesh, state);
        }

        [Fact]
        public void Combine_LeavesAirborneStateAsNone()
        {
            Assert.Equal(
                SurfaceState.None,
                SurfaceStateResolver.Combine(SurfaceState.None, SurfaceState.None));
        }
    }
}
