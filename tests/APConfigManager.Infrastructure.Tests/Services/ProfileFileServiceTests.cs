using APConfigManager.Infrastructure.Services;
using FluentAssertions;

namespace APConfigManager.Infrastructure.Tests.Services;

public class ProfileFileServiceTests
{
    [Fact]
    public void GetCandidatePaths_FileNameOnly_IncludesProfileUploadFolder()
    {
        var profileId = Guid.Parse("4802503e-7add-4d34-94b4-e1c382e261b1");
        var root = @"C:\APConfigManager\profile-files";

        var candidates = ProfileFileService.GetCandidatePaths(
            root,
            profileId,
            "OC+.apj").ToList();

        candidates.Should().Contain(
            Path.Combine(root, profileId.ToString(), "OC+.apj"));
    }

    [Fact]
    public void GetCandidatePaths_AbsolutePath_IncludesOnlyThatPath()
    {
        var profileId = Guid.NewGuid();
        var absolute = @"D:\Firmware\copter.apj";

        var candidates = ProfileFileService.GetCandidatePaths(
            @"C:\APConfigManager\profile-files",
            profileId,
            absolute).ToList();

        candidates.Should().Contain(Path.GetFullPath(absolute));
        candidates.Should().HaveCount(1);
    }
}
