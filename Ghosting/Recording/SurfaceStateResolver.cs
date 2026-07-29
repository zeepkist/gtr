namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal static class SurfaceStateResolver
{
    public static bool ShouldIncludeWheel(bool enabled, bool grounded)
    {
        return enabled && grounded;
    }

    public static SurfaceParticleType GetEffectiveParticleType(
        bool soapOverride,
        SurfaceParticleType materialParticleType)
    {
        return soapOverride ? SurfaceParticleType.Soap : materialParticleType;
    }

    public static SurfaceState FromParticleType(SurfaceParticleType particleType)
    {
        return particleType switch
        {
            SurfaceParticleType.Tarmac => SurfaceState.Tarmac,
            SurfaceParticleType.Grass => SurfaceState.Grass,
            SurfaceParticleType.Sand => SurfaceState.Sand,
            SurfaceParticleType.Ice => SurfaceState.Ice,
            SurfaceParticleType.Snow => SurfaceState.Snow,
            SurfaceParticleType.Wood => SurfaceState.Wood,
            SurfaceParticleType.Metal => SurfaceState.Metal,
            SurfaceParticleType.Mud => SurfaceState.Mud,
            SurfaceParticleType.Flesh => SurfaceState.Flesh,
            SurfaceParticleType.Soap => SurfaceState.Soap,
            _ => SurfaceState.None
        };
    }

    public static SurfaceState Combine(SurfaceState currentState, SurfaceState nextState)
    {
        return currentState | nextState;
    }
}
