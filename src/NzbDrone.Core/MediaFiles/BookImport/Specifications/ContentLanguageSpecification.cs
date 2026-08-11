using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Languages.ContentDetection;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.BookImport.Specifications
{
    public class ContentLanguageSpecification : IImportDecisionEngineSpecification<LocalBook>
    {
        private const double ConfidenceThreshold = 0.7;

        private readonly IConfigService _configService;
        private readonly IEbookTextExtractor _textExtractor;
        private readonly ITextLanguageDetector _detector;
        private readonly Logger _logger;

        public ContentLanguageSpecification(IConfigService configService,
                                            IEbookTextExtractor textExtractor,
                                            ITextLanguageDetector detector,
                                            Logger logger)
        {
            _configService = configService;
            _textExtractor = textExtractor;
            _detector = detector;
            _logger = logger;
        }

        public Decision IsSatisfiedBy(LocalBook localBook, DownloadClientItem downloadClientItem)
        {
            var action = _configService.ContentLanguageDetection;

            if (action == ContentLanguageDetectionType.Disabled)
            {
                return Decision.Accept();
            }

            var wantedLanguage = localBook.Author?.QualityProfile?.Value?.Language;

            if (wantedLanguage == null || wantedLanguage == Language.Any || wantedLanguage == Language.Unknown)
            {
                return Decision.Accept();
            }

            if (!_textExtractor.CanExtract(localBook.Path))
            {
                _logger.Debug("Content language detection does not support {0}, skipping check", localBook.Path);
                return Decision.Accept();
            }

            var sample = _textExtractor.ExtractSample(localBook.Path, 40000);
            var result = _detector.Detect(sample);

            if (result == null)
            {
                _logger.Debug("Could not detect content language of {0}, skipping check", localBook.Path);
                return Decision.Accept();
            }

            if (result.Language == wantedLanguage)
            {
                _logger.Debug("Content language of {0} is {1} (confidence {2:P0}), matches required language", localBook.Path, result.Language, result.Confidence);
                return Decision.Accept();
            }

            if (result.Confidence < ConfidenceThreshold)
            {
                _logger.Debug("Content language of {0} looks like {1} but confidence is low ({2:P0}), skipping check", localBook.Path, result.Language, result.Confidence);
                return Decision.Accept();
            }

            var declared = _textExtractor.GetDeclaredLanguage(localBook.Path);

            if (action == ContentLanguageDetectionType.Reject)
            {
                _logger.Debug("Rejecting {0}: content is {1} (confidence {2:P0}, declared '{3}'), profile requires {4}", localBook.Path, result.Language, result.Confidence, declared, wantedLanguage);
                return Decision.Reject("Content language is {0} but the quality profile requires {1}", result.Language, wantedLanguage);
            }

            _logger.Warn("Content of {0} is {1} (confidence {2:P0}, declared '{3}') but the quality profile requires {4}; importing anyway (Log Only)", localBook.Path, result.Language, result.Confidence, declared, wantedLanguage);
            return Decision.Accept();
        }
    }
}
