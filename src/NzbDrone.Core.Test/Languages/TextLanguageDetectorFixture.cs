using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Languages.ContentDetection;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Languages
{
    [TestFixture]
    public class TextLanguageDetectorFixture : CoreTest<TextLanguageDetector>
    {
        private static string Repeat(string sentence, int times)
        {
            return string.Join(" ", Enumerable.Repeat(sentence, times));
        }

        [Test]
        public void should_detect_french()
        {
            var text = Repeat("Elle marchait dans les rues de la ville avec une idée en tête, mais elle ne savait pas pour qui ni pourquoi cette histoire était plus étrange que les autres.", 30);

            var result = Subject.Detect(text);

            result.Should().NotBeNull();
            result.Language.Should().Be(Language.French);
            result.Confidence.Should().BeGreaterThan(0.6);
        }

        [Test]
        public void should_detect_english()
        {
            var text = Repeat("She walked through the streets of the city with an idea in her head, but they would never know what could have been there from the start.", 30);

            var result = Subject.Detect(text);

            result.Should().NotBeNull();
            result.Language.Should().Be(Language.English);
            result.Confidence.Should().BeGreaterThan(0.6);
        }

        [Test]
        public void should_detect_german()
        {
            var text = Repeat("Sie ging durch die Straßen der Stadt und wusste nicht, wie sie mit dem Problem umgehen sollte, aber es war auch nicht das erste Mal.", 30);

            var result = Subject.Detect(text);

            result.Should().NotBeNull();
            result.Language.Should().Be(Language.German);
        }

        [Test]
        public void should_detect_russian_by_script()
        {
            var text = Repeat("Она шла по улицам города и не знала, что делать дальше, но это была не первая её странная история.", 20);

            var result = Subject.Detect(text);

            result.Should().NotBeNull();
            result.Language.Should().Be(Language.Russian);
        }

        [Test]
        public void should_return_null_for_short_text()
        {
            Subject.Detect("Bonjour le monde").Should().BeNull();
        }

        [Test]
        public void should_return_null_for_empty_text()
        {
            Subject.Detect(null).Should().BeNull();
            Subject.Detect("").Should().BeNull();
        }
    }
}
