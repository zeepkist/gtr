using TNRD.Zeepkist.GTR.Ghosting.Recording;
using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class RecordFeedbackFormatterTests
{
    [Fact]
    public void ClassifiesNonPersonalBest()
    {
        Assert.Equal(RecordFeedbackKind.None,
            RecordFeedbackFormatter.Classify(12, 11, 10, "other", "me"));
    }

    [Theory]
    [InlineData(RecordFeedbackKind.PersonalBest, 12.0, 13.0, 10.0, "other")]
    [InlineData(RecordFeedbackKind.FirstPersonalBest, 12.0, null, 10.0, "other")]
    [InlineData(RecordFeedbackKind.NewWorldRecord, 9.0, 12.0, 10.0, "other")]
    [InlineData(RecordFeedbackKind.ImprovedWorldRecord, 9.0, 10.0, 10.0, "me")]
    public void ClassifiesFeedbackKinds(
        RecordFeedbackKind expected,
        double submitted,
        double? personalBest,
        double? worldRecord,
        string worldRecordSteamId)
    {
        Assert.Equal(expected,
            RecordFeedbackFormatter.Classify(submitted, personalBest, worldRecord, worldRecordSteamId, "me"));
    }

    [Fact]
    public void FormatsImprovementPlaceScoresAndNextTarget()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.PersonalBest,
            PreviousDelta = "00:01.000",
            NextDelta = "00:00.250",
            PreviousPosition = 8,
            Position = 5,
            LevelDecayedPoints = 123.6,
            PlayerDecayedPoints = 77.4
        });

        Assert.Contains($"PB improved by {TextColour.Pink.Wrap("00:01.000")}", result);
        Assert.Contains($"gained {TextColour.Pink.Wrap("3")} places", result);
        Assert.Contains(TextColour.Yellow.Wrap("124pts"), result);
        Assert.Contains(TextColour.Yellow.Wrap("77 ranked pts"), result);
        Assert.Contains($"Improve by {TextColour.Pink.Wrap("00:00.250")}", result);
    }

    [Fact]
    public void FormatsFirstPersonalBestEntry()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.FirstPersonalBest,
            WasFirstPersonalBest = true,
            Position = 42,
            LevelDecayedPoints = 10,
            PlayerDecayedPoints = 5
        });

        Assert.StartsWith("[GTR] You set your first PB", result);
        Assert.Contains($"entered the level leaderboard at {TextColour.Pink.Wrap("#42")}", result);
    }

    [Fact]
    public void FirstPersonalBestWorldRecordKeepsWorldRecordHeadlineAndEntryClause()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.NewWorldRecord,
            WasFirstPersonalBest = true,
            Position = 1,
            LevelDecayedPoints = 100,
            PlayerDecayedPoints = 50
        });

        Assert.StartsWith("[GTR] You set the new WR", result);
        Assert.Contains($"entered the level leaderboard at {TextColour.Pink.Wrap("#1")}", result);
    }

    [Fact]
    public void WorldRecordHeadlineSuppressesNextTarget()
    {
        string result = RecordFeedbackFormatter.Format(new RecordFeedbackMessageData
        {
            Kind = RecordFeedbackKind.NewWorldRecord,
            NextDelta = "00:00.001"
        });

        Assert.Equal("[GTR] You set the new WR", result);
    }

    [Fact]
    public void ConfigPolicySeparatesMessageKinds()
    {
        Assert.False(RecordFeedbackFormatter.ShouldShow(RecordFeedbackKind.PersonalBest, false, true, true));
        Assert.False(RecordFeedbackFormatter.ShouldShow(RecordFeedbackKind.NewWorldRecord, true, false, true));
        Assert.False(RecordFeedbackFormatter.ShouldShow(RecordFeedbackKind.ImprovedWorldRecord, true, true, false));
    }
}
