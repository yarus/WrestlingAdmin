using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wrestling.UI.Utils.Localization;

namespace Wrestling.UI.Material.Localization
{
    // Discovers <BaseDirectory>/i18n/*.json at startup, parses each into the
    // service. JSON shape: a flat object of "Key": "Value" pairs plus an
    // optional "_meta": { "code", "displayName", "culture" } entry that
    // describes the language to the picker. Files with no _meta block fall
    // back to the file name (e.g. "ru.json" → code "ru").
    public static class JsonLocalizationLoader
    {
        public static void LoadAll(ILocalizationService service, string folder)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

            foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
            {
                TryLoadFile(service, file);
            }
        }

        private static void TryLoadFile(ILocalizationService service, string path)
        {
            try
            {
                var content = File.ReadAllText(path);
                var root = JObject.Parse(content);

                LanguageDescriptor descriptor = ReadDescriptor(root, path);
                var entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var prop in root.Properties())
                {
                    if (prop.Name == "_meta") continue;
                    if (prop.Value.Type != JTokenType.String) continue;
                    entries[prop.Name] = prop.Value.Value<string>();
                }

                service.RegisterLanguage(descriptor, entries);
            }
            catch (JsonException ex)
            {
                // Bad JSON in a translation file should not crash the app at
                // startup — the language just won't appear in the picker.
                Debug.WriteLine($"JsonLocalizationLoader: failed to parse {path}: {ex.Message}");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"JsonLocalizationLoader: failed to read {path}: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"JsonLocalizationLoader: access denied {path}: {ex.Message}");
            }
        }

        private static LanguageDescriptor ReadDescriptor(JObject root, string path)
        {
            var fallbackCode = Path.GetFileNameWithoutExtension(path);

            if (root["_meta"] is JObject meta)
            {
                var code = meta.Value<string>("code") ?? fallbackCode;
                var displayName = meta.Value<string>("displayName") ?? code;
                var culture = meta.Value<string>("culture") ?? string.Empty;
                return new LanguageDescriptor(code, displayName, culture);
            }

            return new LanguageDescriptor(fallbackCode, fallbackCode, string.Empty);
        }
    }
}
