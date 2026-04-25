using APConfigManager.Core.Models.Settings;
using APConfigManager.Infrastructure.Data;
using FluentAssertions;

namespace APConfigManager.Infrastructure.Tests.Persistence;

public class DeviceProfileRepositoryTests : IDisposable
{
    private readonly string dbPath;
    private readonly LiteDbContext context;
    private readonly DeviceProfileRepository repository;

    public DeviceProfileRepositoryTests()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        context = new LiteDbContext(dbPath);
        repository = new DeviceProfileRepository(context);
    }

    public void Dispose()
    {
        context.Dispose();

        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }

    private static DeviceProfile CreateProfile(
        string name = "CubeOrange",
        uint boardType = 140,
        string description = "Test profile")
    {
        return new DeviceProfile
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            BoardType = boardType
        };
    }

    [Fact]
    public void GetAll_EmptyRepository_ReturnsEmptyList()
    {
        var result = repository.GetAll();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_MultipleProfiles_ReturnsAll()
    {
        repository.Save(CreateProfile("CubeOrange", 140));
        repository.Save(CreateProfile("CubeBlack", 50));
        repository.Save(CreateProfile("Pixhawk4", 83));

        var result = repository.GetAll();

        result.Should().HaveCount(3);
    }

    [Fact]
    public void Save_ThenGetAll_ReturnsProfile()
    {
        var profile = CreateProfile("CubeOrange", 140);

        repository.Save(profile);
        var result = repository.GetAll();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("CubeOrange");
        result[0].BoardType.Should().Be(140);
    }

    [Fact]
    public void Save_WithParameterFilePath_PersistsPath()
    {
        var profile = CreateProfile();
        profile.ParameterFilePath = @"C:\params\default.param";

        repository.Save(profile);
        var result = repository.GetAll();

        result[0].ParameterFilePath.Should().Be(@"C:\params\default.param");
    }

    [Fact]
    public void Save_WithFirmwareFilePath_PersistsPath()
    {
        var profile = CreateProfile();
        profile.FirmwareFilePath = @"C:\firmware\copter.apj";

        repository.Save(profile);
        var result = repository.GetAll();

        result[0].FirmwareFilePath.Should().Be(@"C:\firmware\copter.apj");
    }

    [Fact]
    public void Save_WithProfileOptions_PersistsOptions()
    {
        var profile = CreateProfile();
        profile.ProfileOptions["firmware"] = true;
        profile.ProfileOptions["parameters"] = true;

        repository.Save(profile);
        var result = repository.GetAll();

        result[0].ProfileOptions["firmware"].Should().BeTrue();
        result[0].ProfileOptions["parameters"].Should().BeTrue();
        result[0].ProfileOptions["bootloader"].Should().BeFalse();
    }

    [Fact]
    public void Save_WithDescription_PersistsDescription()
    {
        var profile = CreateProfile(description: "Main drone autopilot");

        repository.Save(profile);
        var result = repository.GetAll();

        result[0].Description.Should().Be("Main drone autopilot");
    }

    [Fact]
    public void Save_ExistingProfile_Updates()
    {
        var profile = CreateProfile("CubeOrange", 140);
        repository.Save(profile);

        profile.Name = "CubeOrange #2";
        profile.Description = "Updated description";
        profile.FirmwareFilePath = @"C:\new\firmware.apj";
        repository.Save(profile);

        var result = repository.GetAll();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("CubeOrange #2");
        result[0].Description.Should().Be("Updated description");
        result[0].FirmwareFilePath.Should().Be(@"C:\new\firmware.apj");
    }

    [Fact]
    public void GetByBoardType_ExistingType_ReturnsProfile()
    {
        repository.Save(CreateProfile("CubeOrange", 140));
        repository.Save(CreateProfile("CubeBlack", 50));

        var result = repository.GetByBoardType(140);

        result.Should().NotBeNull();
        result!.Name.Should().Be("CubeOrange");
    }

    [Fact]
    public void GetByBoardType_NonExistingType_ReturnsNull()
    {
        repository.Save(CreateProfile("CubeOrange", 140));

        var result = repository.GetByBoardType(999);

        result.Should().BeNull();
    }

    [Fact]
    public void Delete_ExistingProfile_RemovesFromRepository()
    {
        var profile = CreateProfile();
        repository.Save(profile);

        repository.Delete(profile.Id);
        var result = repository.GetAll();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Delete_NonExistingId_DoesNotThrow()
    {
        var act = () => repository.Delete(Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void Delete_OneOfMany_RemovesOnlyThatProfile()
    {
        var profile1 = CreateProfile("CubeOrange", 140);
        var profile2 = CreateProfile("CubeBlack", 50);
        repository.Save(profile1);
        repository.Save(profile2);

        repository.Delete(profile1.Id);
        var result = repository.GetAll();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("CubeBlack");
    }

    [Fact]
    public void Save_NewRepositoryInstance_PersistsData()
    {
        var profile = CreateProfile("CubeOrange", 140);
        repository.Save(profile);

        var secondRepository = new DeviceProfileRepository(context);
        var result = secondRepository.GetAll();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("CubeOrange");
    }
}
