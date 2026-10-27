using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls.Models;

public sealed record ControlApplicabilityReference(Uuid EntryId, string SubjectType,
    string Subject, Uuid? GovernedRecordId, string Rationale, bool Unresolved);
