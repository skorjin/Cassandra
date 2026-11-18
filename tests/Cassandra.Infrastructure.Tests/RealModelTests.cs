using Cassandra.Domain.Models;
using Cassandra.Infrastructure.Models;
using Cassandra.Infrastructure.Translation;
using Xunit.Abstractions;

namespace Cassandra.Infrastructure.Tests;

/// <summary>
/// Installs the "fast" variant once through the real catalog and downloader into a persistent test cache
/// (first run downloads about 220 MB from the internet; later runs reuse it).
/// </summary>
public sealed class RealModelFixture : IAsyncLifetime
{
    public ModelStorage Storage { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cassandra.Tests", "models"));

    public string Directory { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var catalog = await new ModelCatalogLoader(remoteUrl: null).LoadAsync(default);
        var fast = catalog.Single(v => v.Id == "fast");

        if (Storage.GetStatuses([fast])[0].State != ModelState.Installed)
        {
            await new HttpModelDownloader(Storage).DownloadAsync(fast, null, default);
        }

        Directory = Storage.GetInstalledDirectory("fast")!;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

public class RealModelTests(RealModelFixture fixture, ITestOutputHelper output) : IClassFixture<RealModelFixture>
{
    [Fact]
    public async Task Downloaded_model_files_match_the_catalog()
    {
        var catalog = await new ModelCatalogLoader(remoteUrl: null).LoadAsync(default);
        var fast = catalog.Single(v => v.Id == "fast");

        foreach (var file in fast.Files)
        {
            var path = Path.Combine(fixture.Directory, file.Path);
            Assert.True(File.Exists(path), path);
            Assert.Equal(file.Size, new FileInfo(path).Length);
        }
    }

    [Theory]
    [InlineData("Enemy sniper on the roof, watch out!", "en", "ru")]
    [InlineData("I need a medic at point B", "en", "ru")]
    [InlineData("gg, that was close", "en", "ru")]
    [InlineData("Принято, иду на крышу, прикрой справа", "ru", "en")]
    [InlineData("Нужен медик на точке Б", "ru", "en")]
    public async Task Translates_with_the_downloaded_model(string text, string from, string to)
    {
        using var translator = new OnnxMarianTranslator(() => fixture.Directory);

        var result = await translator.TranslateAsync(text, new LanguagePair(from, to), default);

        output.WriteLine($"{text}  =>  {result}");
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.NotEqual(text, result);
    }

    [Fact]
    public async Task Missing_model_gives_a_clear_error()
    {
        using var translator = new OnnxMarianTranslator(() => null);

        await Assert.ThrowsAsync<ModelNotInstalledException>(() => translator.TranslateAsync("hello", LanguagePair.EnRu, default));
    }

    [Fact]
    public async Task Switching_the_model_directory_reloads_models()
    {
        var current = fixture.Directory;
        using var translator = new OnnxMarianTranslator(() => current);
        await translator.TranslateAsync("hello", LanguagePair.EnRu, default);

        current = null;

        await Assert.ThrowsAsync<ModelNotInstalledException>(() => translator.TranslateAsync("hello", LanguagePair.EnRu, default));
    }
}
