using System.Text.Json;
using Staybnb.ViewModels;

namespace Staybnb.Services;

/// <summary>Reads and writes the check-in rules stored as JSON in CheckInProcess.StepsJson.</summary>
public static class CheckInStepsSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Used until a host configures their own rules.</summary>
    public static readonly IReadOnlyList<CheckInStepDto> DefaultSteps = new List<CheckInStepDto>
    {
        new() { Title = "Respect the property", Description = "Keep noise down between 22:00 and 07:00 and treat the home with care." },
        new() { Title = "Guests and visitors", Description = "Only the guests on your booking may stay overnight. No parties or events." },
        new() { Title = "Check-in and check-out times", Description = "Check in from 14:00 and check out by 10:00." },
        new() { Title = "Identity verification", Description = "Upload a valid ID or passport so the host can verify you before arrival." }
    };

    public static List<CheckInStepDto> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();

        try
        {
            return JsonSerializer.Deserialize<List<CheckInStepDto>>(json, Options) ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public static string Serialize(IEnumerable<CheckInStepDto> steps) => JsonSerializer.Serialize(steps, Options);

    public static string SerializeDefault() => Serialize(DefaultSteps);
}