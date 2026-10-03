using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AetherBoy.Runtime;

public enum ReleaseUpdateState { Idle, Checking, NoReleases, NoNewerRelease, NoPackage, Available, Downloading, Downloaded, Cancelled, Failed }
public enum ReleaseUpdateError { None, Network, RateLimit, Timeout, InvalidCatalog, CatalogLimit, UnsupportedRuntime, UnknownVersion, Integrity, Disk, UnsafeRedirect }
public sealed record ReleaseUpdateSnapshot(ReleaseUpdateState State = ReleaseUpdateState.Idle,
    string? Version = null, string? PackageName = null, long PackageSize = 0, long ReceivedBytes = 0,
    string? DownloadPath = null, ReleaseUpdateError Error = ReleaseUpdateError.None)
{
    public bool Busy => State is ReleaseUpdateState.Checking or ReleaseUpdateState.Downloading;
}

/// <summary>
/// Public GitHub releases only. Never extracts, executes, overwrites the application or accesses game data.
/// SHA-256 validates the download against GitHub metadata, not an independent publisher signature.
/// Both frontends poll the same immutable status; workers never call UI or emulator code.
/// </summary>
public sealed class ReleaseUpdateService : IDisposable
{
    public const string ReleasesPage = "https://github.com/VoltexModz/AetherBoy/releases";
    private const string Api = "https://api.github.com/repos/VoltexModz/AetherBoy/releases";
    private const long MaxPackageSize = 1024L * 1024 * 1024;
    private const int MaxCatalogBytes = 2 * 1024 * 1024;
    private const int PageSize = 100, MaxPages = 5;
    private readonly object sync = new();
    private readonly HttpClient client;
    private readonly string directory;
    private CancellationTokenSource? operation;
    private bool disposed;
    private ReleaseUpdateSnapshot snapshot = new();
    private Candidate? candidate;
    private sealed record Candidate(ReleaseVersion Version, string Name, long Size, string Sha256, Uri Url);
    private sealed class UpdateFailure(ReleaseUpdateError error) : Exception { public ReleaseUpdateError Error { get; } = error; }

    public string CurrentVersion { get; }
    public string RuntimeIdentifier { get; }
    public bool IncludePrereleases { get; }
    public string DownloadDirectory => directory;
    public ReleaseUpdateSnapshot Snapshot => Volatile.Read(ref snapshot);

