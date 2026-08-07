using Aimon.Infrastructure.Persistence;
using Aimon.Infrastructure.Secrets;
using Aimon.Infrastructure.Tests.TestSupport;

namespace Aimon.Infrastructure.Tests;

public class DpapiSecretStoreTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static DpapiSecretStore CreateStore(TempDirectory root)
        => new(new SecretStoreOptions { FilePath = Path.Combine(root.Root, "secrets.json") });

    [Fact]
    public async Task Set_Then_Get_Returns_The_Secret()
    {
        using var root = new TempDirectory();
        var store = CreateStore(root);

        await store.SetAsync("openai", "sk-example-123", Ct);

        Assert.Equal("sk-example-123", await store.GetAsync("openai", Ct));
    }

    [Fact]
    public async Task Get_Returns_Null_For_An_Unknown_Key()
    {
        using var root = new TempDirectory();
        var store = CreateStore(root);

        Assert.Null(await store.GetAsync("missing", Ct));
    }

    [Fact]
    public async Task Set_Overwrites_A_Previous_Value()
    {
        using var root = new TempDirectory();
        var store = CreateStore(root);

        await store.SetAsync("openai", "first", Ct);
        await store.SetAsync("openai", "second", Ct);

        Assert.Equal("second", await store.GetAsync("openai", Ct));
    }

    [Fact]
    public async Task Contains_And_Delete_Behave()
    {
        using var root = new TempDirectory();
        var store = CreateStore(root);

        await store.SetAsync("openai", "sk-example-123", Ct);
        Assert.True(await store.ContainsAsync("openai", Ct));

        await store.DeleteAsync("openai", Ct);
        Assert.False(await store.ContainsAsync("openai", Ct));
    }

    [Fact]
    public async Task Secrets_File_Never_Contains_The_Plaintext()
    {
        using var root = new TempDirectory();
        var secretsPath = Path.Combine(root.Root, "secrets.json");
        var store = new DpapiSecretStore(new SecretStoreOptions { FilePath = secretsPath });

        await store.SetAsync("openai", "sk-super-secret-key", Ct);

        var fileContents = await File.ReadAllTextAsync(secretsPath, Ct);
        Assert.DoesNotContain("sk-super-secret-key", fileContents);
    }

    [Fact]
    public async Task Api_Key_Is_Not_Written_Into_Any_Profile_File()
    {
        using var profilesRoot = new TempDirectory();
        using var secretsRoot = new TempDirectory();

        var profileStore = new FileProfileStore(new ProfileStoreOptions { RootDirectory = profilesRoot.Root });
        var secretStore = new DpapiSecretStore(new SecretStoreOptions { FilePath = Path.Combine(secretsRoot.Root, "secrets.json") });
        var profile = TestProfiles.CreateSample();

        await profileStore.SaveAsync(profile, Ct);
        await secretStore.SetAsync("openai", "sk-super-secret-key", Ct);

        foreach (var file in Directory.EnumerateFiles(profilesRoot.Root, "*", SearchOption.AllDirectories))
        {
            var contents = await File.ReadAllTextAsync(file, Ct);
            Assert.DoesNotContain("sk-super-secret-key", contents);
        }
    }
}
