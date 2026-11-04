using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>One exact revision of a record covered by a product-rule waiver.</summary>
public sealed record WaiverScopeRecord(string RecordType, Uuid RecordId, long Revision);
