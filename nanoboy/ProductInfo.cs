using System.Reflection;
using System.Linq;

namespace nanoboy
{
    internal static class ProductInfo
    {
        public const string Name = "AetherBoy";
        public const string Status = "Alpha";

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
