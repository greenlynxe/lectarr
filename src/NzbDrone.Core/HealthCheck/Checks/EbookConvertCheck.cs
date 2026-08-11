using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles.EbookConversion;

namespace NzbDrone.Core.HealthCheck.Checks
{
    [CheckOn(typeof(ConfigSavedEvent))]
    public class EbookConvertCheck : HealthCheckBase
    {
        private readonly IConfigService _configService;
        private readonly IEbookConverter _converter;

        public EbookConvertCheck(IConfigService configService,
                                 IEbookConverter converter,
                                 ILocalizationService localizationService)
            : base(localizationService)
        {
            _configService = configService;
            _converter = converter;
        }

        public override HealthCheck Check()
        {
            if (_configService.PreferredBookFormat.IsNullOrWhiteSpace())
            {
                return new HealthCheck(GetType());
            }

            if (!_converter.IsAvailable())
            {
                return new HealthCheck(GetType(),
                    HealthCheckResult.Warning,
                    "Preferred book format is configured but ebook-convert (calibre) was not found. Install it, e.g. with the linuxserver universal-calibre docker mod.",
                    "#ebook-convert-missing");
            }

            return new HealthCheck(GetType());
        }
    }
}
