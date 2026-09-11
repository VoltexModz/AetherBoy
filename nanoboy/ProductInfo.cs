using System.Reflection;
using System.Linq;

namespace nanoboy
{
    internal static class ProductInfo
    {
        public const string Name = "AetherBoy";
        public const string TeamName = "NekoZDevTeam";
        public const string RepositoryUrl = "https://github.com/VoltexModz/AetherBoy";
        // The button is already visible; activate its external link once the team supplies the destination.
        private const string SupportUrl = "";
        public const string Status = "Alpha";

        public static System.Uri? SupportUri =>
            System.Uri.TryCreate(SupportUrl, System.UriKind.Absolute, out var uri) &&
            uri.Scheme == System.Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host) &&
            string.IsNullOrEmpty(uri.UserInfo) ? uri : null;

        public static string BuildChannel => typeof(ProductInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "AetherBoyChannel")?.Value ?? "development";

        public static bool IsDevelopmentBuild => string.Equals(BuildChannel, "development",
            System.StringComparison.OrdinalIgnoreCase);

        public static string Version
        {
            get
            {
                var attribute = Assembly.GetEntryAssembly()
                    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                var value = attribute?.InformationalVersion;

                if (string.IsNullOrWhiteSpace(value))
                {
                    return "4.8.0-alpha.1";
                }

                int metadataSeparator = value.IndexOf('+');
                return metadataSeparator >= 0 ? value.Substring(0, metadataSeparator) : value;
            }
        }

        public static string DisplayName => $"{Name} {Version} ({Status})";
    }
}
