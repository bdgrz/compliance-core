using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Events;

[Discriminator("bdgrz.criteria.criteria-edition-draft-requested", 1)]
public sealed record CriteriaEditionDraftRequested(Uuid EditionId, Uuid DraftGeneration, Uuid OperationId, long ExecutionGeneration,
    [property: JsonRequired] string Framework, string Label, ActorAttribution Actor, AccessAdmissionEvidence? Admission,
    string? SeedVersion, string? SeedFingerprint) : DomainEvent;
