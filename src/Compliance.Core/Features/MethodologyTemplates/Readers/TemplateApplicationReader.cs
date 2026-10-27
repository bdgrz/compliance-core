using Bdgrz.Compliance.Features.Content;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates;

/// <summary>Composes immutable application history from the captured native owner cut, never a projection.</summary>
public sealed class TemplateApplicationReader(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    public const string Schema = "template-application-change.v1";

    public async ValueTask<TemplateApplicationReadSnapshot> ReadAsync(Uuid tenant, Uuid program, Uuid application,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new TemplateApplication(tenant, program, application), ct).ConfigureAwait(false);
        return await ReadAsync(owner, tenant, program, ct).ConfigureAwait(false);
    }

    internal async ValueTask<TemplateApplicationReadSnapshot> ReadAsync(TemplateApplication owner, Uuid tenant,
        Uuid program, CancellationToken ct)
    {
        if (owner.Header is null)
            return new(owner, null);
        if (owner.Header.TenantId != tenant || owner.Header.ProgramId != program)
            return new(owner, null);
        var rules = new TemplateApplicationRules(tenant, program, owner.Id);
        long revision = 0;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString(),
            TemplateApplication.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            if (record.Event is not ITemplateApplicationFact fact || fact.TenantId != tenant ||
                fact.SubjectId != owner.Id || fact.Header.ProgramId != program || fact.Revision != revision + 1 ||
                fact.Content.Schema != Schema)
                throw new ImmutableContentIntegrityException("The application history has another exact source or revision.");
            var change = await content.ReadJsonAsync<TemplateApplicationMutationContent>(tenant.ToString(),
                fact.Content, ct).ConfigureAwait(false);
            if (change.Transition != Transition(record.Event))
                throw new ImmutableContentIntegrityException("The immutable application change has another business fact.");
            try
            {
                rules.ApplyChange(change, fact.Revision, fact.Header.ActorMemberId, fact.Header.ActorDisplay, fact.OccurredAt);
            }
            catch (InvalidOperationException)
            {
                throw new ImmutableContentIntegrityException("The application history violates its immutable source or transition.");
            }
            if (rules.Header() != fact.Header)
                throw new ImmutableContentIntegrityException("The application body differs from its exact native current authority.");
            revision = fact.Revision;
            if (revision == owner.Revision)
                break;
        }
        if (revision != owner.Revision || rules.Current is null)
            throw new ImmutableContentUnavailableException("The application source cut is not fully visible yet.");
        if (rules.Header() != owner.Header)
            throw new ImmutableContentIntegrityException("The application history differs from its captured authoritative cut.");
        return new(owner, rules.Current);
    }

    internal static string Transition(DomainEvent value) => value switch
    {
        MethodologyTemplateApplied => "apply",
        TemplateSuccessorAdoptionProposed => "successor_proposed",
        TemplateSuccessorDispositionRecorded => "successor_disposition",
        _ => throw new ImmutableContentIntegrityException("The application history has another native transition."),
    };
}
