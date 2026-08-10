using System.Collections.Generic;
using NzbDrone.Core.Books;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.CustomFormats
{
    public class CustomFormatInput
    {
        public ParsedBookInfo BookInfo { get; set; }
        public Author Author { get; set; }
        public long Size { get; set; }
        public IndexerFlags IndexerFlags { get; set; }
        public List<Language> Languages { get; set; } = new List<Language>();
        public string Filename { get; set; }
    }
}
