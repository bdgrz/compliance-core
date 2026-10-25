using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

public static class IndependenceCatalogHistory
{
    public static bool ProvesAbsent(ImmutableContentRef? head, long sequence,
        IReadOnlyList<IndependenceRatificationHistoryRead> history, Uuid ratificationId)
    {
        var reference = head;
        var expected = sequence;
        long? version = null;
        foreach (var entry in history)
        {
            var value = entry.Content;
            if (reference is null || reference != entry.Reference || !IndependenceCatalogContent.ValidRatification(value) || value.Sequence != expected ||
                version is { } later && value.Ratification.RuleVersion >= later || !IndependenceCatalogContent.Matches(reference, value, IndependenceCatalogContent.RatificationSchema))
                throw new ImmutableContentIntegrityException("The ratification history proof differs from its exact monotonic native cut.");
            if (value.Ratification.RatificationId == ratificationId)
                return false;
            reference = value.Previous;
            expected--;
            version = value.Ratification.RuleVersion;
        }
        if (reference is not null || expected != 0)
            throw new ImmutableContentIntegrityException("The historical ratification identity absence has not been proven.");
        return true;
    }
}
