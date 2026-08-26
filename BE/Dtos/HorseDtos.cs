using System;
using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HorseRacing.Dtos;

public class HorseCreateRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Breed { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int Age { get; set; }

    [JsonConverter(typeof(HorseMeasurementDecimalConverter))]
    public decimal? Weight { get; set; }

    [JsonConverter(typeof(HorseMeasurementDecimalConverter))]
    public decimal? Height { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    public int TotalRaces { get; set; } = 0;

    public int TotalWins { get; set; } = 0;

    [MaxLength(2000)]
    public string? ImageUrl { get; set; }
}

public class HorseUpdateRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Breed { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int Age { get; set; }

    [JsonConverter(typeof(HorseMeasurementDecimalConverter))]
    public decimal? Weight { get; set; }

    [JsonConverter(typeof(HorseMeasurementDecimalConverter))]
    public decimal? Height { get; set; }

    [MaxLength(50)]
    public string? Color { get; set; }

    public int TotalRaces { get; set; }

    public int TotalWins { get; set; }

    [MaxLength(2000)]
    public string? ImageUrl { get; set; }
}

public class JockeyInvitationCreateRequest
{
    [Required]
    public Guid JockeyId { get; set; }
    [Required]
    public Guid RaceId { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }
}

public class JockeyRemovalRequest
{
    // J2 follow-up: multiple Pending/Accepted invitations can now exist for the same
    // Horse+Race, so the exact invitation being cancelled must be identified explicitly.
    [Required]
    public Guid InvitationId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class RaceRegistrationRequest
{
    public bool OwnerConfirmed { get; set; } = false;
}

// J3: Owner picks exactly one Accepted invitation as the official Jockey for a RaceEntry.
public class OwnerFinalConfirmJockeyRequest
{
    [Required]
    public Guid InvitationId { get; set; }
}

public sealed class HorseMeasurementDecimalConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var raw = reader.TokenType switch
        {
            JsonTokenType.Number => ReadRawNumber(ref reader),
            JsonTokenType.String => reader.GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(raw) || raw.Any(c => c < '0' || c > '9'))
        {
            throw new JsonException("Chỉ được nhập chữ số.");
        }

        return decimal.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new JsonException("Giá trị đo không hợp lệ.");
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
            return;
        }

        writer.WriteNullValue();
    }

    private static string ReadRawNumber(ref Utf8JsonReader reader)
    {
        if (!reader.HasValueSequence)
        {
            return Encoding.UTF8.GetString(reader.ValueSpan);
        }

        return Encoding.UTF8.GetString(reader.ValueSequence.ToArray());
    }
}
