using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.FirmServices.Models;

/// <summary>Exact source revision, including revisions that void a record.</summary>
public sealed record NonattestServiceVersion(Uuid RecordId, long Revision);
