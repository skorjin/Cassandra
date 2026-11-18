using System.Net;
using System.Security.Cryptography;
using Cassandra.Domain.Models;
using Cassandra.Infrastructure.Models;

namespace Cassandra.Infrastructure.Tests;

/// <summary>Tiny HTTP server that can resume, ignore Range, drop the connection, stall or serve corrupt data.</summary>
internal sealed class TestServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, byte[]> _files = new();
    private readonly CancellationTokenSource _stop = new();

    public TestServer()
    {
        for (var port = 18100; ; port++)
        {
            try
            {
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                BaseUrl = $"http://localhost:{port}";
                break;
            }
            catch (HttpListenerException)
            {
                _listener.Prefixes.Clear();
            }
        }

        _ = Task.Run(Loop);
    }

    public string BaseUrl { get; }
    public bool IgnoreRange { get; set; }
    public int AbortFirstRequestsAfterBytes { get; set; } = -1;
    public int AbortRequests { get; set; }
    public bool StallAfterFirstChunk { get; set; }
    public bool Corrupt { get; set; }
    public List<string?> RangeHeaders { get; } = [];

    public void Add(string path, byte[] content) => _files[path] = content;

    private async Task Loop()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                return;
            }

            _ = Task.Run(() => Handle(ctx));
        }
    }

    private async Task Handle(HttpListenerContext ctx)
    {
        try
        {
            if (!_files.TryGetValue(ctx.Request.Url!.AbsolutePath, out var content))
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                return;
            }

            var range = ctx.Request.Headers["Range"];
            RangeHeaders.Add(range);

            long start = 0;
            if (range is not null && !IgnoreRange)
            {
                start = long.Parse(range.Replace("bytes=", "").TrimEnd('-'));
                if (start >= content.Length)
                {
                    ctx.Response.StatusCode = 416;
                    ctx.Response.Close();
                    return;
                }

                ctx.Response.StatusCode = 206;
                ctx.Response.Headers["Content-Range"] = $"bytes {start}-{content.Length - 1}/{content.Length}";
            }

            var body = content[(int)start..];
            if (Corrupt)
            {
                body = (byte[])body.Clone();
                body[body.Length / 2] ^= 0xFF;
            }

            ctx.Response.ContentLength64 = body.Length;

            if (AbortRequests > 0 && AbortFirstRequestsAfterBytes >= 0)
            {
                AbortRequests--;
                await ctx.Response.OutputStream.WriteAsync(body.AsMemory(0, Math.Min(AbortFirstRequestsAfterBytes, body.Length)));
                await ctx.Response.OutputStream.FlushAsync();
                await Task.Delay(100);
                ctx.Response.Abort();
                return;
            }

            if (StallAfterFirstChunk)
            {
                await ctx.Response.OutputStream.WriteAsync(body.AsMemory(0, Math.Min(1000, body.Length)));
                await ctx.Response.OutputStream.FlushAsync();
                await Task.Delay(TimeSpan.FromSeconds(10), _stop.Token);
                return;
            }

            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
        catch
        {
            // client went away
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Close();
    }
}

public sealed class ModelDownloaderTests : IDisposable
{
    private readonly TestServer _server = new();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cassandra-dl-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _a = Random(300_000, 1);
    private readonly byte[] _b = Random(200_000, 2);

