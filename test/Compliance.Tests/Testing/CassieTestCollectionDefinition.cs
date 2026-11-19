namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Serializes tests that mutate Cassie's shared server catalog.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CassieTestCollectionDefinition
{
    public const string Name = "Cassie test server";
}
