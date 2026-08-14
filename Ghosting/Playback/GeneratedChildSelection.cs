namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

internal static class GeneratedChildSelection
{
    public static bool IsCurrentAuxiliaryRoot(int siblingIndex, int childCount)
    {
        return childCount > 0 && siblingIndex == childCount - 1;
    }

    public static bool IsCurrentCosmeticRoot(
        int siblingIndex,
        int childCount,
        bool hasHat,
        bool hasGlasses)
    {
        if (siblingIndex < 0 || siblingIndex >= childCount)
            return false;

        // SetupModelCar appends the requested hat first and glasses second.
        int currentIndex = childCount - 1;
        if (hasGlasses && siblingIndex == currentIndex--)
            return true;

        return hasHat && siblingIndex == currentIndex;
    }
}
