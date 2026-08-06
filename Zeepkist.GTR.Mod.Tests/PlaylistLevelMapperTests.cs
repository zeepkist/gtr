using TNRD.Zeepkist.GTR.LevelBrowser;
using TNRD.Zeepkist.GTR.PlaylistBrowserHost;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class PlaylistLevelMapperTests
{
    [Fact]
    public void MapsSelectionToTheFourPlaylistMetadataFields()
    {
        var selection = new LevelBrowserSelection("file-uid", 123456UL, "Cool Level", "MattFile");

        PlaylistLevelMetadata metadata = PlaylistLevelMapper.ToPlaylistMetadata(selection);

        Assert.Equal("file-uid", metadata.Uid);
        Assert.Equal(123456UL, metadata.WorkshopId);
        Assert.Equal("Cool Level", metadata.Name);
        Assert.Equal("MattFile", metadata.Author);
    }

    [Fact]
    public void LeavesCollaboratorsAndOverrideEmptyByDesign()
    {
        var selection = new LevelBrowserSelection("uid", 1UL, "Name", "Author");

        PlaylistLevelMetadata metadata = PlaylistLevelMapper.ToPlaylistMetadata(selection);

        Assert.Equal(string.Empty, metadata.Collaborators);
        Assert.Equal(string.Empty, metadata.OverrideAuthorName);
    }

    [Fact]
    public void AdventureLevelWorkshopIdZeroIsPreserved()
    {
        var selection = new LevelBrowserSelection("adv-uid", 0UL, "Adventure", "Author");

        PlaylistLevelMetadata metadata = PlaylistLevelMapper.ToPlaylistMetadata(selection);

        Assert.Equal(0UL, metadata.WorkshopId);
        Assert.Equal("adv-uid", metadata.Uid);
    }
}
