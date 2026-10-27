using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>One participant result a campaign admitted: its root operation, person, kind and admission fact.</summary>
public sealed record CampaignAdmittedResult(Uuid OperationId, Uuid PersonId, CampaignParticipantResultKind Kind,
    NativeFactReference Admission);
