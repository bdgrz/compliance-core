using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Readers;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Fitz;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

abstract class RiskDraftProjectionStore(IKvClient client, IImmutableContentStore content, RiskDraftReader sources, string address, string name)
    : FitzKvProjectionStore(client, address, name), IProjectionStore
{
    Uuid _tenant;
    public new async ValueTask<IProjectionBatch> BeginAsync(ProjectionBatchContext context, CancellationToken ct = default)
    {
        if (!Uuid.TryParse(context.Identity.Pattern.Realm, CultureInfo.InvariantCulture, out var tenant) || tenant == Uuid.Empty)
            throw new InvalidOperationException("A risk draft projection requires its bound tenant.");
        var batch = await base.BeginAsync(context, ct).ConfigureAwait(false);
        _tenant = tenant;
        return batch;
    }
    async ValueTask<IProjectionBatch> IProjectionStore.BeginAsync(ProjectionBatchContext context, CancellationToken ct) =>
        await BeginAsync(context, ct).ConfigureAwait(false);
    protected async ValueTask<RiskDraftDirectoryIdentity> IdentityAsync(DomainEvent value, CancellationToken ct)
    {
        if (value is not IRiskDraftFact fact || fact.Header is not { } header || header.TenantId != _tenant)
            throw new ImmutableContentIntegrityException("The risk draft native projection source has another exact tenant, aggregate or kind.");
        RiskDraftFactContent.Metadata(value);
        await RiskDraftFactContent.ReadAsync(content, fact, ct).ConfigureAwait(false);
        return new(fact.Header, fact.Content, fact.NativeSequence, NativeFactReference.Of(
            new EventStreamAddress(fact.Header.TenantId.ToString(), RiskDraft.Area, fact.Header.RiskId.ToString()), value));
    }
    protected ValueTask ValidateSourceAsync(DomainEvent value, CancellationToken ct) => sources.ValidateFactAsync(value, ct);
    protected async ValueTask<RiskDraftRevisionView> ReadAsync(Uuid tenant, RiskDraftDirectoryIdentity row, CancellationToken ct)
    {
        if (row.Header is null || row.Content is null || row.Source is null || row.Header.TenantId != tenant || row.Source.Stream != new EventStreamAddress(tenant.ToString(), RiskDraft.Area, row.Header.RiskId.ToString()) ||
            row.Content.Schema != RiskDraftFactContent.Schema || row.Source.EventId == Uuid.Empty || row.Source.AggregateVersion == 0 ||
            row.Header.Revision < 1 || row.NativeSequence != row.Header.Revision)
            throw new ImmutableContentIntegrityException("The risk draft row has another exact native source scope.");
        var revision = await content.ReadJsonAsync<RiskDraftRevisionView>(tenant.ToString(), row.Content, ct).ConfigureAwait(false);
        if (revision is null || RiskDraftFactContent.Header(revision) != row.Header || RiskDraftRules.Validate(revision.Identifier, revision.Content) is not null ||
            ContentDigest.OfJson(revision.Content) != ContentDigest.OfJson(RiskDraftRules.Clean(revision.Content)))
            throw new ImmutableContentIntegrityException("The risk draft row differs from its exact immutable revision.");
        return revision;
    }
}
