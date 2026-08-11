using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MediaFiles.EbookConversion
{
    public interface IEbookConversionService
    {
        bool ConvertIfNeeded(BookFile bookFile, Edition edition, string authorName);
    }

    public class EbookConversionService : IEbookConversionService, IHandleAsync<TrackImportedEvent>
    {
        // Formats ebook-convert can reasonably take as input; audiobooks are excluded.
        private static readonly HashSet<string> ConvertibleExtensions = new (StringComparer.OrdinalIgnoreCase)
        {
            ".epub", ".mobi", ".azw", ".azw3", ".pdf"
        };

        private readonly IConfigService _configService;
        private readonly IEbookConverter _converter;
        private readonly IMediaFileService _mediaFileService;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public EbookConversionService(IConfigService configService,
                                      IEbookConverter converter,
                                      IMediaFileService mediaFileService,
                                      IRecycleBinProvider recycleBinProvider,
                                      IDiskProvider diskProvider,
                                      Logger logger)
        {
            _configService = configService;
            _converter = converter;
            _mediaFileService = mediaFileService;
            _recycleBinProvider = recycleBinProvider;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public void HandleAsync(TrackImportedEvent message)
        {
            ConvertIfNeeded(message.ImportedBook, message.BookInfo?.Edition, message.BookInfo?.Author?.Name);
        }

        public bool ConvertIfNeeded(BookFile bookFile, Edition edition, string authorName)
        {
            var targetFormat = _configService.PreferredBookFormat;

            if (targetFormat.IsNullOrWhiteSpace())
            {
                return false;
            }

            var sourcePath = bookFile.Path;
            var sourceExtension = Path.GetExtension(sourcePath);

            if (sourceExtension.TrimStart('.').Equals(targetFormat, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!ConvertibleExtensions.Contains(sourceExtension))
            {
                _logger.Debug("Skipping conversion of {0}: {1} is not a convertible ebook format", sourcePath, sourceExtension);
                return false;
            }

            if (!_converter.IsAvailable())
            {
                _logger.Warn("Preferred format is set to {0} but ebook-convert is not available. Install calibre (e.g. the linuxserver universal-calibre docker mod) to enable conversion.", targetFormat);
                return false;
            }

            var targetPath = Path.ChangeExtension(sourcePath, targetFormat.ToLowerInvariant());

            if (_diskProvider.FileExists(targetPath))
            {
                _logger.Debug("Skipping conversion of {0}: {1} already exists", sourcePath, targetPath);
                return false;
            }

            var metadata = new EbookConversionMetadata
            {
                Title = edition?.Title,
                Authors = authorName,
                Isbn13 = edition?.Isbn13,
                Language = edition?.Language
            };

            if (!_converter.Convert(sourcePath, targetPath, metadata) || !_diskProvider.FileExists(targetPath))
            {
                _logger.Warn("Conversion of {0} to {1} failed; keeping original only", sourcePath, targetFormat);
                return false;
            }

            var convertedFile = new BookFile
            {
                Path = targetPath,
                Size = _diskProvider.GetFileSize(targetPath),
                Modified = _diskProvider.FileGetLastWrite(targetPath),
                DateAdded = DateTime.UtcNow,
                ReleaseGroup = bookFile.ReleaseGroup,
                Quality = QualityParser.ParseQuality(Path.GetFileName(targetPath)),
                EditionId = bookFile.EditionId,
                Part = bookFile.Part,
                PartCount = bookFile.PartCount
            };

            _mediaFileService.Add(convertedFile);
            _logger.Info("Converted {0} to {1}", sourcePath, targetPath);

            if (_configService.DeleteOriginalAfterConvert)
            {
                _logger.Debug("Deleting original file after conversion: {0}", sourcePath);
                _recycleBinProvider.DeleteFile(sourcePath);
                _mediaFileService.Delete(bookFile, DeleteMediaFileReason.Upgrade);
            }

            return true;
        }
    }
}
