using System;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;

namespace NzbDrone.Core.MediaFiles.EbookConversion
{
    public interface IEbookConverter
    {
        bool IsAvailable();
        bool Convert(string sourcePath, string targetPath, EbookConversionMetadata metadata);
    }

    public class EbookConversionMetadata
    {
        public string Title { get; set; }
        public string Authors { get; set; }
        public string Isbn13 { get; set; }
        public string Language { get; set; }
        public string CoverPath { get; set; }
    }

    public class EbookConverter : IEbookConverter
    {
        private const string CONVERTER_BINARY = "ebook-convert";

        private readonly IProcessProvider _processProvider;
        private readonly Logger _logger;

        private bool? _available;

        public EbookConverter(IProcessProvider processProvider, Logger logger)
        {
            _processProvider = processProvider;
            _logger = logger;
        }

        public bool IsAvailable()
        {
            if (_available.HasValue)
            {
                return _available.Value;
            }

            try
            {
                var output = _processProvider.StartAndCapture(CONVERTER_BINARY, "--version");
                _available = output.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.Trace(ex, "ebook-convert not found on PATH");
                _available = false;
            }

            return _available.Value;
        }

        public bool Convert(string sourcePath, string targetPath, EbookConversionMetadata metadata)
        {
            var args = $"{Quote(sourcePath)} {Quote(targetPath)}";

            if (metadata != null)
            {
                if (metadata.Title.IsNotNullOrWhiteSpace())
                {
                    args += $" --title {Quote(metadata.Title)}";
                }

                if (metadata.Authors.IsNotNullOrWhiteSpace())
                {
                    args += $" --authors {Quote(metadata.Authors)}";
                }

                if (metadata.Isbn13.IsNotNullOrWhiteSpace())
                {
                    args += $" --isbn {Quote(metadata.Isbn13)}";
                }

                if (metadata.Language.IsNotNullOrWhiteSpace())
                {
                    args += $" --language {Quote(metadata.Language)}";
                }

                if (metadata.CoverPath.IsNotNullOrWhiteSpace())
                {
                    args += $" --cover {Quote(metadata.CoverPath)}";
                }
            }

            _logger.Debug("Converting {0} to {1}", sourcePath, targetPath);

            var processOutput = _processProvider.StartAndCapture(CONVERTER_BINARY, args);

            if (processOutput.ExitCode != 0)
            {
                _logger.Warn("ebook-convert failed with exit code {0} for {1}", processOutput.ExitCode, sourcePath);
                return false;
            }

            return true;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
