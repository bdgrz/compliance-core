using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.PolicyDistribution;

/// <summary>
///     Defines a requirement's first version or an immutable successor version. <paramref name="ContentContent" />
///     is the complete <see cref="TrainingRequirementContent" />; <paramref name="DefineRequestId" /> is the
///     logical operation identity that defined this version.
/// </summary>
[Discriminator("bdgrz.training_requirement.defined", 2)]
public sealed record TrainingRequirementDefined(Uuid TenantId, Uuid ProgramId,
    Uuid RequirementId, string Identifier, long Version, ImmutableContentRef ContentContent,
    string ContentSha256, Uuid DefineRequestId, ActorReference Actor, DateTimeOffset ChangedAt,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
