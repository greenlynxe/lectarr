using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class LanguageSpecificationFixture : CoreTest<LanguageSpecification>
    {
        private RemoteBook _remoteBook;

        [SetUp]
        public void Setup()
        {
            var fakeAuthor = Builder<Author>.CreateNew()
                         .With(c => c.QualityProfile = new QualityProfile { Language = Language.French })
                         .Build();

            _remoteBook = new RemoteBook
            {
                Author = fakeAuthor,
                ParsedBookInfo = new ParsedBookInfo
                {
                    Languages = new List<Language> { Language.French }
                },
                Release = new ReleaseInfo()
            };
        }

        private void WithProfileLanguage(Language language)
        {
            _remoteBook.Author.QualityProfile.Value.Language = language;
        }

        [Test]
        public void should_allow_when_profile_language_is_any()
        {
            WithProfileLanguage(Language.Any);
            _remoteBook.ParsedBookInfo.Languages = new List<Language> { Language.English };

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_allow_when_release_contains_wanted_language()
        {
            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_release_does_not_contain_wanted_language()
        {
            _remoteBook.ParsedBookInfo.Languages = new List<Language> { Language.English };

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_reject_unknown_language_when_specific_language_is_wanted()
        {
            _remoteBook.ParsedBookInfo.Languages = new List<Language> { Language.Unknown };

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_allow_multi_language_release_containing_wanted_language()
        {
            _remoteBook.ParsedBookInfo.Languages = new List<Language> { Language.English, Language.French };

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_prefer_release_info_languages_over_parsed_languages()
        {
            // The indexer explicitly says French even though nothing was
            // parsed from the title.
            _remoteBook.ParsedBookInfo.Languages = new List<Language> { Language.Unknown };
            _remoteBook.Release.Languages = new List<Language> { Language.French };

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_release_info_language_differs_from_wanted()
        {
            // Explicit indexer metadata wins over the title parsing.
            _remoteBook.ParsedBookInfo.Languages = new List<Language> { Language.French };
            _remoteBook.Release.Languages = new List<Language> { Language.English };

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_allow_when_nothing_is_parsed_and_profile_language_is_any()
        {
            WithProfileLanguage(Language.Any);
            _remoteBook.ParsedBookInfo = null;

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_nothing_is_parsed_and_specific_language_is_wanted()
        {
            _remoteBook.ParsedBookInfo = null;
            _remoteBook.Release = null;

            Subject.IsSatisfiedBy(_remoteBook, null).Accepted.Should().BeFalse();
        }
    }
}
