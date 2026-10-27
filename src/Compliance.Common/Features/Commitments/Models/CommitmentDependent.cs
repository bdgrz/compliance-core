using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Commitments.Models;

public sealed record CommitmentDependent(string Context, string RecordType, Uuid RecordId,
    string Relationship);
