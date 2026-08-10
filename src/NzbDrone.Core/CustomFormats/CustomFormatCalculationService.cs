using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Books;
using NzbDrone.Core.History;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.CustomFormats
{
    public interface ICustomFormatCalculationService
    {
        List<CustomFormat> ParseCustomFormat(RemoteBook remoteBook, long size);
        List<CustomFormat> ParseCustomFormat(BookFile bookFile, Author artist);
        List<CustomFormat> ParseCustomFormat(BookFile bookFile);
        List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Author artist);
        List<CustomFormat> ParseCustomFormat(EntityHistory history, Author artist);
        List<CustomFormat> ParseCustomFormat(LocalBook localBook);
    }

    public class CustomFormatCalculationService : ICustomFormatCalculationService
    {
        private readonly ICustomFormatService _formatService;
        private readonly Logger _logger;

        public CustomFormatCalculationService(ICustomFormatService formatService, Logger logger)
        {
            _formatService = formatService;
            _logger = logger;
        }

        public List<CustomFormat> ParseCustomFormat(RemoteBook remoteBook, long size)
        {
            var input = new CustomFormatInput
            {
                BookInfo = remoteBook.ParsedBookInfo,
                Author = remoteBook.Author,
                Size = size,
                IndexerFlags = remoteBook.Release?.IndexerFlags ?? 0,
                Languages = CombineLanguages(remoteBook.Release?.Languages, remoteBook.ParsedBookInfo?.Languages)
            };

            return ParseCustomFormat(input);
        }

        // Combines explicit indexer metadata (e.g. the newznab/torznab
        // language attribute) with languages parsed from the release title.
        // Explicit metadata wins when both are known.
        private static List<Language> CombineLanguages(List<Language> releaseLanguages, List<Language> parsedLanguages)
        {
            var known = (releaseLanguages ?? new List<Language>())
                .Where(l => l != null && l != Language.Unknown)
                .ToList();

            if (!known.Any())
            {
                known = (parsedLanguages ?? new List<Language>())
                    .Where(l => l != null && l != Language.Unknown)
                    .ToList();
            }

            if (!known.Any())
            {
                return new List<Language> { Language.Unknown };
            }

            return known.DistinctBy(l => l.Id).ToList();
        }

        public List<CustomFormat> ParseCustomFormat(BookFile bookFile, Author author)
        {
            return ParseCustomFormat(bookFile, author, _formatService.All());
        }

        public List<CustomFormat> ParseCustomFormat(BookFile bookFile)
        {
            return ParseCustomFormat(bookFile, bookFile.Author.Value, _formatService.All());
        }

        public List<CustomFormat> ParseCustomFormat(Blocklist blocklist, Author author)
        {
            var parsed = Parser.Parser.ParseBookTitle(blocklist.SourceTitle);

            var bookInfo = new ParsedBookInfo
            {
                AuthorName = author.Name,
                ReleaseTitle = parsed?.ReleaseTitle ?? blocklist.SourceTitle,
                Quality = blocklist.Quality,
                ReleaseGroup = parsed?.ReleaseGroup,
                Languages = parsed?.Languages ?? Parser.LanguageParser.ParseLanguages(blocklist.SourceTitle)
            };

            var input = new CustomFormatInput
            {
                BookInfo = bookInfo,
                Author = author,
                Size = blocklist.Size ?? 0,
                IndexerFlags = blocklist.IndexerFlags,
                Languages = bookInfo.Languages
            };

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(EntityHistory history, Author author)
        {
            var parsed = Parser.Parser.ParseBookTitle(history.SourceTitle);

            long.TryParse(history.Data.GetValueOrDefault("size"), out var size);
            Enum.TryParse(history.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags indexerFlags);

            var bookInfo = new ParsedBookInfo
            {
                AuthorName = author.Name,
                ReleaseTitle = parsed?.ReleaseTitle ?? history.SourceTitle,
                Quality = history.Quality,
                ReleaseGroup = parsed?.ReleaseGroup,
                Languages = parsed?.Languages ?? Parser.LanguageParser.ParseLanguages(history.SourceTitle)
            };

            var input = new CustomFormatInput
            {
                BookInfo = bookInfo,
                Author = author,
                Size = size,
                IndexerFlags = indexerFlags,
                Languages = bookInfo.Languages
            };

            return ParseCustomFormat(input);
        }

        public List<CustomFormat> ParseCustomFormat(LocalBook localBook)
        {
            var languageTitle = localBook.SceneName.IsNotNullOrWhiteSpace()
                ? localBook.SceneName
                : Path.GetFileName(localBook.Path ?? string.Empty);

            var bookInfo = new ParsedBookInfo
            {
                AuthorName = localBook.Author.Name,
                ReleaseTitle = localBook.SceneName,
                Quality = localBook.Quality,
                ReleaseGroup = localBook.ReleaseGroup,
                Languages = Parser.LanguageParser.ParseLanguages(languageTitle ?? string.Empty)
            };

            var input = new CustomFormatInput
            {
                BookInfo = bookInfo,
                Author = localBook.Author,
                Size = localBook.Size,
                IndexerFlags = localBook.IndexerFlags,
                Languages = bookInfo.Languages
            };

            return ParseCustomFormat(input);
        }

        private List<CustomFormat> ParseCustomFormat(CustomFormatInput input)
        {
            return ParseCustomFormat(input, _formatService.All());
        }

        private static List<CustomFormat> ParseCustomFormat(CustomFormatInput input, List<CustomFormat> allCustomFormats)
        {
            var matches = new List<CustomFormat>();

            foreach (var customFormat in allCustomFormats)
            {
                var specificationMatches = customFormat.Specifications
                    .GroupBy(t => t.GetType())
                    .Select(g => new SpecificationMatchesGroup
                    {
                        Matches = g.ToDictionary(t => t, t => t.IsSatisfiedBy(input))
                    })
                    .ToList();

                if (specificationMatches.All(x => x.DidMatch))
                {
                    matches.Add(customFormat);
                }
            }

            return matches.OrderBy(x => x.Name).ToList();
        }

        private List<CustomFormat> ParseCustomFormat(BookFile bookFile, Author author, List<CustomFormat> allCustomFormats)
        {
            var releaseTitle = string.Empty;

            if (bookFile.SceneName.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using scene name for release title: {0}", bookFile.SceneName);
                releaseTitle = bookFile.SceneName;
            }
            else if (bookFile.OriginalFilePath.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using original file path for release title: {0}", bookFile.OriginalFilePath);
                releaseTitle = bookFile.OriginalFilePath;
            }
            else if (bookFile.Path.IsNotNullOrWhiteSpace())
            {
                _logger.Trace("Using path for release title: {0}", Path.GetFileName(bookFile.Path));
                releaseTitle = Path.GetFileName(bookFile.Path);
            }

            var bookInfo = new ParsedBookInfo
            {
                AuthorName = author.Name,
                ReleaseTitle = releaseTitle,
                Quality = bookFile.Quality,
                ReleaseGroup = bookFile.ReleaseGroup,
                Languages = Parser.LanguageParser.ParseLanguages(releaseTitle ?? string.Empty)
            };

            var input = new CustomFormatInput
            {
                BookInfo = bookInfo,
                Author = author,
                Size = bookFile.Size,
                IndexerFlags = bookFile.IndexerFlags,
                Languages = bookInfo.Languages,
                Filename = Path.GetFileName(bookFile.Path)
            };

            return ParseCustomFormat(input, allCustomFormats);
        }
    }
}
