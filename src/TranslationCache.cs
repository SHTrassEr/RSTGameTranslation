using System;
using System.Collections.Generic;
using System.Linq;

namespace RSTGameTranslation
{
    class TranslationCacheEntry
    {
        public string TranslatedText;
        public DateTime CreatedAt;
        public DateTime LastAccessedAt;
    }

    class TranslationCache
    {
        private readonly Dictionary<string, TranslationCacheEntry> _entries = new();
        private readonly object _lock = new();

        public int Count
        {
            get { lock (_lock) { return _entries.Count; } }
        }

        public bool TryGet(string text, out string translatedText)
        {
            if (!ConfigManager.Instance.IsTranslationCacheEnabled())
            {
                translatedText = string.Empty;
                return false;
            }
            if (!ShouldCache(text))
            {
                translatedText = string.Empty;
                return false;
            }
            lock (_lock)
            {
                if (_entries.TryGetValue(text, out var entry))
                {
                    int ttl = ConfigManager.Instance.GetTranslationCacheTtlMinutes();
                    if (ttl > 0 && (DateTime.Now - entry.CreatedAt).TotalMinutes > ttl)
                    {
                        _entries.Remove(text);
                        translatedText = string.Empty;
                        return false;
                    }
                    entry.LastAccessedAt = DateTime.Now;
                    translatedText = entry.TranslatedText;
                    Console.WriteLine($"Cache hit:  '{Trunc(text, 20)}' -> '{Trunc(translatedText, 20)}'");
                    return true;
                }
            }
            translatedText = string.Empty;
            return false;
        }

        public void Store(string text, string translatedText)
        {
            if (!ConfigManager.Instance.IsTranslationCacheEnabled())
                return;
            if (!ShouldCache(text))
                return;
            if (string.IsNullOrEmpty(translatedText))
                return;
            lock (_lock)
            {
                if (_entries.TryGetValue(text, out var existing))
                {
                    existing.TranslatedText = translatedText;
                    existing.LastAccessedAt = DateTime.Now;
                    return;
                }
                int maxEntries = ConfigManager.Instance.GetTranslationCacheMaxEntries();
                while (maxEntries > 0 && _entries.Count >= maxEntries)
                {
                    var oldest = _entries.MinBy(kvp => kvp.Value.LastAccessedAt);
                    _entries.Remove(oldest.Key);
                }
                _entries[text] = new TranslationCacheEntry
                {
                    TranslatedText = translatedText,
                    CreatedAt = DateTime.Now,
                    LastAccessedAt = DateTime.Now,
                };
                Console.WriteLine($"Cache store: '{Trunc(text, 20)}' -> '{Trunc(translatedText, 20)}'");
            }
        }

        public void Clear()
        {
            lock (_lock) { _entries.Clear(); }
        }

        private static string Trunc(string s, int maxLen)
        {
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }

        private bool ShouldCache(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            int max = ConfigManager.Instance.GetTranslationCacheMaxLength();
            if (max > 0 && text.Length > max) return false;
            return true;
        }
    }
}
