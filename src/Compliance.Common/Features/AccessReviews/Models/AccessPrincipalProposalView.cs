using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews.Models;

/// <summary>A provider-hint proposal; <c>Reason</c> explains it. It is never applied automatically.</summary>
public sealed record AccessPrincipalProposalView(string Classification, Uuid? PersonId,
    string Reason);
