using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Models;

public sealed record ApplicationImportRegistration(Uuid BatchId, long Revision,
    string ContentSha256);
