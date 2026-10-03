using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AetherBoy.Runtime;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class ReleaseUpdateTests
{
    private string root = null!;
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("test package, never executed");
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-updates-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    [DataRow("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [DataRow("1.0.0-alpha.2", "1.0.0-alpha.10", -1)]
    [DataRow("1.0.0-alpha.10", "1.0.0-beta", -1)]
    [DataRow("1.0.0-rc.1", "1.0.0", -1)]
    [DataRow("v4.8.0-alpha.1+abc123", "4.8.0-alpha.1+def456", 0)]
    [DataRow("10.0.0", "9.99.999", 1)]
    [DataRow("999999999999999999999999.0.0", "10.0.0", 1)]
    public void VersionsCompareSemanticallyNotByDateOrLexicalOrdering(string a, string b, int expected)
    {
        Assert.IsTrue(ReleaseVersion.TryParse(a, out var av)); Assert.IsTrue(ReleaseVersion.TryParse(b, out var bv));
        Assert.AreEqual(expected, Math.Sign(av.CompareTo(bv)));
    }

    [TestMethod]
    [DataRow("1.0")][DataRow("01.0.0")][DataRow("1.0.0-01")][DataRow("1.0.0\n")]
    [DataRow(" 1.0.0")][DataRow("nightly")][DataRow("1.0.0+../escape")][DataRow("1.0.0-ß")]
    public void InvalidVersionsAreRejected(string version) => Assert.IsFalse(ReleaseVersion.TryParse(version, out _));

    [TestMethod]
    public async Task ServiceIsOfflineUntilExplicitlyCheckedAndRequestsContainNoUserData()
    {
        using var handler = new Handler((request, _) =>
        {
            Assert.AreEqual("https://api.github.com/repos/VoltexModz/AetherBoy/releases?per_page=100&page=1", request.RequestUri!.AbsoluteUri);
            Assert.AreEqual(HttpMethod.Get, request.Method); Assert.IsNull(request.Content); Assert.IsNull(request.Headers.Authorization);
            Assert.IsFalse(request.Headers.Contains("Cookie")); Assert.IsNull(request.Headers.Referrer);
            Assert.AreEqual("AetherBoy-UpdateCheck/1.0", request.Headers.UserAgent.ToString());
            return Task.FromResult(Json());
        });
        using var service = Create(handler);
        Assert.AreEqual(ReleaseUpdateState.Idle, service.Snapshot.State); Assert.AreEqual(0, handler.Calls);
        await service.DownloadAsync(); Assert.AreEqual(0, handler.Calls);
        await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.NoReleases, service.Snapshot.State);
        Assert.AreEqual(0, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public async Task StableExcludesPrereleaseFlagAndPrereleaseVersionAndDrafts()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(
            Release("v8.0.0", draft: true), Release("v7.0.0", pre: true), Release("v6.0.0-beta.1"), Release("v5.0.0"))));
        using var stable = Create(handler, includePrereleases: false);
        await stable.CheckAsync(); Assert.AreEqual("5.0.0", stable.Snapshot.Version); Assert.AreEqual(ReleaseUpdateState.Available, stable.Snapshot.State);
    }

    [TestMethod]
    public async Task DevelopmentAcceptsPrereleasesAndDoesNotOfferSameVersionWithDifferentCommitMetadata()
    {
        using var handler = new Handler((_, _) => Task.FromResult(Json(Release("v5.0.0-alpha.2", pre: true), Release("v4.8.0"))));
        using var dev = Create(handler, current: "5.0.0-alpha.1");
        await dev.CheckAsync(); Assert.AreEqual("5.0.0-alpha.2", dev.Snapshot.Version);
        using var unchanged = Create(new Handler((_, _) => Task.FromResult(Json(Release("v4.8.0-alpha.1+newcommit")))), current: "4.8.0-alpha.1+oldcommit");
        await unchanged.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.NoNewerRelease, unchanged.Snapshot.State);
    }

    [TestMethod]
    [DataRow("win-x64")][DataRow("linux-x64")][DataRow("linux-arm64")]
    public async Task ChoosesMatchingArchitectureAndPackage(string rid)
    {
        object[] assets = [Asset("v5.0.0", "win-x64"), Asset("v5.0.0", "linux-x64"), Asset("v5.0.0", "linux-arm64")];
        using var service = Create(new Handler((_, _) => Task.FromResult(Json(Release("v5.0.0", assets: assets)))), rid: rid);
        await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.Available, service.Snapshot.State);
        StringAssert.Contains(service.Snapshot.PackageName!, rid);
    }

    [TestMethod]
    public async Task ExistingWindowsBuildNamesAreRecognized()
    {
        var asset = Asset("v5.0.0", "win-x64"); SetName(asset, "v5.0.0", "AetherBoy-Windows-x64-abcdef012345-20261002-120001-local.zip");
        using var service = Create(new Handler((_, _) => Task.FromResult(Json(Release("v5.0.0", assets: [asset])))));
        await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.Available, service.Snapshot.State);
    }

    [TestMethod]
    [DataRow("digest")][DataRow("foreignUrl")][DataRow("credentials")][DataRow("size")][DataRow("state")][DataRow("path")][DataRow("ambiguous")]
    public async Task UnverifiableOrAmbiguousAssetsAreNeverOffered(string failure)
    {
        var asset = Asset("v5.0.0", "win-x64");
        switch (failure)
        {
            case "digest": asset["digest"] = null; break;
            case "foreignUrl": asset["browser_download_url"] = "https://example.com/package.zip"; break;
            case "credentials": asset["browser_download_url"] = ((string)asset["browser_download_url"]!).Replace("https://", "https://attacker@"); break;
            case "size": asset["size"] = long.MaxValue; break;
            case "state": asset["state"] = "new"; break;
            case "path": SetName(asset, "v5.0.0", "../../AetherBoy-5.0.0-win-x64-self-contained.zip"); break;
        }
        using var service = Create(new Handler((_, _) => Task.FromResult(Json(Release("v5.0.0", assets: failure == "ambiguous" ? [asset, asset] : [asset])))));
        await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.NoPackage, service.Snapshot.State);
        await service.DownloadAsync(); Assert.AreEqual(0, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public async Task NewerReleaseWithNoPackageDoesNotFallBackToOlderRelease()
    {
        using var service = Create(new Handler((_, _) => Task.FromResult(Json(Release("v5.0.0"), Release("v6.0.0", assets: [])))));
        await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.NoPackage, service.Snapshot.State); Assert.AreEqual("6.0.0", service.Snapshot.Version);
    }

    [TestMethod]
    public async Task DuplicateSemanticReleasesFailClosed()
    {
        using var service = Create(new Handler((_, _) => Task.FromResult(Json(Release("v5.0.0"), Release("5.0.0")))));
        await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.NoPackage, service.Snapshot.State);
    }

    [TestMethod]
    public async Task TraversesBoundedPaginationInsteadOfAssumingApiIsSortedByVersion()
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Query.Contains("page=2")
            ? Json(Release("v6.0.0")) : Json(Enumerable.Range(0, 100).Select(i => Release($"v3.0.{i}")).ToArray())));
        using var service = Create(handler); await service.CheckAsync();
        Assert.AreEqual(2, handler.Calls); Assert.AreEqual("6.0.0", service.Snapshot.Version);
    }

    [TestMethod]
    public async Task CatalogCapAndMalformedResponsesNeverClaimCurrentVersionIsLatest()
    {
        using var paged = Create(new Handler((_, _) => Task.FromResult(Json(Enumerable.Range(0, 100).Select(i => Release($"v3.0.{i}")).ToArray()))));
        await paged.CheckAsync(); Assert.AreEqual(ReleaseUpdateError.CatalogLimit, paged.Snapshot.Error);
        foreach (string invalid in new[] { "{", "{}", "[{}]", "[null]", new string(' ', 2 * 1024 * 1024 + 1) })
        {
            using var service = Create(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(invalid) })));
            await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.Failed, service.Snapshot.State);
            Assert.AreEqual(ReleaseUpdateError.InvalidCatalog, service.Snapshot.Error);
        }
    }

    [TestMethod]
    [DataRow(404, ReleaseUpdateError.Network)][DataRow(429, ReleaseUpdateError.RateLimit)][DataRow(503, ReleaseUpdateError.Network)]
    public async Task HttpErrorsRemainErrors(int status, ReleaseUpdateError expected)
    {
        using var service = Create(new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        await service.CheckAsync(); Assert.AreEqual(ReleaseUpdateState.Failed, service.Snapshot.State); Assert.AreEqual(expected, service.Snapshot.Error);
    }

    [TestMethod]
    public async Task UnknownBuildOrPlatformDoesNotContactNetwork()
    {
        using var handler = new Handler((_, _) => throw new AssertFailedException("Should not request anything"));
        using var unknown = Create(handler, current: "unknown"); await unknown.CheckAsync(); Assert.AreEqual(ReleaseUpdateError.UnknownVersion, unknown.Snapshot.Error);
        using var other = Create(new Handler((_, _) => throw new AssertFailedException()), rid: "win-arm64");
        await other.CheckAsync(); Assert.AreEqual(ReleaseUpdateError.UnsupportedRuntime, other.Snapshot.Error); Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    public async Task ValidatedDownloadIsAtomicAndNeverReplacesOldData()
    {
        string save = Path.Combine(root, "original.sav"); await File.WriteAllTextAsync(save, "untouched");
        using var service = Create(PackageHandler());
        await service.CheckAsync(); await service.DownloadAsync();
        Assert.AreEqual(ReleaseUpdateState.Downloaded, service.Snapshot.State);
        string first = service.Snapshot.DownloadPath!;
        CollectionAssert.AreEqual(Payload, await File.ReadAllBytesAsync(first));
        await service.CheckAsync(); await service.DownloadAsync(); Assert.AreNotEqual(first, service.Snapshot.DownloadPath);
        Assert.IsTrue(File.Exists(first)); Assert.AreEqual("untouched", await File.ReadAllTextAsync(save));
        Assert.AreEqual(0, Directory.GetFiles(root, "*.partial", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    [DataRow("hash")][DataRow("truncated")][DataRow("oversize")][DataRow("lengthHeader")]
    public async Task CorruptOrTruncatedDownloadIsRemoved(string failure)
    {
        byte[] content = failure switch { "hash" => Enumerable.Repeat((byte)0, Payload.Length).ToArray(), "truncated" => Payload[..^1], "oversize" => [.. Payload, 42], _ => Payload };
        using var handler = new Handler((request, _) =>
        {
            if (request.RequestUri!.Host == "api.github.com") return Task.FromResult(Json(Release("v5.0.0")));
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnboundedLengthContent(content) };
            if (failure == "lengthHeader") response.Content.Headers.ContentLength = 1;
            return Task.FromResult(response);
        });
        using var service = Create(handler); await service.CheckAsync(); await service.DownloadAsync();
        Assert.AreEqual(ReleaseUpdateError.Integrity, service.Snapshot.Error); Assert.IsNull(service.Snapshot.DownloadPath);
        Assert.AreEqual(0, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
        Assert.AreEqual(0, Directory.GetDirectories(root).Length);
    }

    [TestMethod]
    [DataRow("http://release-assets.githubusercontent.com/file")]
    [DataRow("https://release-assets.githubusercontent.com.evil.test/file")]
    [DataRow("https://user:pass@release-assets.githubusercontent.com/file")]
    [DataRow("https://release-assets.githubusercontent.com:444/file")]
    [DataRow("https://127.0.0.1/file")][DataRow("file:///tmp/file")]
    public async Task UnapprovedRedirectsAreNeverRequested(string target)
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com"
            ? Json(Release("v5.0.0")) : Redirect(target)));
        using var service = Create(handler); await service.CheckAsync(); await service.DownloadAsync();
        Assert.AreEqual(ReleaseUpdateError.UnsafeRedirect, service.Snapshot.Error); Assert.AreEqual(2, handler.Calls);
    }

    [TestMethod]
    public async Task ApprovedCdnRedirectIsSupportedButRedirectLoopIsBounded()
    {
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host switch
        {
            "api.github.com" => Json(Release("v5.0.0")), "github.com" => Redirect("https://release-assets.githubusercontent.com/test?signature=fake"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) }
        }));
        using var service = Create(handler); await service.CheckAsync(); await service.DownloadAsync();
        Assert.AreEqual(ReleaseUpdateState.Downloaded, service.Snapshot.State); Assert.AreEqual(3, handler.Calls);
        using var loopHandler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com" ? Json(Release("v5.0.0")) : Redirect("https://release-assets.githubusercontent.com/loop")));
        using var loop = Create(loopHandler); await loop.CheckAsync(); await loop.DownloadAsync();
        Assert.AreEqual(ReleaseUpdateError.UnsafeRedirect, loop.Snapshot.Error); Assert.AreEqual(6, loopHandler.Calls);
    }

    [TestMethod]
    public async Task CancellationAndDisposalStopWorkersAndDoNotStartConcurrentChecks()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async (_, token) => { started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return Json(); });
        using var service = Create(handler);
        Task pending = service.CheckAsync(); await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.CheckAsync(); await service.DownloadAsync(); Assert.AreEqual(1, handler.Calls);
        service.Cancel(); await pending.WaitAsync(TimeSpan.FromSeconds(5)); Assert.AreEqual(ReleaseUpdateState.Cancelled, service.Snapshot.State);
        Task second = service.CheckAsync(); service.Dispose(); await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(ReleaseUpdateState.Cancelled, service.Snapshot.State);
        int count = handler.Calls; await service.CheckAsync(); Assert.AreEqual(count, handler.Calls);
    }

    [TestMethod]
    public async Task CancelledDownloadCleansOnlyItsPartialFile()
    {
        var stream = new PausingStream();
        using var handler = new Handler((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com" ? Json(Release("v5.0.0"))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) }));
        using var service = Create(handler); await service.CheckAsync();
        Task download = service.DownloadAsync(); await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, Directory.GetFiles(root, "*.partial", SearchOption.AllDirectories).Length);
        service.Cancel(); await download.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(ReleaseUpdateState.Cancelled, service.Snapshot.State);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length);
    }

    private ReleaseUpdateService Create(Handler handler, string current = "4.8.0-alpha.1", string rid = "win-x64", bool includePrereleases = true) => new(current, rid, includePrereleases, root, new HttpClient(handler));
    private static Handler PackageHandler() => new((request, _) => Task.FromResult(request.RequestUri!.Host == "api.github.com" ? Json(Release("v5.0.0"))
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) }));
    private static HttpResponseMessage Json(params object[] releases) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(releases)) };
    private static object Release(string tag, bool draft = false, bool pre = false, object[]? assets = null) =>
        new { tag_name = tag, draft, prerelease = pre, assets = assets ?? [Asset(tag, "win-x64")] };
    private static Dictionary<string, object?> Asset(string tag, string rid)
    {
        string name = $"AetherBoy-{tag.TrimStart('v')}-{rid}-self-contained." + (rid == "win-x64" ? "zip" : "tar.gz");
        var result = new Dictionary<string, object?> { ["state"] = "uploaded", ["size"] = Payload.Length, ["digest"] = "sha256:" + Convert.ToHexString(SHA256.HashData(Payload)) };
        SetName(result, tag, name); return result;
    }
    private static void SetName(Dictionary<string, object?> asset, string tag, string name)
    { asset["name"] = name; asset["browser_download_url"] = $"{ReleaseUpdateService.ReleasesPage}/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}"; }
    private static HttpResponseMessage Redirect(string target)
    { var response = new HttpResponseMessage(HttpStatusCode.Found); response.Headers.Location = new Uri(target); return response; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Interlocked.Increment(ref calls); return send(request, token); }
    }
    private sealed class UnboundedLengthContent(byte[] content) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(content).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(content));
    }
    private sealed class PausingStream : Stream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { Started.TrySetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
