using System;
using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

internal readonly struct GhostFrameSample
{
    internal GhostFrameSample(int currentIndex, int nextIndex, float interpolation)
    {
        CurrentIndex = currentIndex;
        NextIndex = nextIndex;
        Interpolation = interpolation;
    }

    internal int CurrentIndex { get; }
    internal int NextIndex { get; }
    internal float Interpolation { get; }
}

internal static class GhostFrameSearch
{
    internal static bool TryGetFrameSample(
        int frameCount,
        float time,
        Func<int, float> getTimeAtIndex,
        out GhostFrameSample sample)
    {
        sample = default;
        if (frameCount <= 0)
            return false;

        if (frameCount == 1 || time < getTimeAtIndex(0))
        {
            sample = new GhostFrameSample(0, 0, 0f);
            return true;
        }

        int currentIndex = FindLastFrameIndexAtOrBeforeTime(frameCount, time, getTimeAtIndex);
        if (currentIndex < 0)
        {
            sample = new GhostFrameSample(0, 0, 0f);
            return true;
        }

        if (currentIndex >= frameCount - 1)
        {
            int lastIndex = frameCount - 1;
            sample = new GhostFrameSample(lastIndex, lastIndex, 0f);
            return true;
        }

        int nextIndex = currentIndex + 1;
        float currentTime = getTimeAtIndex(currentIndex);
        float nextTime = getTimeAtIndex(nextIndex);
        float interpolation = nextTime > currentTime
            ? (time - currentTime) / (nextTime - currentTime)
            : 0f;
        if (interpolation < 0f)
            interpolation = 0f;
        else if (interpolation > 1f)
            interpolation = 1f;

        sample = new GhostFrameSample(currentIndex, nextIndex, interpolation);
        return true;
    }

    internal static bool TryGetFrameSample(
        int frameCount,
        float time,
        int currentIndexHint,
        Func<int, float> getTimeAtIndex,
        out GhostFrameSample sample)
    {
        sample = default;
        if (frameCount <= 0)
            return false;

        if (currentIndexHint < 0 || currentIndexHint >= frameCount)
            return TryGetFrameSample(frameCount, time, getTimeAtIndex, out sample);

        float currentTime = getTimeAtIndex(currentIndexHint);
        if (time < currentTime)
        {
            if (currentIndexHint == 0)
            {
                sample = new GhostFrameSample(0, 0, 0f);
                return true;
            }

            return TryGetFrameSample(frameCount, time, getTimeAtIndex, out sample);
        }

        int currentIndex = currentIndexHint;
        while (currentIndex < frameCount - 1)
        {
            int nextIndex = currentIndex + 1;
            float nextTime = getTimeAtIndex(nextIndex);
            if (nextTime > time)
            {
                float interpolation = nextTime > currentTime
                    ? (time - currentTime) / (nextTime - currentTime)
                    : 0f;
                if (interpolation < 0f)
                    interpolation = 0f;
                else if (interpolation > 1f)
                    interpolation = 1f;

                sample = new GhostFrameSample(currentIndex, nextIndex, interpolation);
                return true;
            }

            currentIndex++;
            currentTime = nextTime;
        }

        sample = new GhostFrameSample(currentIndex, currentIndex, 0f);
        return true;
    }

    private static int FindLastFrameIndexAtOrBeforeTime(
        int frameCount,
        float time,
        Func<int, float> getTimeAtIndex)
    {
        int low = 0;
        int high = frameCount;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (getTimeAtIndex(mid) <= time)
                low = mid + 1;
            else
                high = mid;
        }

        return low - 1;
    }

    internal static int FindFirstFrameIndexAtOrAfterTime(
        int frameCount,
        float time,
        Func<int, float> getTimeAtIndex)
    {
        int low = 1;
        int high = frameCount - 1;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (getTimeAtIndex(mid) < time)
                low = mid + 1;
            else
                high = mid;
        }

        return getTimeAtIndex(low) < time ? frameCount : low;
    }

    internal static int FindFirstFrameIndexAtOrAfterTime<TFrame>(
        IReadOnlyList<TFrame> frames,
        float time,
        Func<TFrame, float> getTime)
    {
        return FindFirstFrameIndexAtOrAfterTime(frames.Count, time, index => getTime(frames[index]));
    }

    internal static bool TryGetAdjacentFrameTime(
        int frameCount,
        float currentTime,
        int direction,
        float timeEpsilon,
        Func<int, float> getTimeAtIndex,
        out float adjacentTime)
    {
        adjacentTime = 0f;
        if (frameCount <= 1 || direction is not (1 or -1))
            return false;

        if (direction > 0)
        {
            int nextIndex = FindFirstFrameIndexAtOrAfterTime(
                frameCount,
                currentTime + timeEpsilon,
                getTimeAtIndex);
            if (nextIndex >= frameCount)
                return false;

            adjacentTime = getTimeAtIndex(nextIndex);
            return true;
        }

        int nextOrEqualIndex = FindFirstFrameIndexAtOrAfterTime(frameCount, currentTime, getTimeAtIndex);
        int prevIndex = nextOrEqualIndex - 1;
        if (prevIndex < 0)
            return false;

        if (prevIndex == 0 && currentTime <= getTimeAtIndex(0) + timeEpsilon)
            return false;

        adjacentTime = getTimeAtIndex(prevIndex);
        return true;
    }
}
