using ProtoBuf;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class MaterialPhysicsStateSerializationTests
{
    [Flags]
    public enum LegacySurfaceState : byte
    {
        None = 0,
        Tarmac = 1 << 0,
        Grass = 1 << 1,
        Sand = 1 << 2,
        Snow = 1 << 3,
        Ice = 1 << 4,
        Soap = 1 << 5,
        Metal = 1 << 6
    }

    [ProtoContract]
    public class LegacyFrame
    {
        [ProtoMember(1)] public LegacySurfaceState SurfaceState { get; set; }
    }

    [ProtoContract]
    public class CurrentFrame
    {
        [ProtoMember(1)] public MaterialPhysicsState MaterialPhysicsState { get; set; }
    }

    [Fact]
    public void CurrentUshortState_DeserializesLegacyByteValues()
    {
        LegacySurfaceState[] values =
        {
            LegacySurfaceState.None,
            LegacySurfaceState.Tarmac,
            LegacySurfaceState.Grass,
            LegacySurfaceState.Sand,
            LegacySurfaceState.Snow,
            LegacySurfaceState.Ice,
            LegacySurfaceState.Soap,
            LegacySurfaceState.Metal,
            LegacySurfaceState.Tarmac |
            LegacySurfaceState.Grass |
            LegacySurfaceState.Sand |
            LegacySurfaceState.Snow |
            LegacySurfaceState.Ice |
            LegacySurfaceState.Soap |
            LegacySurfaceState.Metal
        };

        foreach (LegacySurfaceState value in values)
        {
            using MemoryStream stream = new();
            Serializer.Serialize(stream, new LegacyFrame { SurfaceState = value });
            stream.Position = 0;

            CurrentFrame frame = Serializer.Deserialize<CurrentFrame>(stream);

            Assert.Equal((ushort)value, (ushort)frame.MaterialPhysicsState);
        }
    }

    [Theory]
    [InlineData(MaterialPhysicsState.Wood)]
    [InlineData(MaterialPhysicsState.Mud)]
    [InlineData(MaterialPhysicsState.Ice1)]
    [InlineData(MaterialPhysicsState.Ice2)]
    [InlineData(MaterialPhysicsState.Ice3)]
    [InlineData(
        MaterialPhysicsState.Wood |
        MaterialPhysicsState.Mud |
        MaterialPhysicsState.Ice3)]
    public void CurrentUshortState_RoundTripsV7Values(MaterialPhysicsState value)
    {
        using MemoryStream stream = new();
        Serializer.Serialize(stream, new CurrentFrame { MaterialPhysicsState = value });
        stream.Position = 0;

        CurrentFrame frame = Serializer.Deserialize<CurrentFrame>(stream);

        Assert.Equal(value, frame.MaterialPhysicsState);
    }
}
