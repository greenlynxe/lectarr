using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Languages.ContentDetection;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.BookImport.Specifications
{
    [TestFixture]
    public class ContentLanguageSpecificationFixture : CoreTest<ContentLanguageSpecification>
    {
        private LocalBook _localBook;

        [SetUp]
        public void Setup()
        {
            _localBook = new LocalBook
            {
                Path = @"C:\Test\book.epub".AsOsAgnostic(),
                Author = new Author
                {
                    QualityProfile = new LazyLoaded<QualityProfile>(new QualityProfile
                    {
                        Language = Language.French
                    })
                }
            };

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.ContentLanguageDetection)
                  .Returns(ContentLanguageDetectionType.Reject);

            Mocker.GetMock<IEbookTextExtractor>()
                  .Setup(s => s.CanExtract(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IEbookTextExtractor>()
                  .Setup(s => s.ExtractSample(It.IsAny<string>(), It.IsAny<int>()))
                  .Returns("sample text");

            GivenDetection(Language.French, 0.95);
        }

        private void GivenDetection(Language language, double confidence)
        {
            Mocker.GetMock<ITextLanguageDetector>()
                  .Setup(s => s.Detect(It.IsAny<string>()))
                  .Returns(language == null ? null : new LanguageDetectionResult { Language = language, Confidence = confidence, MatchedWords = 100 });
        }

        [Test]
        public void should_accept_when_disabled()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.ContentLanguageDetection)
                  .Returns(ContentLanguageDetectionType.Disabled);

            GivenDetection(Language.English, 0.99);

            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_profile_allows_any()
        {
            _localBook.Author.QualityProfile = new LazyLoaded<QualityProfile>(new QualityProfile { Language = Language.Any });

            GivenDetection(Language.English, 0.99);

            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_format_not_supported()
        {
            Mocker.GetMock<IEbookTextExtractor>()
                  .Setup(s => s.CanExtract(It.IsAny<string>()))
                  .Returns(false);

            GivenDetection(Language.English, 0.99);

            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_language_matches()
        {
            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_confidence_low()
        {
            GivenDetection(Language.English, 0.55);

            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_on_confident_mismatch()
        {
            GivenDetection(Language.English, 0.95);

            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_but_warn_in_log_only_mode()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.ContentLanguageDetection)
                  .Returns(ContentLanguageDetectionType.LogOnly);

            GivenDetection(Language.English, 0.95);

            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeTrue();

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_accept_when_detection_inconclusive()
        {
            GivenDetection(null, 0);

            Subject.IsSatisfiedBy(_localBook, null).Accepted.Should().BeTrue();
        }
    }
}
