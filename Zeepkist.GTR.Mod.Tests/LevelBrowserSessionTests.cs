using System.Collections.Generic;
using TNRD.Zeepkist.GTR.LevelBrowser;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LevelBrowserSessionTests
{
    private static LevelBrowserSelection Pick(string uid = "uid-1") =>
        new(uid, 42UL, "Cool Level", "Author");

    [Fact]
    public void OpenStartsSessionWithCallbackAndAlreadyInSet()
    {
        var session = new LevelBrowserSession();

        session.Open(_ => { }, new[] { "in-a", "in-b" });

        Assert.True(session.IsOpen);
        Assert.True(session.IsAlreadyIn("in-a"));
        Assert.True(session.IsAlreadyIn("in-b"));
        Assert.False(session.IsAlreadyIn("free"));
    }

    [Fact]
    public void OpenWithoutAlreadyInSetTreatsEverythingAsFree()
    {
        var session = new LevelBrowserSession();

        session.Open(_ => { });

        Assert.False(session.IsAlreadyIn("anything"));
    }

    [Fact]
    public void ConfirmingPickInvokesCallbackOnceWithSelection()
    {
        var session = new LevelBrowserSession();
        var received = new List<LevelBrowserSelection>();
        session.Open(received.Add);

        LevelBrowserSelection pick = Pick();
        SelectionOutcome outcome = session.TrySelect(pick);

        Assert.Equal(SelectionOutcome.Selected, outcome);
        Assert.Single(received);
        Assert.Same(pick, received[0]);
    }

    [Fact]
    public void SelectionCarriesAllFourFields()
    {
        var session = new LevelBrowserSession();
        LevelBrowserSelection received = null;
        session.Open(s => received = s);

        session.TrySelect(new LevelBrowserSelection("file-uid", 99UL, "Name", "FileAuthor"));

        Assert.NotNull(received);
        Assert.Equal("file-uid", received.FileUid);
        Assert.Equal(99UL, received.WorkshopId);
        Assert.Equal("Name", received.Name);
        Assert.Equal("FileAuthor", received.FileAuthor);
    }

    [Fact]
    public void SelectingAlreadyInRowEmitsNoSelection()
    {
        var session = new LevelBrowserSession();
        var received = new List<LevelBrowserSelection>();
        session.Open(received.Add, new[] { "taken" });

        SelectionOutcome outcome = session.TrySelect(Pick("taken"));

        Assert.Equal(SelectionOutcome.AlreadyIn, outcome);
        Assert.Empty(received);
    }

    [Fact]
    public void HostPushedUidSetUpdatesAlreadyInMarking()
    {
        var session = new LevelBrowserSession();
        session.Open(_ => { }, new[] { "old" });

        session.UpdateAlreadyIn(new[] { "new-1", "new-2" });

        Assert.False(session.IsAlreadyIn("old"));
        Assert.True(session.IsAlreadyIn("new-1"));
        Assert.True(session.IsAlreadyIn("new-2"));
    }

    [Fact]
    public void UpdateAlreadyInThenSelectBlocksTheNewlyAddedRow()
    {
        var session = new LevelBrowserSession();
        var received = new List<LevelBrowserSelection>();
        session.Open(received.Add);

        Assert.Equal(SelectionOutcome.Selected, session.TrySelect(Pick("uid")));
        session.UpdateAlreadyIn(new[] { "uid" });
        Assert.Equal(SelectionOutcome.AlreadyIn, session.TrySelect(Pick("uid")));

        Assert.Single(received);
    }

    [Fact]
    public void OpeningWhileOpenReplacesCallbackAndAlreadyInSet()
    {
        var session = new LevelBrowserSession();
        var first = new List<LevelBrowserSelection>();
        var second = new List<LevelBrowserSelection>();
        session.Open(first.Add, new[] { "a" });

        session.Open(second.Add, new[] { "b" });
        session.TrySelect(Pick("free"));

        Assert.Empty(first);
        Assert.Single(second);
        Assert.False(session.IsAlreadyIn("a"));
        Assert.True(session.IsAlreadyIn("b"));
    }

    [Fact]
    public void OpeningWhileOpenResetsDiscoveryUiState()
    {
        var session = new LevelBrowserSession();
        session.Open(_ => { });
        session.SearchName = "rock";
        session.SearchAuthor = "matt";
        session.AuthorUserId = "76561198111111111";
        session.AuthorUserName = "Uploader";
        session.Sort = LevelBrowseSort.NameAsc;
        session.DateRange = LevelBrowseDateRange.PastWeek;
        session.TrackLength = LevelBrowseTrackLength.Long;
        session.Rating = LevelBrowseRating.TopRated;
        session.OwnLevelsOnly = true;
        session.WithoutMyPersonalBest = true;
        session.WithoutRecords = true;
        session.Page = 4;

        session.Open(_ => { });

        Assert.Equal(string.Empty, session.SearchName);
        Assert.Equal(string.Empty, session.SearchAuthor);
        Assert.Equal(string.Empty, session.AuthorUserId);
        Assert.Equal(string.Empty, session.AuthorUserName);
        Assert.Equal(LevelBrowseSort.Newest, session.Sort);
        Assert.Equal(LevelBrowseDateRange.AnyTime, session.DateRange);
        Assert.Equal(LevelBrowseTrackLength.Any, session.TrackLength);
        Assert.Equal(LevelBrowseRating.Any, session.Rating);
        Assert.False(session.OwnLevelsOnly);
        Assert.False(session.WithoutMyPersonalBest);
        Assert.False(session.WithoutRecords);
        Assert.Equal(0, session.Page);
    }

    [Fact]
    public void CloseByUserResetsAuthorUserFields()
    {
        var session = new LevelBrowserSession();
        session.Open(_ => { });
        session.AuthorUserId = "76561198111111111";
        session.AuthorUserName = "Uploader";

        session.CloseByUser();

        Assert.Equal(string.Empty, session.AuthorUserId);
        Assert.Equal(string.Empty, session.AuthorUserName);
    }

    [Fact]
    public void CloseByUserEndsSessionAndNotifiesHost()
    {
        var session = new LevelBrowserSession();
        var closed = 0;
        session.Open(_ => { }, onClosed: () => closed++);

        session.CloseByUser();

        Assert.False(session.IsOpen);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void CloseByUserIsIdempotent()
    {
        var session = new LevelBrowserSession();
        var closed = 0;
        session.Open(_ => { }, onClosed: () => closed++);

        session.CloseByUser();
        session.CloseByUser();

        Assert.Equal(1, closed);
    }

    [Fact]
    public void CloseByHostEndsSessionWithoutNotifyingHost()
    {
        var session = new LevelBrowserSession();
        var closed = 0;
        session.Open(_ => { }, onClosed: () => closed++);

        session.CloseByHost();

        Assert.False(session.IsOpen);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void SelectAfterCloseDoesNothing()
    {
        var session = new LevelBrowserSession();
        var received = new List<LevelBrowserSelection>();
        session.Open(received.Add);
        session.CloseByUser();

        SelectionOutcome outcome = session.TrySelect(Pick());

        Assert.Equal(SelectionOutcome.NotOpen, outcome);
        Assert.Empty(received);
    }

    [Fact]
    public void UpdateAlreadyInAfterCloseIsIgnored()
    {
        var session = new LevelBrowserSession();
        session.Open(_ => { });
        session.CloseByHost();

        session.UpdateAlreadyIn(new[] { "x" });

        Assert.False(session.IsAlreadyIn("x"));
    }
}
