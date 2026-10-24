using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Reconstructs verified complete period views from immutable bodies; command hydration retains bounded authority.</summary>
public sealed class TypeIIPeriodReader(IAggregateReader reader, IDomainEventReader events, IImmutableContentStore content)
{
    public async ValueTask<Result<TypeIIPeriodReadSnapshot>> TryReadAsync(Uuid tenantId, Uuid programId, Uuid periodId,
        CancellationToken ct = default)
    {
        try
        {
            return Result<TypeIIPeriodReadSnapshot>.Success(await ReadAsync(tenantId, programId, periodId, ct).ConfigureAwait(false));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result<TypeIIPeriodReadSnapshot>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The period's immutable source body cannot be verified.", exception is ImmutableContentUnavailableException));
        }
    }

    public async ValueTask<TypeIIPeriodReadSnapshot> ReadAsync(Uuid tenantId, Uuid programId, Uuid periodId,
        CancellationToken ct = default)
    {
        var owner = await reader.HydrateAsync(new TypeIIPeriod(tenantId, programId, periodId), ct).ConfigureAwait(false);
        return owner.ProgramId == programId ? await ReadAsync(owner, ct).ConfigureAwait(false) :
            new TypeIIPeriodReadSnapshot(owner, new TypeIIPeriodRules(tenantId, programId, periodId), []);
    }

    public async ValueTask<TypeIIPeriodReadSnapshot> ReadAsync(TypeIIPeriod owner, CancellationToken ct = default)
    {
        var rules = new TypeIIPeriodRules(owner.TenantId, owner.ProgramId, owner.Id);
        var facts = new List<DomainEvent>();
        if (owner.NativeSequence == 0)
            return new TypeIIPeriodReadSnapshot(owner, rules, facts.AsReadOnly());
        var sequence = 0L;
        ITypeIIPeriodStateFact? latest = null;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(owner.Stream.Realm,
            TypeIIPeriod.Area, owner.Id.ToString()), EventCursor.Start, ct).ConfigureAwait(false))
        {
            if (record.Event.Metadata.IsAudit)
                continue;
            var value = record.Event;
            if (value is ITypeIIPeriodStateFact fact)
            {
                if (fact.NativeSequence != sequence + 1 || fact.CurrentContent.Schema != TypeIIPeriodContent.StateSchema)
                    throw new ImmutableContentIntegrityException("The period's immutable source has another exact native sequence.");
                var state = await content.ReadJsonAsync<TypeIIPeriodView>(owner.Stream.Realm, fact.CurrentContent, ct).ConfigureAwait(false);
                if (TypeIIPeriodContent.Header(state) != fact.Header || ContentDigest.OfJson(state) != fact.CurrentContent.Sha256)
                    throw new ImmutableContentIntegrityException("The period's immutable body differs from its compact native authority.");
                await TypeIIPeriodFactContent.ApplyAsync(content, rules, value, state, ct).ConfigureAwait(false);
                if (rules.Current is not { } folded || ContentDigest.OfJson(folded) != fact.CurrentContent.Sha256)
                    throw new ImmutableContentIntegrityException("The period's immutable body does not follow its accepted local transition.");
                latest = fact;
            }
            else if (value is TypeIIPeriodOpeningAccepted or TypeIIPeriodAmendmentAccepted or
                TypeIIPeriodOpeningRejected or TypeIIPeriodAmendmentCanceled)
                rules.Apply(value);
            else
                throw new ImmutableContentIntegrityException("The period source has another native fact kind.");
            facts.Add(value);
            sequence++;
            if (sequence == owner.NativeSequence)
                break;
        }
        if (sequence != owner.NativeSequence)
            throw new ImmutableContentUnavailableException("The exact period source cut is not visible yet.");
        if (latest?.Header != owner.Header || latest?.CurrentContent != owner.CurrentContent)
            throw new ImmutableContentIntegrityException("The composed period differs from the hydrated owner's exact native authority.");
        rules.ClearPendingChanges();
        return new TypeIIPeriodReadSnapshot(owner, rules, facts.AsReadOnly());
    }
}