    public static string CurrentRuntimeIdentifier => (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), RuntimeInformation.ProcessArchitecture) switch
    {
        (true, _, Architecture.X64) => "win-x64",
        (_, true, Architecture.X64) => "linux-x64",
        (_, true, Architecture.Arm64) => "linux-arm64",
        _ => "unsupported"
    };

    public ReleaseUpdateService(string currentVersion, bool includePrereleases, string downloadDirectory)
        : this(currentVersion, CurrentRuntimeIdentifier, includePrereleases, downloadDirectory,
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan }) { }

    internal ReleaseUpdateService(string currentVersion, string runtimeIdentifier, bool includePrereleases, string downloadDirectory, HttpClient client)
    {
        CurrentVersion = currentVersion; RuntimeIdentifier = runtimeIdentifier; IncludePrereleases = includePrereleases;
        directory = Path.GetFullPath(downloadDirectory); this.client = client;
    }

    public Task CheckAsync() => Start(download: false);
    public Task DownloadAsync() => Start(download: true);

    private Task Start(bool download)
    {
        lock (sync)
        {
            if (disposed || operation is not null) return Task.CompletedTask;
            if (download && (snapshot.State != ReleaseUpdateState.Available || candidate is null)) return Task.CompletedTask;
            operation = new CancellationTokenSource();
            snapshot = download ? snapshot with { State = ReleaseUpdateState.Downloading, ReceivedBytes = 0 }
                : new(ReleaseUpdateState.Checking);
            if (!download) candidate = null;
            var source = operation;
            return Task.Run(() => RunAsync(download, source));
        }
    }

    public void Cancel() { lock (sync) operation?.Cancel(); }

    private async Task RunAsync(bool download, CancellationTokenSource source)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(source.Token);
        timeout.CancelAfter(download ? TimeSpan.FromMinutes(15) : TimeSpan.FromSeconds(60));
        try
        {
            if (download) await DownloadCoreAsync(candidate!, timeout.Token).ConfigureAwait(false);
            else await CheckCoreAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        { Set(Snapshot with { State = source.IsCancellationRequested ? ReleaseUpdateState.Cancelled : ReleaseUpdateState.Failed, Error = source.IsCancellationRequested ? ReleaseUpdateError.None : ReleaseUpdateError.Timeout }); }
        catch (UpdateFailure e) { Fail(e.Error); }
        catch (HttpRequestException) { Fail(ReleaseUpdateError.Network); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { Fail(ReleaseUpdateError.InvalidCatalog); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Fail(download ? ReleaseUpdateError.Disk : ReleaseUpdateError.Network); }
        finally
        {
            lock (sync)
            { operation = null; source.Dispose(); if (disposed) client.Dispose(); }
        }
    }

    private void Set(ReleaseUpdateSnapshot value) => Volatile.Write(ref snapshot, value);
    private void Fail(ReleaseUpdateError error) => Set(Snapshot with { State = ReleaseUpdateState.Failed, Error = error, DownloadPath = null });

    private async Task CheckCoreAsync(CancellationToken token)
    {
        if (RuntimeIdentifier is not ("win-x64" or "linux-x64" or "linux-arm64")) throw new UpdateFailure(ReleaseUpdateError.UnsupportedRuntime);
        if (!ReleaseVersion.TryParse(CurrentVersion, out var current)) throw new UpdateFailure(ReleaseUpdateError.UnknownVersion);
        ReleaseVersion? newest = null;
        Candidate? newestPackage = null;
        bool sawRelease = false;
        for (int page = 1; page <= MaxPages; page++)
        {
            using var response = await SendAsync(new Uri($"{Api}?per_page={PageSize}&page={page}"), true, token).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                if (bytes.Length + read > MaxCatalogBytes) throw new UpdateFailure(ReleaseUpdateError.InvalidCatalog);
                bytes.Write(buffer, 0, read);
            }
            using var json = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() > PageSize)
                throw new UpdateFailure(ReleaseUpdateError.InvalidCatalog);
            foreach (var release in json.RootElement.EnumerateArray())
            {
                // Required flags fail closed. Release notes and arbitrary HTML links are not consumed.
                if (release.GetProperty("draft").GetBoolean()) continue;
                bool prerelease = release.GetProperty("prerelease").GetBoolean();
                string? tag = release.GetProperty("tag_name").GetString();
                if (!ReleaseVersion.TryParse(tag, out var version) || (!IncludePrereleases && (prerelease || version.IsPrerelease))) continue;
                sawRelease = true;
                if (version.CompareTo(current) <= 0 || newest is not null && version.CompareTo(newest) < 0) continue;
                // Duplicate semantic versions (v1.0.0 and 1.0.0, or differing build metadata) are ambiguous.
                if (newest is not null && version.CompareTo(newest) == 0) { newestPackage = null; continue; }
                newest = version;
                newestPackage = SelectPackage(release.GetProperty("assets"), tag!, version);
            }
            if (json.RootElement.GetArrayLength() < PageSize) break;
            if (page == MaxPages) throw new UpdateFailure(ReleaseUpdateError.CatalogLimit);
        }
        token.ThrowIfCancellationRequested();
        candidate = newestPackage;
        Set(new(newest is null ? (sawRelease ? ReleaseUpdateState.NoNewerRelease : ReleaseUpdateState.NoReleases)
                : newestPackage is null ? ReleaseUpdateState.NoPackage : ReleaseUpdateState.Available,
            newest?.Text, newestPackage?.Name, newestPackage?.Size ?? 0));
    }

    private Candidate? SelectPackage(JsonElement assets, string tag, ReleaseVersion version)
    {
        if (assets.ValueKind != JsonValueKind.Array) throw new UpdateFailure(ReleaseUpdateError.InvalidCatalog);
        Candidate? found = null;
        int matching = 0;
        foreach (var asset in assets.EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            if (!MatchesPackageName(name, version.Text)) continue;
            matching++;
            if (asset.GetProperty("state").GetString() != "uploaded") continue;
            long size = asset.GetProperty("size").GetInt64();
            string? digest = asset.TryGetProperty("digest", out var hash) && hash.ValueKind == JsonValueKind.String ? hash.GetString() : null;
            if (size <= 0 || size > MaxPackageSize || digest is null || !Regex.IsMatch(digest, @"\Asha256:[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant)) continue;
            var expected = new Uri($"{ReleasesPage}/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name!)}");
            if (!Uri.TryCreate(asset.GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var url)
                || url.UserInfo.Length != 0 || url.Fragment.Length != 0 || url.Query.Length != 0 || url != expected) continue;
            found = new(version, name!, size, digest[7..], expected);
        }
        return matching == 1 ? found : null;
    }

    private bool MatchesPackageName(string? name, string version)
    {
        if (name is null || name.Length > 220 || !Regex.IsMatch(name, @"\AAetherBoy-[A-Za-z0-9.+-]+\z", RegexOptions.CultureInvariant)) return false;
        if (RuntimeIdentifier == "win-x64")
            return name == $"AetherBoy-{version}-win-x64-self-contained.zip" ||
                Regex.IsMatch(name, @"\AAetherBoy-Windows-x64-[a-f0-9]{12}-[0-9]{8}-[0-9]{6}(?:-local)?\.zip\z", RegexOptions.CultureInvariant);
        return name == $"AetherBoy-{version}-{RuntimeIdentifier}-self-contained.tar.gz";
    }

    private async Task<HttpResponseMessage> SendAsync(Uri url, bool metadata, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("AetherBoy-UpdateCheck/1.0");
        if (metadata)
        {
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        }
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (response.IsSuccessStatusCode || !metadata && IsRedirect(response.StatusCode)) return response;
        ReleaseUpdateError error = response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.Forbidden
            && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.Contains("0")
            ? ReleaseUpdateError.RateLimit : ReleaseUpdateError.Network;
        response.Dispose(); throw new UpdateFailure(error);
    }

    private static bool IsRedirect(HttpStatusCode status) => (int)status is 301 or 302 or 303 or 307 or 308;
    internal static bool IsAllowedAssetRedirect(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" && uri.Port == 443
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && uri.OriginalString.Length <= 16384
        && uri.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com";

    private async Task<HttpResponseMessage> OpenPackageAsync(Uri initial, CancellationToken token)
    {
        Uri url = initial;
        for (int count = 0; count < 5; count++)
        {
            var response = await SendAsync(url, false, token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK) return response;
            Uri? location = response.Headers.Location;
            bool redirect = IsRedirect(response.StatusCode);
            response.Dispose();
            if (!redirect || location is null || !Uri.TryCreate(url, location, out var next) || !IsAllowedAssetRedirect(next))
                throw new UpdateFailure(ReleaseUpdateError.UnsafeRedirect);
            url = next;
        }
        throw new UpdateFailure(ReleaseUpdateError.UnsafeRedirect);
    }

    private async Task DownloadCoreAsync(Candidate selected, CancellationToken token)
    {
        using var response = await OpenPackageAsync(selected.Url, token).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is long length && length != selected.Size) throw new UpdateFailure(ReleaseUpdateError.Integrity);
        string staging = Path.Combine(directory, Guid.NewGuid().ToString("N"));
        string path = Path.Combine(staging, selected.Name), partial = path + ".partial";
        try
        {
            Directory.CreateDirectory(staging);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            {
                byte[] buffer = new byte[65536]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    total += read;
                    if (total > selected.Size) throw new UpdateFailure(ReleaseUpdateError.Integrity);
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    Set(Snapshot with { ReceivedBytes = total });
                }
                if (total != selected.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(selected.Sha256)))
                    throw new UpdateFailure(ReleaseUpdateError.Integrity);
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            File.Move(partial, path, overwrite: false);
            Set(Snapshot with { State = ReleaseUpdateState.Downloaded, DownloadPath = path });
        }
        finally
        {
            // Only the exact partial file and empty, uniquely owned staging directory are removed.
            try { File.Delete(partial); Directory.Delete(staging, recursive: false); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            if (operation is not null) operation.Cancel(); else client.Dispose();
        }
    }
}
