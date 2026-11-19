using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bdgrz.Compliance;

static class ComplianceCoreJsonMetadata
{
    static readonly JsonSerializerContext[] Contexts =
    [
        ComplianceCoreJsonContext.Default,
        ComplianceCoreAdditionalJsonContext.Default,
    ];

    static readonly IJsonTypeInfoResolver Resolver = JsonTypeInfoResolver.Combine(Contexts);

    public static JsonSerializerOptions Options { get; } = CreateOptions();
    public static JsonSerializerContext Default { get; } = new CompositeContext(Options);

    static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(ComplianceCoreJsonContext.Default.Options)
        {
            TypeInfoResolver = Resolver,
        };
        return options;
    }

    sealed class CompositeContext : JsonSerializerContext
    {
        readonly JsonSerializerOptions _options;

        public CompositeContext(JsonSerializerOptions options) : base(options) => _options = options;

        protected override JsonSerializerOptions GeneratedSerializerOptions => _options;

        public override JsonTypeInfo? GetTypeInfo(Type type) => Resolver.GetTypeInfo(type, Options);
    }
}
