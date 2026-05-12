using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var tokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeTrack", "tokens.dat");
if (!File.Exists(tokenPath)) { Console.WriteLine("No tokens.dat!"); return; }

var encData = File.ReadAllBytes(tokenPath);
var decData = ProtectedData.Unprotect(encData, null, DataProtectionScope.CurrentUser);
var json = Encoding.UTF8.GetString(decData);
var doc = JsonDocument.Parse(json);
var jwt = doc.RootElement.GetProperty("Jwt").GetString()!;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

// Test GET /evidence?startDate=2026-04-24&endDate=2026-04-25
var req = new HttpRequestMessage(HttpMethod.Get, "http://localhost:5000/api/v1/evidence?startDate=2026-04-24T00:00:00Z&endDate=2026-04-25T00:00:00Z&page=1&pageSize=10");
req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

var resp = await http.SendAsync(req);
var body = await resp.Content.ReadAsStringAsync();
Console.WriteLine($"List status: {resp.StatusCode}");

// Pretty print
var pretty = JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(body), new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(pretty[..Math.Min(2000, pretty.Length)]);

// If we have items, test the download-url endpoint
var listDoc = JsonDocument.Parse(body);
var items = listDoc.RootElement.GetProperty("items");
if (items.GetArrayLength() > 0)
{
    var firstId = items[0].GetProperty("id").GetString();
    Console.WriteLine($"\n--- Testing download-url for {firstId} ---");
    var dlReq = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:5000/api/v1/evidence/{firstId}/download-url");
    dlReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
    var dlResp = await http.SendAsync(dlReq);
    var dlBody = await dlResp.Content.ReadAsStringAsync();
    Console.WriteLine($"Download status: {dlResp.StatusCode}");
    Console.WriteLine(dlBody[..Math.Min(1000, dlBody.Length)]);
}
else
{
    Console.WriteLine("No evidence items found!");
}
