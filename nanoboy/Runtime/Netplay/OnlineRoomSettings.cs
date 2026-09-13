using System;
using System.IO;
using System.Text.Json;

namespace AetherBoy.Runtime.Netplay;

public sealed record OnlineRoomSettings(string ServerUrl = "", string AccessKey = "")
{
    public Uri Validate()
    {
        if (ServerUrl is null || !Uri.TryCreate(ServerUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new ArgumentException("Enter the room server's HTTPS address, without a path or password.");
        if (AccessKey is null || AccessKey.Length < 32 || AccessKey.Length > 256 || AccessKey.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            throw new ArgumentException("Enter the access key provided by your room server administrator.");
        foreach (char c in AccessKey)
            if (c < '!' || c > '~') throw new ArgumentException("The access key must contain only printable ASCII characters without spaces.");
        return uri;
    }

    public static OnlineRoomSettings Load(string path)
    {
        try
        {
            var result = JsonSerializer.Deserialize<OnlineRoomSettings>(File.ReadAllText(path));
            return result is { ServerUrl: not null, AccessKey: not null } ? result : new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var file = new FileStream(temporary, options))
            { JsonSerializer.Serialize(file, this); file.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string NormalizeCode(string code)
    {
        if (code is null) throw new ArgumentException("Enter the room code.");
        string result = code.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
        if (result.Length != 10) throw new ArgumentException("Enter the 10-character room code.");
        foreach (char c in result)
            if (!"ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(c)) throw new ArgumentException("The room code contains an invalid character.");
        return result;
    }
}
