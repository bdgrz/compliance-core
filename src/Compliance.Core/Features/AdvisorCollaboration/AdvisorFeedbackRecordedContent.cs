using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AdvisorCollaboration;

/// <summary>The exact original contribution composed from its actual native fact and admitted immutable bytes.</summary>
public sealed record AdvisorFeedbackRecordedContent(AdvisorFeedbackRecorded Fact, string Body)
{
    public Uuid TenantId => Fact.TenantId;
    public Uuid ProgramId => Fact.ProgramId;
    public Uuid HandoffId => Fact.HandoffId;
    public Uuid FeedbackId => Fact.FeedbackId;
    public Uuid RecordId => Fact.RecordId;
    public Uuid RecordVersionId => Fact.RecordVersionId;
    public string Kind => Fact.Kind;
    public ActorReference Author => Fact.Author;
    public DateTimeOffset CreatedAt => Fact.CreatedAt;
    public AdvisorRecordVersion? SelectedSource => Fact.SelectedSource;
    public OperationProvenance Operation => Fact.Operation;
    public ImmutableContentRef Intent => Fact.Intent;
    public DomainEventMetadata Metadata => Fact.Metadata;
}
