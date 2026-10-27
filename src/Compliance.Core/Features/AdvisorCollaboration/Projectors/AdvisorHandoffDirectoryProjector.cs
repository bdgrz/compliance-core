using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Projections;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Projectors;

/// <summary>
///     Projects each handoff's lifecycle and feedback admissions into the directory read model. The selection of a
///     creation or revision is read from verified content before it is projected; feedback threads are their own
///     owners and are not projected here.
/// </summary>
public sealed partial class AdvisorHandoffDirectoryProjector(IAdvisorHandoffDirectoryProjection projection,
    IImmutableContentStore content)
    : Projector(projection, EventStreamPattern.ForTenant(AdvisorHandoff.Area),
        FitzAdvisorHandoffDirectory.ProjectorName),
      IProjectorHandler<AdvisorHandoffCreated>,
      IProjectorHandler<AdvisorHandoffSelectionRevised>,
      IProjectorHandler<AdvisorHandoffReadiedForReview>,
      IProjectorHandler<AdvisorHandoffClosed>,
      IProjectorHandler<AdvisorHandoffRevoked>,
      IProjectorHandler<AdvisorFeedbackAdmitted>
{
    public async ValueTask HandleAsync(AdvisorHandoffCreated ev, IProjectorContext context,
        CancellationToken ct) => await projection.ApplyAsync(ev, await AdvisorContent
            .ReadAsync<IReadOnlyList<AdvisorRecordVersion>>(content, ev.TenantId, ev.RecordVersionsManifest, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);

    public async ValueTask HandleAsync(AdvisorHandoffSelectionRevised ev, IProjectorContext context,
        CancellationToken ct) => await projection.ApplyAsync(ev, await AdvisorContent
            .ReadAsync<IReadOnlyList<AdvisorRecordVersion>>(content, ev.TenantId, ev.RecordVersionsManifest, ct)
            .ConfigureAwait(false), ct).ConfigureAwait(false);

    public ValueTask HandleAsync(AdvisorHandoffReadiedForReview ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);

    public ValueTask HandleAsync(AdvisorHandoffClosed ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);

    public ValueTask HandleAsync(AdvisorHandoffRevoked ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);

    public ValueTask HandleAsync(AdvisorFeedbackAdmitted ev, IProjectorContext context,
        CancellationToken ct) => projection.ApplyAsync(ev, null, ct);
}