    private static byte[] Random(int size, int seed)
    {
        var bytes = new byte[size];
        new System.Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    public ModelDownloaderTests()
    {
        _server.Add("/a.bin", _a);
        _server.Add("/b.bin", _b);
    }

    private ModelVariant Variant(int version = 1) => new("test", "Test", "int8", "d", version,
    [
        new ModelFile("en-ru/a.bin", _server.BaseUrl + "/a.bin", _a.Length, Hash(_a)),
        new ModelFile("en-ru/b.bin", _server.BaseUrl + "/b.bin", _b.Length, Hash(_b)),
    ]);

    private (ModelStorage Storage, HttpModelDownloader Downloader) Create(TimeSpan? stall = null)
    {
        var storage = new ModelStorage(_root);
        return (storage, new HttpModelDownloader(storage, stallTimeout: stall));
    }

    [Fact]
    public async Task Downloads_verifies_and_installs()
    {
        var (storage, downloader) = Create();
        var variant = Variant();
        var reports = new List<DownloadProgress>();

        await downloader.DownloadAsync(variant, new Progress<DownloadProgress>(reports.Add), default);
        await Task.Delay(50);

        var dir = storage.GetInstalledDirectory("test")!;
        Assert.Equal(_a, await File.ReadAllBytesAsync(Path.Combine(dir, "en-ru", "a.bin")));
        Assert.Equal(_b, await File.ReadAllBytesAsync(Path.Combine(dir, "en-ru", "b.bin")));
        Assert.Empty(Directory.GetFiles(_root, "*.part", SearchOption.AllDirectories));
        Assert.Equal(ModelState.Installed, storage.GetStatuses([variant])[0].State);
        Assert.Equal(variant.TotalBytes, reports.Max(r => r.DownloadedBytes));
    }

    [Fact]
    public async Task Resumes_a_partial_file_with_a_range_request()
    {
        var (storage, downloader) = Create();
        var variant = Variant();
        var staging = storage.StagingDirectory(variant);
        Directory.CreateDirectory(Path.Combine(staging, "en-ru"));
        await File.WriteAllBytesAsync(Path.Combine(staging, "en-ru", "a.bin.part"), _a[..120_000]);

        await downloader.DownloadAsync(variant, null, default);

        Assert.Contains("bytes=120000-", _server.RangeHeaders);
        var dir = storage.GetInstalledDirectory("test")!;
        Assert.Equal(_a, await File.ReadAllBytesAsync(Path.Combine(dir, "en-ru", "a.bin")));
    }

    [Fact]
    public async Task Dropped_connection_keeps_received_bytes_and_next_attempt_resumes()
    {
        var (storage, downloader) = Create();
        var variant = Variant();
        _server.AbortRequests = 1;
        _server.AbortFirstRequestsAfterBytes = 100_000;

        await Assert.ThrowsAnyAsync<Exception>(() => downloader.DownloadAsync(variant, null, default));

        var status = storage.GetStatuses([variant])[0];
        Assert.Equal(ModelState.Partial, status.State);
        Assert.True(status.DownloadedBytes > 0);

        await downloader.DownloadAsync(variant, null, default);

        Assert.Equal(ModelState.Installed, storage.GetStatuses([variant])[0].State);
        Assert.NotNull(_server.RangeHeaders.FirstOrDefault(h => h is not null));
        var dir = storage.GetInstalledDirectory("test")!;
        Assert.Equal(_a, await File.ReadAllBytesAsync(Path.Combine(dir, "en-ru", "a.bin")));
    }

    [Fact]
    public async Task Server_that_ignores_range_makes_the_file_restart_cleanly()
    {
        var (storage, downloader) = Create();
        var variant = Variant();
        var staging = storage.StagingDirectory(variant);
        Directory.CreateDirectory(Path.Combine(staging, "en-ru"));
        await File.WriteAllBytesAsync(Path.Combine(staging, "en-ru", "a.bin.part"), _a[..50_000]);
        _server.IgnoreRange = true;

        await downloader.DownloadAsync(variant, null, default);

        var dir = storage.GetInstalledDirectory("test")!;
        Assert.Equal(_a, await File.ReadAllBytesAsync(Path.Combine(dir, "en-ru", "a.bin")));
    }

    [Fact]
    public async Task Corrupt_data_is_rejected_and_nothing_is_installed()
    {
        var (storage, downloader) = Create();
        var variant = Variant();
        _server.Corrupt = true;

        await Assert.ThrowsAsync<ModelCorruptException>(() => downloader.DownloadAsync(variant, null, default));

        Assert.Null(storage.GetInstalledDirectory("test"));
        Assert.Empty(Directory.GetFiles(_root, "*.part", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Stalled_connection_times_out()
    {
        var (_, downloader) = Create(TimeSpan.FromMilliseconds(400));
        _server.StallAfterFirstChunk = true;

        await Assert.ThrowsAsync<TimeoutException>(() => downloader.DownloadAsync(Variant(), null, default));
    }

    [Fact]
    public async Task Cancelling_stops_the_download_and_keeps_the_partial_file()
    {
        var (storage, downloader) = Create();
        _server.StallAfterFirstChunk = true;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => downloader.DownloadAsync(Variant(), null, cts.Token));

        Assert.Equal(ModelState.Partial, storage.GetStatuses([Variant()])[0].State);
    }

    [Fact]
    public async Task Missing_disk_space_fails_before_downloading()
    {
        var (_, downloader) = Create();
        var huge = Variant() with { Files = [new ModelFile("en-ru/a.bin", _server.BaseUrl + "/a.bin", long.MaxValue / 4, Hash(_a))] };

        await Assert.ThrowsAsync<InsufficientDiskSpaceException>(() => downloader.DownloadAsync(huge, null, default));
        Assert.Empty(_server.RangeHeaders);
    }

    [Fact]
    public async Task Catalog_paths_cannot_escape_the_model_directory()
    {
        var (_, downloader) = Create();
        var evil = Variant() with { Files = [new ModelFile("../../evil.bin", _server.BaseUrl + "/a.bin", _a.Length, Hash(_a))] };

        await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(evil, null, default));
    }

    [Fact]
    public async Task A_newer_version_installs_next_to_the_old_one_and_replaces_it()
    {
        var (storage, downloader) = Create();
        await downloader.DownloadAsync(Variant(1), null, default);
        var v1 = storage.GetInstalledDirectory("test");

        Assert.Equal(ModelState.UpdateAvailable, storage.GetStatuses([Variant(2)])[0].State);

        await downloader.DownloadAsync(Variant(2), null, default);

        var v2 = storage.GetInstalledDirectory("test");
        Assert.NotEqual(v1, v2);
        Assert.EndsWith("v2", v2);
        Assert.Equal(ModelState.Installed, storage.GetStatuses([Variant(2)])[0].State);
    }

    [Fact]
    public async Task Delete_removes_everything()
    {
        var (storage, downloader) = Create();
        await downloader.DownloadAsync(Variant(), null, default);

        await storage.DeleteAsync("test");

        Assert.Null(storage.GetInstalledDirectory("test"));
        Assert.Equal(ModelState.NotInstalled, storage.GetStatuses([Variant()])[0].State);
    }

    public void Dispose()
    {
        _server.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
