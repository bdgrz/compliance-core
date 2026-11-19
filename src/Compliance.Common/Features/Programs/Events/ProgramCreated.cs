using System.Globalization;
using System.Text.Json.Serialization;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>The exact native program acceptance; authored value bytes remain in verified immutable content.</summary>
[Discriminator("bdgrz.program.created", 2)]
public sealed record ProgramCreated(Uuid TenantId, Uuid ProgramId, long Revision,
    ImmutableContentRef Content, DateTimeOffset ChangedAt, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, ulong NativeSequence, int ChangeCount)
    : DomainEvent, IProgramFact
{
    /// <summary>The edition selected by this creation, absent in legacy creation facts.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uuid? CriteriaEditionId { get; init; }

    /// <summary>The exact committed source publication; absent in legacy creation facts.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CriteriaPublicationProof? CriteriaPublication { get; init; }

    /// <summary>The source-authoritative creation admission; absent in legacy creation facts.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AccessAdmissionEvidence? AccessAdmission { get; init; }

    /// <summary>The fenced creation generation; zero for legacy or unfenced initial creations.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ulong CreationGeneration { get; init; }

    [JsonIgnore]
    public ActorReference Actor => Operation.Actor;
    [JsonIgnore]
    public Uuid ActorMemberId => Uuid.TryParse(Actor.Id, CultureInfo.InvariantCulture, out var member) ? member : Uuid.Empty;
    [JsonIgnore]
    public string ActorDisplay => Actor.Display;
}
