using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentDraftRegistration(Uuid DraftId, string Kind,
    string Identifier, long Revision);
