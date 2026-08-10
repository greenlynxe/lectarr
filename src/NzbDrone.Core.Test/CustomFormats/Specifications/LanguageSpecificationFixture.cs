using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.CustomFormats.Specifications
{
    [TestFixture]
    public class LanguageSpecificationFixture : CoreTest<LanguageSpecification>
    {
        private CustomFormatInput _input;

        [SetUp]
        public void Setup()
        {
            _input = new CustomFormatInput
            {
                BookInfo = new ParsedBookInfo(),
                Languages = new List<Language>
                {
                    Language.French
                }
            };
        }

        [Test]
        public void should_match_single_language()
        {
            Subject.Value = Language.French.Id;
            Subject.Negate = false;

            Subject.IsSatisfiedBy(_input).Should().BeTrue();
        }

        [Test]
        public void should_not_match_different_language()
        {
            Subject.Value = Language.English.Id;
            Subject.Negate = false;

            Subject.IsSatisfiedBy(_input).Should().BeFalse();
        }

        [Test]
        public void should_not_match_negated_present_language()
        {
            Subject.Value = Language.French.Id;
            Subject.Negate = true;

            Subject.IsSatisfiedBy(_input).Should().BeFalse();
        }

        [Test]
        public void should_match_negated_absent_language()
        {
            Subject.Value = Language.English.Id;
            Subject.Negate = true;

            Subject.IsSatisfiedBy(_input).Should().BeTrue();
        }

        [Test]
        public void should_match_multi_language_release()
        {
            _input.Languages = new List<Language> { Language.English, Language.French };

            Subject.Value = Language.French.Id;
            Subject.Negate = false;

            Subject.IsSatisfiedBy(_input).Should().BeTrue();
        }

        [Test]
        public void should_match_except_language_when_other_language_present()
        {
            _input.Languages = new List<Language> { Language.English, Language.French };

            Subject.Value = Language.English.Id;
            Subject.ExceptLanguage = true;
            Subject.Negate = false;

            Subject.IsSatisfiedBy(_input).Should().BeTrue();
        }

        [Test]
        public void should_not_match_except_language_when_only_that_language_present()
        {
            _input.Languages = new List<Language> { Language.French };

            Subject.Value = Language.French.Id;
            Subject.ExceptLanguage = true;
            Subject.Negate = false;

            Subject.IsSatisfiedBy(_input).Should().BeFalse();
        }

        [Test]
        public void should_not_match_unknown_language_when_requiring_french()
        {
            _input.Languages = new List<Language> { Language.Unknown };

            Subject.Value = Language.French.Id;
            Subject.Negate = false;

            Subject.IsSatisfiedBy(_input).Should().BeFalse();
        }
    }
}
