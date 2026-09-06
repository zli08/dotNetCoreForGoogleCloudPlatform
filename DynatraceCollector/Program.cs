using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

// ------------------------------------------------------------
// Configuration – use environment variables in production
// ------------------------------------------------------------
string tenantUrl = "https://YOUR_TENANT.live.dynatrace.com";   // e.g., https://abc12345.live.dynatrace.com
string apiToken = "YOUR_API_TOKEN";                            // Better: Environment.GetEnvironmentVariable("DT_API_TOKEN")

// ------------------------------------------------------------
// HttpClient setup
// ------------------------------------------------------------
using var httpClient = new HttpClient
{
    BaseAddress = new Uri(tenantUrl)
};

// Add API token header (Dynatrace API v2 uses "Authorization: Api-Token <token>")
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Api-Token", apiToken);
httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

// ------------------------------------------------------------
// 1. Fetch open problems (example endpoint)
// ------------------------------------------------------------
Console.WriteLine("Fetching open problems...");
try
{
    // GET /api/v2/problems?from=now-24h&pageSize=10
    var problemsResponse = await httpClient.GetAsync("/api/v2/problems?from=now-24h&pageSize=10");
    problemsResponse.EnsureSuccessStatusCode();

    var problemsJson = await problemsResponse.Content.ReadAsStringAsync();
    var problems = JsonSerializer.Deserialize<ProblemsResponse>(problemsJson, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    Console.WriteLine($"Total open problems: {problems?.TotalCount}");
    if (problems?.Problems != null)
    {
        foreach (var problem in problems.Problems)
        {
            Console.WriteLine($"- [{problem.Status}] {problem.ProblemId}: {problem.Title} (Impact: {problem.ImpactLevel})");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error fetching problems: {ex.Message}");
}

// ------------------------------------------------------------
// 2. Fetch a metric (example: built-in CPU usage for a host)
// ------------------------------------------------------------
Console.WriteLine("\nFetching CPU metric...");
try
{
    // GET /api/v2/metrics/query?metricSelector=builtin:host.cpu.usage&from=now-1h&resolution=1h
    var metricResponse = await httpClient.GetAsync("/api/v2/metrics/query?metricSelector=builtin:host.cpu.usage&from=now-1h&resolution=1h");
    metricResponse.EnsureSuccessStatusCode();

    var metricJson = await metricResponse.Content.ReadAsStringAsync();
    var metricData = JsonSerializer.Deserialize<MetricQueryResponse>(metricJson, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    Console.WriteLine("Metric results:");
    if (metricData?.Result != null)
    {
        foreach (var result in metricData.Result)
        {
            Console.WriteLine($"Metric ID: {result.MetricId}");
            foreach (var dataPoint in result.Data ?? new List<DataPoint>())
            {
                Console.WriteLine($"  Time: {dataPoint.Timeframe}, Value: {dataPoint.Values?.FirstOrDefault()}");
            }
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error fetching metrics: {ex.Message}");
}

// ------------------------------------------------------------
// Data models for Dynatrace API v2 responses
// ------------------------------------------------------------

// Problems response
public class ProblemsResponse
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("problems")]
    public List<Problem>? Problems { get; set; }
}

public class Problem
{
    [JsonPropertyName("problemId")]
    public string? ProblemId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("impactLevel")]
    public string? ImpactLevel { get; set; }
}

// Metrics query response (simplified)
public class MetricQueryResponse
{
    [JsonPropertyName("result")]
    public List<MetricResult>? Result { get; set; }
}

public class MetricResult
{
    [JsonPropertyName("metricId")]
    public string? MetricId { get; set; }

    [JsonPropertyName("data")]
    public List<DataPoint>? Data { get; set; }
}

public class DataPoint
{
    [JsonPropertyName("timeframe")]
    public string? Timeframe { get; set; }

    [JsonPropertyName("values")]
    public List<double>? Values { get; set; }
}