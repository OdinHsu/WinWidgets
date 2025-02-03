using System.Collections.Generic;
using System;

namespace Services
{
    internal class HTMLDocService
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _metaTagCache =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _cacheLock = new object();

        /// <summary>
        /// 獲取指定 HTML 文件的所有 meta 標籤 (快取優化版本)
        /// </summary>
        public Dictionary<string, string> GetAllMetaTags(string widgetPath)
        {
            lock (_cacheLock)
            {
                // 快取命中檢查
                if (_metaTagCache.TryGetValue(widgetPath, out var cachedTags))
                {
                    return cachedTags;
                }

                // 解析並快取新內容
                var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var doc = new HtmlAgilityPack.HtmlDocument();
                    doc.Load(widgetPath);

                    var metaNodes = doc.DocumentNode.SelectNodes("//meta[@name and @content]");
                    if (metaNodes != null)
                    {
                        foreach (var node in metaNodes)
                        {
                            var name = node.GetAttributeValue("name", "").Trim();
                            var content = node.GetAttributeValue("content", "").Trim();
                            if (!string.IsNullOrEmpty(name))
                            {
                                tags[name] = content;
                            }
                        }
                    }
                }
                catch
                {
                    // 明確標記解析失敗，避免後續重試
                    tags = null;
                }

                // 空字典表示有效解析但無結果，null 表示解析失敗
                _metaTagCache[widgetPath] = tags ?? new Dictionary<string, string>();
                return _metaTagCache[widgetPath];
            }
        }

        /// <summary>
        /// 獲取單個 meta 標籤值 (快取優化版本)
        /// </summary>
        public string GetMetaTagValue(string name, string widgetPath)
        {
            var allTags = GetAllMetaTags(widgetPath);
            if (allTags != null && allTags.TryGetValue(name, out string value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// 清除快取 (用於開發時熱重載)
        /// </summary>
        public void ClearCache(string widgetPath = null)
        {
            lock (_cacheLock)
            {
                if (widgetPath == null)
                {
                    _metaTagCache.Clear();
                }
                else
                {
                    _metaTagCache.Remove(widgetPath);
                }
            }
        }
    }
}