namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal static class MaterialPhysicsStateResolver
{
    public static bool ShouldIncludeWheel(bool enabled, bool grounded)
    {
        return enabled && grounded;
    }

    public static MaterialPhysicsState GetEffectiveState(
        bool soapOverride,
        string materialPhysicsName)
    {
        return soapOverride
            ? MaterialPhysicsState.Soap
            : FromPhysicsName(materialPhysicsName);
    }

    public static MaterialPhysicsState FromPhysicsName(string materialPhysicsName)
    {
        return materialPhysicsName switch
        {
            "Tarmac" => MaterialPhysicsState.Tarmac,
            "Grass" => MaterialPhysicsState.Grass,
            "Sand" => MaterialPhysicsState.Sand,
            "Soap" => MaterialPhysicsState.Soap,
            "Wood" => MaterialPhysicsState.Wood,
            "Mud" => MaterialPhysicsState.Mud,
            "Ice 0.05" => MaterialPhysicsState.Ice1,
            "Ice 0.10" => MaterialPhysicsState.Ice2,
            "Ice 0.15" => MaterialPhysicsState.Ice3,
            _ => MaterialPhysicsState.None
        };
    }

    public static MaterialPhysicsState Combine(
        MaterialPhysicsState currentState,
        MaterialPhysicsState nextState)
    {
        return currentState | nextState;
    }
}
