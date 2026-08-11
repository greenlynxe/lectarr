using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.Commands
{
    public class ConvertBookFilesCommand : Command
    {
        public ConvertBookFilesCommand()
        {
        }

        public ConvertBookFilesCommand(List<int> authorIds)
        {
            AuthorIds = authorIds;
        }

        public List<int> AuthorIds { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;
    }
}
