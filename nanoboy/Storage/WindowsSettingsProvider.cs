using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using AetherBoy.Runtime;

namespace nanoboy.Storage;

public sealed class WindowsSettingsProvider : SettingsProvider, IApplicationSettingsProvider
{
    private static readonly object fileSync = new();
    private readonly string file;
    private readonly string? legacyRoot;
    internal string FilePath => file;
    public WindowsSettingsProvider() : this(WindowsDataPaths.Default.SettingsFile, WindowsDataPaths.Default.LegacySettingsRoot) { }
    internal WindowsSettingsProvider(string file, string? legacyRoot = null)
    {
        this.file = file;
        this.legacyRoot = legacyRoot;
    }
    public override string ApplicationName { get; set; } = "AetherBoy";
    public override void Initialize(string name, NameValueCollection config) =>
        base.Initialize(string.IsNullOrEmpty(name) ? nameof(WindowsSettingsProvider) : name, config);

    public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection properties)
    {
        // Keep primary/backup selection and first-run migration in the same
        // transaction boundary as saves. A reload must not observe the brief
        // rename window in File.Replace and mistake it for missing settings.
        lock (fileSync) return ReadPropertyValues(context, properties);
    }

    private SettingsPropertyValueCollection ReadPropertyValues(SettingsContext context, SettingsPropertyCollection properties)
    {
        Dictionary<string, string?>? values = Read(file);
        if (values is null && Read(file + ".bak") is { } recovered)
        {
            // An older backup must not re-enable publicly shared activity after opt-out.
            recovered["DiscordPresenceEnabled"] = "False";
            recovered["DiscordShareGameTitle"] = "False";
            recovered["UpdateCheckOnStartup"] = "False";
            values = recovered;
        }
        if (values is null && (File.Exists(file) || File.Exists(file + ".bak")))
            values = new() { ["DiscordPresenceEnabled"] = "False", ["DiscordShareGameTitle"] = "False" };
        if (values == null && !File.Exists(file) && !File.Exists(file + ".bak"))
        {
            // A portable launch must not silently import settings from the machine's profile.
            values = PortableStorage.IsEnabled ? null : ReadLegacy(properties);
            if (values != null) Write(values);
            else if (!PortableStorage.IsEnabled)
            {
                // Also support the framework's current location if no prior AetherBoy build is found.
                var legacy = new LocalFileSettingsProvider();
                legacy.Initialize("LocalFileSettingsProvider", new NameValueCollection());
                SettingsPropertyValueCollection imported = legacy.GetPropertyValues(context, properties);
                SetPropertyValues(context, imported);
                return imported;
            }
        }

        var result = new SettingsPropertyValueCollection();
        foreach (SettingsProperty property in properties)
        {
            var value = new SettingsPropertyValue(property);
            if (values != null && values.TryGetValue(property.Name, out string? stored))
                value.SerializedValue = stored;
            result.Add(value);
        }
        return result;
    }

    public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection properties)
    {
        var changed = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (SettingsPropertyValue value in properties)
            changed[value.Name] = value.SerializedValue?.ToString();
        WriteSnapshot(changed);
    }

    internal void WriteSnapshot(IReadOnlyDictionary<string, string?> snapshot)
    {
        lock (fileSync)
        {
            var values = Read(file) ?? Read(file + ".bak") ?? new Dictionary<string, string?>();
            foreach (var entry in snapshot) values[entry.Key] = entry.Value;
            Write(values);
        }
    }

    public void Reset(SettingsContext context)
    {
        // Resetting emulator preferences must not silently revoke a diagnostic privacy choice.
        lock (fileSync)
        {
            Dictionary<string, string?>? current = Read(file) ?? Read(file + ".bak");
            var reset = new Dictionary<string, string?>();
            if (current != null && current.TryGetValue("DiagnosticsRecording", out string? recording))
                reset["DiagnosticsRecording"] = recording;
            else if (current == null && (File.Exists(file) || File.Exists(file + ".bak")))
                reset["DiagnosticsRecording"] = "False"; // Unknown prior choice: keep recording off.
            Write(reset);
        }
    }
    public void Upgrade(SettingsContext context, SettingsPropertyCollection properties) { }
    public SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property) =>
        new(property);

    private static Dictionary<string, string?>? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            // Saves publish a complete snapshot via File.Replace. Readers must allow
            // that rename: a reload racing a background save must not block it and
            // open a modal "settings not saved" dialog. The open handle continues
            // reading the old, complete snapshot until this read finishes.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(stream);
        }
        catch (JsonException) { return null; }
    }

    private Dictionary<string, string?>? ReadLegacy(SettingsPropertyCollection properties)
    {
        if (legacyRoot == null || !Directory.Exists(legacyRoot)) return null;
        try
        {
            var candidates = Directory.EnumerateDirectories(legacyRoot, "AetherBoy_Url_*")
                .SelectMany(Directory.EnumerateDirectories)
                .Select(directory => new FileInfo(Path.Combine(directory, "user.config")))
                .Where(info => info.Exists && info.Length < 1024 * 1024)
                .OrderByDescending(info => info.LastWriteTimeUtc);
            foreach (FileInfo candidate in candidates)
            {
                try
                {
                    using var reader = XmlReader.Create(candidate.FullName, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                        MaxCharactersInDocument = 1024 * 1024
                    });
                    XElement? section = XDocument.Load(reader).Root?.Element("userSettings")
                        ?.Element("nanoboy.Properties.Settings");
                    if (section == null) continue;
                    var result = new Dictionary<string, string?>();
                    foreach (XElement setting in section.Elements("setting"))
                    {
                        string? name = (string?)setting.Attribute("name");
                        if (name != null && properties[name] != null &&
                            (string?)setting.Attribute("serializeAs") == "String")
                            result[name] = setting.Element("value")?.Value;
                    }
                    return result;
                }
                catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException)
                {
                    // Try an older valid AetherBoy configuration without altering the source.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        return null;
    }

    private void Write(Dictionary<string, string?> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        string temporary = file + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, values, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(file))
            {
                // Preserve the last readable settings when recovering a damaged primary file.
                File.Replace(temporary, file, Read(file) != null ? file + ".bak" : null);
            }
            else File.Move(temporary, file);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
