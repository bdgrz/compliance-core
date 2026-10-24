using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bdgrz.Compliance.Features.Kernel;

/// <summary>Writes decimals in invariant G29 form so values that differ only in scale encode identically.</summary>
public sealed class G29DecimalJsonConverter : JsonConverter<decimal>
{
    /// <inheritdoc />
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString("G29", CultureInfo.InvariantCulture));
}
