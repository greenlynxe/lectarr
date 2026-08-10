using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class LanguageParserFixture : CoreTest
    {
        [TestCase("Victor Hugo - Les Misérables 1862 FRENCH ePub-GRP")]
        [TestCase("Liu Cixin - Le Problème à trois corps (2016) TRUEFRENCH [EPUB]")]
        [TestCase("Stephen King - Ca FRENCH EBOOK-Group")]
        [TestCase("J.K. Rowling - Harry Potter à l'école des sorciers [FR] EPUB")]
        [TestCase("Author - Title (2020) [ePub FR]")]
        [TestCase("Auteur - Livre Audio VF mp3 64kbps")]
        [TestCase("Some.Author.Some.Book.2019.FRENCH.eBook-GRP")]
        [TestCase("Pierre Bottero - La Quête d'Ewilan T1 [ebook francais]")]
        [TestCase("Amélie Nothomb - Stupeur et tremblements (français) epub")]
        [TestCase("Fred Vargas - Pars vite et reviens tard FRA epub")]
        public void should_parse_french(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().Contain(Language.French);
        }

        [TestCase("Author - Book Title 2020 MULTI EPUB")]
        [TestCase("Author.Book.Title.2020.MULTI.FR.EN.ePub-GRP")]
        public void should_parse_multi_as_french_and_english(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().Contain(Language.French);
            result.Should().Contain(Language.English);
        }

        [TestCase("Author - Book Title (2020) English EPUB")]
        [TestCase("Author.Book.Title.2018.ENG.ePub-GRP")]
        [TestCase("Author - Book Title [EN] mobi")]
        public void should_parse_english(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().Contain(Language.English);
            result.Should().NotContain(Language.French);
        }

        [TestCase("Autor - Titel GERMAN eBook-GRP")]
        [TestCase("Autor - Titel [DE] epub")]
        public void should_parse_german(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().Contain(Language.German);
        }

        [TestCase("Author - Some Book Title (2021) EPUB")]
        [TestCase("Author - Book Title Retail azw3")]
        public void should_default_to_unknown(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().BeEquivalentTo(new[] { Language.Unknown });
        }

        // Lowercase two-letter tokens are ambiguous (domain names like
        // ebook-site.fr, initials, etc.) and must not be picked up.
        [TestCase("www.torrent-site.fr - Author - Book Title EPUB")]
        [TestCase("Author - Book Title [ebook-site.fr] azw3")]
        public void should_not_parse_lowercase_fr_as_french(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().NotContain(Language.French);
        }

        [TestCase("Author - Book Title MULTI-FORMAT epub mobi")]
        public void should_not_parse_multi_format_as_multi_language(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().BeEquivalentTo(new[] { Language.Unknown });
        }

        [TestCase("Author - Book Title FRENCH ENGLISH epub")]
        public void should_parse_multiple_languages(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().Contain(Language.French);
            result.Should().Contain(Language.English);
        }

        [TestCase("Author - Book Title FRENCH FR TRUEFRENCH epub")]
        public void should_not_return_duplicates(string title)
        {
            var result = LanguageParser.ParseLanguages(title);

            result.Should().OnlyHaveUniqueItems(l => l.Id);
        }
    }
}
