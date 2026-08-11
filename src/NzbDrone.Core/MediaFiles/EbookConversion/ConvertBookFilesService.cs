using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.EbookConversion
{
    public class ConvertBookFilesService : IExecute<ConvertBookFilesCommand>
    {
        private readonly IConfigService _configService;
        private readonly IEbookConverter _converter;
        private readonly IEbookConversionService _conversionService;
        private readonly IAuthorService _authorService;
        private readonly IEditionService _editionService;
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;

        public ConvertBookFilesService(IConfigService configService,
                                       IEbookConverter converter,
                                       IEbookConversionService conversionService,
                                       IAuthorService authorService,
                                       IEditionService editionService,
                                       IMediaFileService mediaFileService,
                                       Logger logger)
        {
            _configService = configService;
            _converter = converter;
            _conversionService = conversionService;
            _authorService = authorService;
            _editionService = editionService;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public void Execute(ConvertBookFilesCommand message)
        {
            if (_configService.PreferredBookFormat.IsNullOrWhiteSpace())
            {
                _logger.Debug("No preferred book format configured, nothing to convert");
                return;
            }

            if (!_converter.IsAvailable())
            {
                _logger.Warn("Preferred format is configured but ebook-convert is not available; skipping conversion sweep");
                return;
            }

            var authors = message.AuthorIds is { Count: > 0 }
                ? message.AuthorIds.Select(_authorService.GetAuthor).ToList()
                : _authorService.GetAllAuthors();

            var converted = 0;

            foreach (var author in authors)
            {
                var files = _mediaFileService.GetFilesByAuthor(author.Id);

                foreach (var file in files)
                {
                    var edition = file.EditionId > 0 ? _editionService.GetEdition(file.EditionId) : null;

                    if (_conversionService.ConvertIfNeeded(file, edition, author.Name))
                    {
                        converted++;
                    }
                }
            }

            _logger.ProgressInfo("Conversion sweep completed, {0} file(s) converted", converted);
        }
    }
}
