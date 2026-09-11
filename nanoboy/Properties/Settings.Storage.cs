using System.Configuration;
using nanoboy.Storage;

namespace nanoboy.Properties;

[SettingsProvider(typeof(WindowsSettingsProvider))]
internal sealed partial class Settings { }
