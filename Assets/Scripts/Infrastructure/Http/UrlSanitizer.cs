using System.Text.RegularExpressions;

namespace Yobi.Infrastructure.Http
{
    internal static class UrlSanitizer
    {
        // Redacts common secret query parameters (API keys, tokens) from a URL so it's safe
        // to include in exception messages and logs. .NET's regex engine supports variable-length
        // lookbehind, so the alternation below works even though the parameter names differ in length.
        private static readonly Regex SensitiveParamRegex = new Regex(
            @"(?<=[?&](key|api_key|apikey|access_token|token)=)[^&]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string Redact(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            return SensitiveParamRegex.Replace(url, "***REDACTED***");
        }
    }
}
