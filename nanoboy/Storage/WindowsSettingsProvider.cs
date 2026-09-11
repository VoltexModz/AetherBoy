using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace nanoboy.Storage;

public sealed class WindowsSettingsProvider : SettingsProvider, IApplicationSettingsProvider
{
    private readonly string file;
    private readonly string? legacyRoot;
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
        Dictionary<string, string?>? values = Read(file) ?? Read(file + ".bak");
        if (values == null && !File.Exists(file) && !File.Exists(file + ".bak"))
        {
            values = ReadLegacy(properties);
            if (values != null) Write(values);
            else
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
        var values = Read(file) ?? Read(file + ".bak") ?? new Dictionary<string, string?>();
        foreach (SettingsPropertyValue value in properties)
            values[value.Name] = value.SerializedValue?.ToString();
        Write(values);
    }

    public void Reset(SettingsContext context) => Write(new Dictionary<string, string?>());
    public void Upgrade(SettingsContext context, SettingsPropertyCollection properties) { }
    public SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property) =>
        new(property);

    private static Dictionary<string, string?>? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(path)); }
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
