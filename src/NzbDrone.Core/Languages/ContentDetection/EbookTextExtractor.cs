using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace NzbDrone.Core.Languages.ContentDetection
{
    public interface IEbookTextExtractor
    {
        bool CanExtract(string path);
        string ExtractSample(string path, int maxChars);
        string GetDeclaredLanguage(string path);
    }

    public class EbookTextExtractor : IEbookTextExtractor
    {
        private static readonly Regex TagRegex = new (@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new (@"\s+", RegexOptions.Compiled);
        private static readonly Regex OpfLanguageRegex = new (@"<dc:language[^>]*>\s*([A-Za-z-]+)\s*</dc:language>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly Logger _logger;

        public EbookTextExtractor(Logger logger)
        {
            _logger = logger;
        }

        public bool CanExtract(string path)
        {
            return Path.GetExtension(path).Equals(".epub", StringComparison.OrdinalIgnoreCase);
        }

        public string ExtractSample(string path, int maxChars)
        {
            try
            {
                using var archive = ZipFile.OpenRead(path);

                // Content documents, skipping covers/tocs; sample from the middle
                // of the spine where real prose lives.
                var contentEntries = archive.Entries
                    .Where(e => IsContentDocument(e.FullName))
                    .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!contentEntries.Any())
                {
                    return null;
                }

                var sample = new StringBuilder();
                var start = contentEntries.Count / 3;

                foreach (var entry in contentEntries.Skip(start))
                {
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true);
                    var html = reader.ReadToEnd();
                    var text = WhitespaceRegex.Replace(TagRegex.Replace(html, " "), " ");

                    sample.Append(text);
                    sample.Append(' ');

                    if (sample.Length >= maxChars)
                    {
                        break;
                    }
                }

                var result = sample.ToString();
                return result.Length > maxChars ? result.Substring(0, maxChars) : result;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to extract text sample from {0}", path);
                return null;
            }
        }

        public string GetDeclaredLanguage(string path)
        {
            try
            {
                using var archive = ZipFile.OpenRead(path);

                var opf = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));

                if (opf == null)
                {
                    return null;
                }

                using var reader = new StreamReader(opf.Open(), Encoding.UTF8, true);
                var match = OpfLanguageRegex.Match(reader.ReadToEnd());

                return match.Success ? match.Groups[1].Value : null;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to read declared language from {0}", path);
                return null;
            }
        }

        private static bool IsContentDocument(string name)
        {
            var lower = name.ToLowerInvariant();

            if (!lower.EndsWith(".xhtml") && !lower.EndsWith(".html") && !lower.EndsWith(".htm"))
            {
                return false;
            }

            return !lower.Contains("cover") && !lower.Contains("toc") && !lower.Contains("nav");
        }
    }
}
