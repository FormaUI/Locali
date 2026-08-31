namespace Forma.Locali.Tests.Support.Reporting;

using System.Text.Json;

using Forma.Locali.Reporting;

/// <summary>Reads back a report the linter wrote.</summary>
public static class LintReportTestReader
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The report the JSON carries.</summary>
    /// <param name="json">The report as it was written.</param>
    public static LintReport Read(string json)
    {
        ArgumentException.ThrowIfNullOrEmpty(json);

        return JsonSerializer.Deserialize<LintReport>(json, ReadOptions)
            ?? throw new InvalidOperationException("The report carries no content.");
    }
}
