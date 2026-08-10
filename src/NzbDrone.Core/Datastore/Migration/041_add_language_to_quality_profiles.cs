using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class add_language_to_quality_profiles : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // -1 is Language.Any, which preserves the existing behavior of
            // profiles created before this migration.
            Alter.Table("QualityProfiles").AddColumn("Language").AsInt32().NotNullable().WithDefaultValue(-1);
        }
    }
}
