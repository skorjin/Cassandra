using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Cassandra.Domain.Abstractions;
using Cassandra.Domain.Models;

namespace Cassandra.Infrastructure.Models;

/// <summary>
/// Downloads model files over HTTP with resume (Range requests), stall detection and SHA-256 verification.
/// Everything received is kept as "*.part" so a dropped connection, a closed app or a reboot costs nothing.
/// </summary>
public sealed class HttpModelDownloader : IModelDownloader
{
    private const long SafetyMarginBytes = 50L * 1024 * 1024;
    private const int BufferSize = 128 * 1024;

    private readonly ModelStorage _storage;
    private readonly HttpClient _client;
    private readonly TimeSpan _stallTimeout;

    public HttpModelDownloader(ModelStorage storage, HttpClient? client = null, TimeSpan? stallTimeout = null)
    {
        _storage = storage;
        _stallTimeout = stallTimeout ?? TimeSpan.FromSeconds(30);
        _client = client ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        if (_client.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Cassandra-Overlay/1.0");
        }
    }

    public async Task DownloadAsync(ModelVariant variant, IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        var staging = _storage.StagingDirectory(variant);
        Directory.CreateDirectory(staging);

        EnsureDiskSpace(variant, staging);

        var finished = 0L;
        var reporter = new Throttle(progress, variant.TotalBytes);
        reporter.Report(ModelStorage.DirectoryBytes(staging), null, force: true);

        foreach (var file in variant.Files)
        {
            ct.ThrowIfCancellationRequested();
            var target = SafeCombine(staging, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (!(File.Exists(target) && new FileInfo(target).Length == file.Size))
            {
                await DownloadFileAsync(file, target, finished, reporter, ct).ConfigureAwait(false);
            }

            finished += file.Size;
            reporter.Report(finished, file.Path, force: true);
        }

        _storage.Commit(variant);
    }

    private async Task DownloadFileAsync(ModelFile file, string target, long finishedBefore, Throttle reporter, CancellationToken ct)
    {
        var part = target + ".part";
        var restarted = false;

        while (true)
        {
            var offset = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (offset > file.Size)
            {
                File.Delete(part);
                offset = 0;
            }

            if (offset < file.Size)
            {
                var ok = await CopyAsync(file, part, offset, finishedBefore, reporter, ct).ConfigureAwait(false);
                if (!ok && !restarted)
                {
                    restarted = true; // the server could not resume from our offset; begin this file again
                    File.Delete(part);
                    continue;
                }
            }

            if (!await HashMatchesAsync(part, file.Sha256, ct).ConfigureAwait(false))
            {
                File.Delete(part);
                throw new ModelCorruptException(file.Path);
            }

            File.Move(part, target, overwrite: true);
            return;
        }
    }

    /// <returns>False if the server rejected the requested range.</returns>
    private async Task<bool> CopyAsync(ModelFile file, string part, long offset, long finishedBefore, Throttle reporter, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
        if (offset > 0)
        {
            request.Headers.Range = new RangeHeaderValue(offset, null);
        }

        using var response = await SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        var resumed = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (offset > 0 && !resumed)
        {
            offset = 0; // the server ignored Range and sends the whole file
        }

        await using var network = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var disk = new FileStream(part, resumed ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, useAsync: true);

        var buffer = new byte[BufferSize];
        var received = offset;
        while (true)
        {
            var read = await ReadWithStallTimeoutAsync(network, buffer, ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await disk.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;
            reporter.Report(finishedBefore + received, file.Path, force: false);
        }

        await disk.FlushAsync(ct).ConfigureAwait(false);
        if (received < file.Size)
        {
            throw new IOException("Соединение закрыто до окончания файла.");
        }

        return true;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stall.CancelAfter(_stallTimeout);
        try
        {
            return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stall.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Сервер не отвечает.");
        }
    }

    private async Task<int> ReadWithStallTimeoutAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stall.CancelAfter(_stallTimeout);
        try
        {
            return await stream.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Данные перестали поступать.");
        }
    }

    private static async Task<bool> HashMatchesAsync(string path, string expectedHex, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return string.Equals(Convert.ToHexString(hash), expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureDiskSpace(ModelVariant variant, string staging)
    {
        var needed = Math.Max(0, variant.TotalBytes - ModelStorage.DirectoryBytes(staging)) + SafetyMarginBytes;
        var root = Path.GetPathRoot(Path.GetFullPath(_storage.Root));
        if (root is null)
        {
            return;
        }

        var free = new DriveInfo(root).AvailableFreeSpace;
        if (free < needed)
        {
            throw new InsufficientDiskSpaceException(needed - free);
        }
    }

    /// <summary>Joins a catalog path onto the directory and refuses anything that escapes it.</summary>
    private static string SafeCombine(string directory, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(directory, relative));
        if (!full.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Недопустимый путь файла в каталоге: {relative}");
        }

        return full;
    }

    private sealed class Throttle(IProgress<DownloadProgress>? progress, long total)
    {
        private long _lastTicks;

        public void Report(long downloaded, string? file, bool force)
        {
            if (progress is null)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (!force && now - _lastTicks < 100)
            {
                return;
            }

            _lastTicks = now;
            progress.Report(new DownloadProgress(Math.Min(downloaded, total), total, file));
        }
    }
}
