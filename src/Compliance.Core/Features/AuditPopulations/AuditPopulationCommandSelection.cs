using Bdgrz.Compliance.Features.AuditPopulations.Commands;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.Kernel;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

sealed record AuditPopulationCommandSelection(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid PopulationId,
    long? ExpectedRevision, string Kind, string Digest, AuditPopulationCommandContent Content)
{
    public static IRequest Snapshot(IRequest request) => request switch
    {
        ReviseAuditPopulationDefinition value => Copy(value),
        RecordAuditPopulationFacts value => Copy(value),
        ClassifyAuditPopulationRow value => Copy(value),
        ReconcileAuditPopulation value => Copy(value),
        FreezeAuditPopulation value => Copy(value),
        AmendAuditPopulation value => Copy(value),
        _ => throw new InvalidOperationException("This request has another exact population decision kind.")
    };
    static T Copy<T>(T value) where T : IRequest
    {
        var info = ComplianceCoreJsonContext.Default.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException("The population command has no actual serializer contract.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, info);
        return (T)(JsonSerializer.Deserialize(bytes, info)
            ?? throw new InvalidOperationException("The population command has no complete authored value."));
    }
    public static AuditPopulationCommandSelection Of(IRequest request) => request switch
    {
        ReviseAuditPopulationDefinition value => new(value.CommandId, value.TenantId, value.ProgramId, value.DefinitionId,
            value.ExpectedRevision, nameof(ReviseAuditPopulationDefinition), AuthoredIntentDigest.Compute(value), new(value, null, null, null, null, null)),
        RecordAuditPopulationFacts value => new(value.CommandId, value.TenantId, value.ProgramId, value.DefinitionId,
            value.ExpectedRevision, nameof(RecordAuditPopulationFacts), AuthoredIntentDigest.Compute(value), new(null, value, null, null, null, null)),
        ClassifyAuditPopulationRow value => new(value.CommandId, value.TenantId, value.ProgramId, value.PopulationId,
            null, nameof(ClassifyAuditPopulationRow), AuthoredIntentDigest.Compute(value), new(null, null, value, null, null, null)),
        ReconcileAuditPopulation value => new(value.CommandId, value.TenantId, value.ProgramId, value.PopulationId,
            value.ExpectedRevision, nameof(ReconcileAuditPopulation), AuthoredIntentDigest.Compute(value), new(null, null, null, value, null, null)),
        FreezeAuditPopulation value => new(value.CommandId, value.TenantId, value.ProgramId, value.PopulationId,
            value.ExpectedRevision, nameof(FreezeAuditPopulation), AuthoredIntentDigest.Compute(value), new(null, null, null, null, value, null)),
        AmendAuditPopulation value => new(value.CommandId, value.TenantId, value.ProgramId, value.PopulationId,
            value.ExpectedRevision, nameof(AmendAuditPopulation), AuthoredIntentDigest.Compute(value), new(null, null, null, null, null, value)),
        _ => throw new InvalidOperationException("This request has another exact population decision kind.")
    };
}
