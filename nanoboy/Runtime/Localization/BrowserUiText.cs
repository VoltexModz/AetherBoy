using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AetherBoy.Runtime.Localization;

// Markers exist only in bundled, application-owned resources. Never pass user
// HTML, SDP or connection data here. Encoding is specific to each output context.
internal static partial class BrowserUiText
{
    internal static byte[] Render(byte[] resource, string language, bool script)
    {
        string source = Encoding.UTF8.GetString(resource);
        string rendered = script
            ? ScriptText().Replace(source, match => JsonSerializer.Serialize(
                UiText.Get(JsonSerializer.Deserialize<string>(match.Groups[1].Value)!, language)))
            : HtmlText().Replace(source, match => WebUtility.HtmlEncode(UiText.Get(match.Groups[1].Value, language)))
                .Replace("{{language}}", language == UiText.German ? "de" : "en");
        return Encoding.UTF8.GetBytes(rendered);
    }

    [GeneratedRegex(@"\{\{ui:([^}]+)\}\}")]
    private static partial Regex HtmlText();

    [GeneratedRegex("/\\*ui\\*/(\"(?:[^\"\\\\]|\\\\.)*\")")]
    private static partial Regex ScriptText();
}
