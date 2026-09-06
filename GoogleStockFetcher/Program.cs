using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

// Calculate Unix timestamps
var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
var endDate = DateTimeOffset.UtcNow;
long startUnix = startDate.ToUnixTimeSeconds();
long endUnix = endDate.ToUnixTimeSeconds();

// Build Yahoo Finance chart API URL (daily interval)
string url = $"https://query1.finance.yahoo.com/v8/finance/chart/GOOGL?period1={startUnix}&period2={endUnix}&interval=1d";

using var client = new HttpClient();
// Yahoo requires a User-Agent header
client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

try
{
    HttpResponseMessage response = await client.GetAsync(url);
    response.EnsureSuccessStatusCode();
    string json = await response.Content.ReadAsStringAsync();

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var chartData = JsonSerializer.Deserialize<YahooChartResponse>(json, options);

    if (chartData?.Chart?.Result == null || chartData.Chart.Result.Length == 0)
    {
        Console.WriteLine("No data found.");
        return;
    }

    var result = chartData.Chart.Result[0];
    var timestamps = result.Timestamp;
    var quote = result.Indicators?.Quote?[0];

    if (timestamps == null || quote == null)
    {
        Console.WriteLine("Incomplete data received.");
        return;
    }

    Console.WriteLine("Date       | Open     | High     | Low      | Close    | Volume");
    Console.WriteLine("-----------+----------+----------+----------+----------+------------");

    for (int i = 0; i < timestamps.Length; i++)
    {
        // Convert Unix timestamp to date (UTC)
        string date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime.ToString("yyyy-MM-dd");

        // Safely access nullable arrays
        double? open = quote.Open?[i];
        double? high = quote.High?[i];
        double? low = quote.Low?[i];
        double? close = quote.Close?[i];
        long? volume = quote.Volume?[i];

        Console.WriteLine($"{date} | {open,8:F2} | {high,8:F2} | {low,8:F2} | {close,8:F2} | {volume,10}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// ----- JSON response classes -----
public class YahooChartResponse
{
    [JsonPropertyName("chart")]
    public Chart? Chart { get; set; }
}

public class Chart
{
    [JsonPropertyName("result")]
    public Result[]? Result { get; set; }
}

public class Result
{
    [JsonPropertyName("timestamp")]
    public long[]? Timestamp { get; set; }

    [JsonPropertyName("indicators")]
    public Indicators? Indicators { get; set; }
}

public class Indicators
{
    [JsonPropertyName("quote")]
    public Quote[]? Quote { get; set; }
}

public class Quote
{
    [JsonPropertyName("open")]
    public double?[]? Open { get; set; }

    [JsonPropertyName("high")]
    public double?[]? High { get; set; }

    [JsonPropertyName("low")]
    public double?[]? Low { get; set; }

    [JsonPropertyName("close")]
    public double?[]? Close { get; set; }

    [JsonPropertyName("volume")]
    public long?[]? Volume { get; set; }
}