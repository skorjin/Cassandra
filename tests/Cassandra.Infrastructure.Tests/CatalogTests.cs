using System.Net;
using Cassandra.Infrastructure.Models;

namespace Cassandra.Infrastructure.Tests;

public class CatalogTests
{
    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond());
    }

    private static HttpClient Client(Func<HttpResponseMessage> respond) => new(new StubHandler(respond));

    [Fact]
    public async Task Embedded_catalog_has_both_variants_with_hashes()
    {
        var variants = await new ModelCatalogLoader(remoteUrl: null).LoadAsync(default);

        Assert.Contains(variants, v => v.Id == "fast");
        Assert.Contains(variants, v => v.Id == "accurate");
        Assert.All(variants, v =>
        {
            Assert.Equal(14, v.Files.Count);
            Assert.All(v.Files, f => Assert.Equal(64, f.Sha256.Length));
        });
        Assert.InRange(variants.Single(v => v.Id == "fast").TotalBytes, 200_000_000, 240_000_000);
    }

    [Fact]
    public async Task Offline_falls_back_to_the_embedded_catalog()
    {
        var client = Client(() => throw new HttpRequestException("no network"));

        var variants = await new ModelCatalogLoader("https://example.invalid/models.json", client).LoadAsync(default);

        Assert.NotEmpty(variants);
    }

    [Fact]
    public async Task Garbage_from_the_server_is_ignored()
    {
        var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>captive portal</html>") });

        var variants = await new ModelCatalogLoader("https://example.invalid/models.json", client).LoadAsync(default);

        Assert.Contains(variants, v => v.Id == "fast");
    }

    [Fact]
    public async Task A_valid_remote_catalog_wins()
    {
        const string json = """
            { "schema": 1, "variants": [ { "id": "x", "name": "X", "precision": "int8", "description": "d", "version": 3,
              "files": [ { "path": "en-ru/a.bin", "url": "https://example.com/a.bin", "size": 10,
                           "sha256": "0000000000000000000000000000000000000000000000000000000000000000" } ] } ] }
            """;
        var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

        var variants = await new ModelCatalogLoader("https://example.invalid/models.json", client).LoadAsync(default);

        var only = Assert.Single(variants);
        Assert.Equal("x", only.Id);
        Assert.Equal(3, only.Version);
    }

    [Fact]
    public async Task Remote_catalog_with_insecure_urls_is_rejected()
    {
        const string json = """
            { "schema": 1, "variants": [ { "id": "x", "name": "X", "precision": "int8", "description": "d", "version": 1,
              "files": [ { "path": "a.bin", "url": "http://example.com/a.bin", "size": 10,
                           "sha256": "0000000000000000000000000000000000000000000000000000000000000000" } ] } ] }
            """;
        var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

        var variants = await new ModelCatalogLoader("https://example.invalid/models.json", client).LoadAsync(default);

        Assert.Contains(variants, v => v.Id == "fast");
    }
}
