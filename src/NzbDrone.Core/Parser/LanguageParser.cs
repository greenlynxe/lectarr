using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Parser
{
    public static class LanguageParser
    {
        private static readonly Regex LanguageRegex = new Regex(@"(?:\W|_|^)(?<english>\beng\b)|
                                                                            (?<italian>\b(?:ita|italian)\b)|
                                                                            (?<german>german\b|\bger\b|\bdeutsch\b)|
                                                                            (?<flemish>flemish)|
                                                                            (?<greek>greek)|
                                                                            (?<french>\b(?:VF|VFF|VFQ|VF2|TRUEFRENCH|FRENCH|FRE|FRA|FRANCAIS|FRAN[ÇC]AIS)\b)|
                                                                            (?<russian>\b(?:rus|russian)\b)|
                                                                            (?<hungarian>\b(?:HUNDUB|HUN)\b)|
                                                                            (?<polish>\b(?:polish|POL)\b)|
                                                                            (?<chinese>\[(?:CH[ST]|BIG5|GB)\]|简|繁|字幕)|
                                                                            (?<spanish>\b(?:español|castellano|spanish)\b)|
                                                                            (?<vietnamese>\bVIE\b)|
                                                                            (?<japanese>\b(?:JAP|japanese)\b)|
                                                                            (?<korean>\b(?:KOR|korean)\b)|
                                                                            (?<portuguese>\b(?:portuguese|POR|PT)\b)|
                                                                            (?<dutch>\b(?:dutch|NLD?)\b)|
                                                                            (?<swedish>\b(?:swedish|SWE)\b)|
                                                                            (?<norwegian>\b(?:norwegian|NOR)\b)|
                                                                            (?<danish>\b(?:danish|DAN)\b)|
                                                                            (?<finnish>\b(?:finnish|FIN)\b)|
                                                                            (?<turkish>\b(?:turkish|TUR)\b)|
                                                                            (?<czech>\b(?:czech|CZE)\b)|
                                                                            (?<multi>\bMULTI\b(?![.\-_ ]?format))",
                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        // Short language codes are only matched case-sensitively (and never
        // when they are part of a lowercase domain name such as site.fr),
        // since two-letter tokens are far too ambiguous otherwise.
        private static readonly Regex CaseSensitiveLanguageRegex = new Regex(@"(?:(?i)(?<!SUB[\W|_|^]))(?:(?<english>\bEN\b)|
                                                                                                          (?<french>\bFR\b)|
                                                                                                          (?<lithuanian>\bLT\b)|
                                                                                                          (?<czech>\bCZ\b)|
                                                                                                          (?<polish>\bPL\b)|
                                                                                                          (?<bulgarian>\bBG\b)|
                                                                                                          (?<german>\bDE\b)|
                                                                                                          (?<italian>\bIT\b)|
                                                                                                          (?<spanish>\b(?<!DTS[._ -])ES\b))(?:(?i)(?![\W|_|^]SUB))",
                                                                RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

        public static List<Language> ParseLanguages(string title)
        {
            var lowerTitle = title.ToLower();
            var languages = new List<Language>();

            if (lowerTitle.Contains("english"))
            {
                languages.Add(Language.English);
            }

            if (lowerTitle.Contains("français") || lowerTitle.Contains("francais"))
            {
                languages.Add(Language.French);
            }

            var regexMatches = LanguageRegex.Matches(title);

            foreach (Match match in regexMatches)
            {
                if (match.Groups["french"].Success)
                {
                    languages.Add(Language.French);
                }

                if (match.Groups["english"].Success)
                {
                    languages.Add(Language.English);
                }

                if (match.Groups["german"].Success)
                {
                    languages.Add(Language.German);
                }

                if (match.Groups["italian"].Success)
                {
                    languages.Add(Language.Italian);
                }

                if (match.Groups["flemish"].Success)
                {
                    languages.Add(Language.Flemish);
                }

                if (match.Groups["greek"].Success)
                {
                    languages.Add(Language.Greek);
                }

                if (match.Groups["spanish"].Success)
                {
                    languages.Add(Language.Spanish);
                }

                if (match.Groups["russian"].Success)
                {
                    languages.Add(Language.Russian);
                }

                if (match.Groups["hungarian"].Success)
                {
                    languages.Add(Language.Hungarian);
                }

                if (match.Groups["polish"].Success)
                {
                    languages.Add(Language.Polish);
                }

                if (match.Groups["chinese"].Success)
                {
                    languages.Add(Language.Chinese);
                }

                if (match.Groups["vietnamese"].Success)
                {
                    languages.Add(Language.Vietnamese);
                }

                if (match.Groups["japanese"].Success)
                {
                    languages.Add(Language.Japanese);
                }

                if (match.Groups["korean"].Success)
                {
                    languages.Add(Language.Korean);
                }

                if (match.Groups["portuguese"].Success)
                {
                    languages.Add(Language.Portuguese);
                }

                if (match.Groups["dutch"].Success)
                {
                    languages.Add(Language.Dutch);
                }

                if (match.Groups["swedish"].Success)
                {
                    languages.Add(Language.Swedish);
                }

                if (match.Groups["norwegian"].Success)
                {
                    languages.Add(Language.Norwegian);
                }

                if (match.Groups["danish"].Success)
                {
                    languages.Add(Language.Danish);
                }

                if (match.Groups["finnish"].Success)
                {
                    languages.Add(Language.Finnish);
                }

                if (match.Groups["turkish"].Success)
                {
                    languages.Add(Language.Turkish);
                }

                if (match.Groups["czech"].Success)
                {
                    languages.Add(Language.Czech);
                }

                if (match.Groups["multi"].Success)
                {
                    // On French (and most non-English) trackers MULTI denotes a
                    // release containing both the original (usually English)
                    // and the translated text.
                    languages.Add(Language.English);
                    languages.Add(Language.French);
                }
            }

            var caseSensitiveMatch = CaseSensitiveLanguageRegex.Match(title);

            if (caseSensitiveMatch.Groups["english"].Success)
            {
                languages.Add(Language.English);
            }

            if (caseSensitiveMatch.Groups["french"].Success)
            {
                languages.Add(Language.French);
            }

            if (caseSensitiveMatch.Groups["lithuanian"].Success)
            {
                languages.Add(Language.Lithuanian);
            }

            if (caseSensitiveMatch.Groups["czech"].Success)
            {
                languages.Add(Language.Czech);
            }

            if (caseSensitiveMatch.Groups["polish"].Success)
            {
                languages.Add(Language.Polish);
            }

            if (caseSensitiveMatch.Groups["bulgarian"].Success)
            {
                languages.Add(Language.Bulgarian);
            }

            if (caseSensitiveMatch.Groups["german"].Success)
            {
                languages.Add(Language.German);
            }

            if (caseSensitiveMatch.Groups["italian"].Success)
            {
                languages.Add(Language.Italian);
            }

            if (caseSensitiveMatch.Groups["spanish"].Success)
            {
                languages.Add(Language.Spanish);
            }

            if (!languages.Any())
            {
                languages.Add(Language.Unknown);
            }

            return languages.DistinctBy(l => l.Id).ToList();
        }
    }
}
