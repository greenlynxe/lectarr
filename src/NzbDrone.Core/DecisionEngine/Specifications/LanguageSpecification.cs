using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class LanguageSpecification : IDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public LanguageSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public virtual Decision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria)
        {
            var wantedLanguage = subject.Author?.QualityProfile?.Value?.Language;

            if (wantedLanguage == null || wantedLanguage == Language.Any || wantedLanguage == Language.Unknown)
            {
                _logger.Debug("Profile allows any language, accepting release.");
                return Decision.Accept();
            }

            var languages = GetLanguages(subject);

            _logger.Debug("Checking if report meets language requirements. {0}", languages.ToExtendedString());

            if (!languages.Contains(wantedLanguage))
            {
                _logger.Debug("Report language: {0} rejected because it is not wanted, wanted {1}", languages.ToExtendedString(), wantedLanguage);
                return Decision.Reject("{0} is wanted, but found {1}", wantedLanguage, languages.ToExtendedString());
            }

            return Decision.Accept();
        }

        // Explicit indexer metadata (e.g. the newznab/torznab language
        // attribute) wins over languages parsed from the release title.
        private static List<Language> GetLanguages(RemoteBook subject)
        {
            var releaseLanguages = subject.Release?.Languages?
                .Where(l => l != null && l != Language.Unknown)
                .ToList();

            if (releaseLanguages != null && releaseLanguages.Any())
            {
                return releaseLanguages.DistinctBy(l => l.Id).ToList();
            }

            var parsedLanguages = subject.ParsedBookInfo?.Languages?
                .Where(l => l != null && l != Language.Unknown)
                .ToList();

            if (parsedLanguages != null && parsedLanguages.Any())
            {
                return parsedLanguages.DistinctBy(l => l.Id).ToList();
            }

            return new List<Language> { Language.Unknown };
        }
    }
}
