using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Languages.ContentDetection
{
    public class LanguageDetectionResult
    {
        public Language Language { get; set; }
        public double Confidence { get; set; }
        public int MatchedWords { get; set; }
    }

    public interface ITextLanguageDetector
    {
        LanguageDetectionResult Detect(string text);
    }

    public class TextLanguageDetector : ITextLanguageDetector
    {
        private const int MinimumMatches = 20;

        // Distinctive high-frequency function words per language. Words shared
        // between languages (e.g. 'a', 'in', 'la') are deliberately avoided so
        // every hit is a strong signal.
        private static readonly Dictionary<int, string[]> Profiles = new ()
        {
            [Language.English.Id] = new[] { "the", "and", "of", "was", "with", "his", "her", "they", "would", "there", "which", "have", "been", "that", "from", "she", "him", "were", "what", "could" },
            [Language.French.Id] = new[] { "le", "les", "des", "une", "est", "dans", "qui", "pas", "pour", "avec", "sur", "mais", "vous", "elle", "était", "avait", "cette", "être", "sont", "plus", "leur", "comme" },
            [Language.Spanish.Id] = new[] { "los", "las", "una", "por", "para", "está", "pero", "como", "más", "había", "cuando", "ella", "sus", "muy", "sin", "sobre", "también", "hasta", "donde", "era" },
            [Language.German.Id] = new[] { "der", "die", "und", "das", "nicht", "ich", "mit", "sich", "auf", "ein", "eine", "aber", "auch", "wie", "wenn", "noch", "dem", "des", "war", "hatte", "sind" },
            [Language.Italian.Id] = new[] { "che", "della", "una", "per", "non", "sono", "nella", "come", "anche", "era", "gli", "più", "aveva", "quando", "loro", "questo", "essere", "alla", "dei", "lui" },
            [Language.Dutch.Id] = new[] { "het", "een", "van", "niet", "zijn", "maar", "voor", "naar", "ook", "met", "haar", "had", "hij", "toen", "als", "nog", "wat", "dat", "aan", "bij" },
            [Language.Portuguese.Id] = new[] { "que", "não", "uma", "com", "para", "mais", "quando", "ela", "seu", "sua", "estava", "como", "isso", "pelo", "pela", "muito", "até", "dos", "das", "ele" },
            [Language.Swedish.Id] = new[] { "och", "att", "det", "som", "inte", "hon", "han", "med", "för", "var", "hade", "den", "till", "men", "sig", "från", "skulle", "kunde", "vid", "efter" },
            [Language.Norwegian.Id] = new[] { "og", "det", "som", "ikke", "han", "hun", "med", "var", "til", "hadde", "den", "men", "seg", "fra", "skulle", "kunne", "ved", "etter", "opp", "ut" },
            [Language.Danish.Id] = new[] { "og", "det", "som", "ikke", "han", "hun", "med", "var", "til", "havde", "den", "men", "sig", "fra", "skulle", "kunne", "ved", "efter", "op", "ud" },
            [Language.Finnish.Id] = new[] { "että", "hän", "oli", "mutta", "kun", "niin", "kuin", "sen", "joka", "ovat", "tämä", "mitä", "vain", "myös", "jos", "sitten", "hänen", "olla", "sitä", "nyt" },
            [Language.Polish.Id] = new[] { "się", "nie", "jest", "był", "była", "ale", "jak", "tak", "przez", "jego", "tylko", "czy", "już", "może", "być", "przy", "które", "który", "bardzo", "jeszcze" },
            [Language.Czech.Id] = new[] { "se", "na", "je", "že", "byl", "byla", "ale", "jak", "tak", "jeho", "jen", "už", "může", "být", "při", "které", "který", "velmi", "ještě", "podle" },
            [Language.Romanian.Id] = new[] { "și", "nu", "este", "era", "dar", "cum", "așa", "prin", "lui", "doar", "dacă", "mai", "poate", "care", "foarte", "încă", "după", "până", "fost", "sunt" },
            [Language.Hungarian.Id] = new[] { "hogy", "nem", "volt", "egy", "azt", "csak", "már", "van", "mint", "még", "aki", "ezt", "vagy", "amikor", "lehet", "minden", "olyan", "őket", "neki", "ott" },
            [Language.Turkish.Id] = new[] { "bir", "bu", "için", "gibi", "daha", "ama", "çok", "değil", "sonra", "kadar", "ile", "var", "ben", "onu", "şey", "olarak", "olan", "diye", "bile", "biraz" }
        };

        public LanguageDetectionResult Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var scriptLanguage = DetectByScript(text);

            if (scriptLanguage != null)
            {
                return scriptLanguage;
            }

            var tokens = Tokenize(text);

            if (tokens.Count < 50)
            {
                return null;
            }

            var scores = new Dictionary<int, int>();

            foreach (var (languageId, words) in Profiles)
            {
                var wordSet = new HashSet<string>(words, StringComparer.Ordinal);
                scores[languageId] = tokens.Count(t => wordSet.Contains(t));
            }

            var ranked = scores.OrderByDescending(s => s.Value).ToList();
            var top = ranked[0];
            var second = ranked[1];

            if (top.Value < MinimumMatches)
            {
                return null;
            }

            var confidence = (double)top.Value / (top.Value + second.Value);

            return new LanguageDetectionResult
            {
                Language = Language.All.Single(l => l.Id == top.Key),
                Confidence = confidence,
                MatchedWords = top.Value
            };
        }

        private static LanguageDetectionResult DetectByScript(string text)
        {
            int cyrillic = 0, greek = 0, cjk = 0, hebrew = 0, thai = 0, kana = 0, hangul = 0, letters = 0;

            foreach (var c in text)
            {
                if (!char.IsLetter(c))
                {
                    continue;
                }

                letters++;

                if (c >= 0x0400 && c <= 0x04FF)
                {
                    cyrillic++;
                }
                else if (c >= 0x0370 && c <= 0x03FF)
                {
                    greek++;
                }
                else if (c >= 0x3040 && c <= 0x30FF)
                {
                    kana++;
                }
                else if (c >= 0xAC00 && c <= 0xD7AF)
                {
                    hangul++;
                }
                else if (c >= 0x4E00 && c <= 0x9FFF)
                {
                    cjk++;
                }
                else if (c >= 0x0590 && c <= 0x05FF)
                {
                    hebrew++;
                }
                else if (c >= 0x0E00 && c <= 0x0E7F)
                {
                    thai++;
                }
            }

            if (letters < 200)
            {
                return null;
            }

            LanguageDetectionResult Result(Language language, int count) => new ()
            {
                Language = language,
                Confidence = (double)count / letters,
                MatchedWords = count
            };

            if ((double)cyrillic / letters > 0.5)
            {
                return Result(Language.Russian, cyrillic);
            }

            if ((double)greek / letters > 0.5)
            {
                return Result(Language.Greek, greek);
            }

            if ((double)kana / letters > 0.2)
            {
                return Result(Language.Japanese, kana + cjk);
            }

            if ((double)hangul / letters > 0.5)
            {
                return Result(Language.Korean, hangul);
            }

            if ((double)cjk / letters > 0.5)
            {
                return Result(Language.Chinese, cjk);
            }

            if ((double)hebrew / letters > 0.5)
            {
                return Result(Language.Hebrew, hebrew);
            }

            if ((double)thai / letters > 0.5)
            {
                return Result(Language.Thai, thai);
            }

            return null;
        }

        private static List<string> Tokenize(string text)
        {
            return text.ToLowerInvariant()
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '“', '”', '(', ')', '—', '–' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }
    }
}
