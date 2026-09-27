using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Anorath.WinUI.Services
{
    // Who is logged in (shared by all pages). Filled from the Master login response.
    public static class Session
    {
        public static string Token { get; set; } = "";
        public static int UserId { get; set; }
        public static string Username { get; set; } = "";
        public static string FullName { get; set; } = "";
        public static string Role { get; set; } = "";
        public static string CompanyCode { get; set; } = "";   // empty for Super Admin
        public static string CompanyName { get; set; } = "";
        public static string Plan { get; set; } = "";          // Micro / Small / Medium / Master

        public static bool IsSuperAdmin => Role == "SuperAdmin";

        public static void Clear()
        {
            UserId = 0;
            Username = FullName = Role = CompanyCode = CompanyName = Plan = Token = "";
        }
    }

    public class ApiException : Exception
    {
        public ApiException(string message) : base(message) { }
    }

    // One place that talks to Anorath.api and adds the X-Company-Code header
    public static class ApiClient
    {
        private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("http://localhost:5166/") };
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private static HttpRequestMessage Build(HttpMethod method, string url, object? body)
        {
            var request = new HttpRequestMessage(method, url);

            // Signed login token: the server reads the user, role and company from it
            if (!string.IsNullOrWhiteSpace(Session.Token))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Session.Token);

            if (body != null)
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            return request;
        }

        public static async Task<T> GetAsync<T>(string url)
        {
            using var response = await Http.SendAsync(Build(HttpMethod.Get, url, null));
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new ApiException(ErrorText(response, text));
            return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? throw new ApiException("Empty response from server.");
        }

        public static async Task<T> PostAsync<T>(string url, object body)
        {
            using var response = await Http.SendAsync(Build(HttpMethod.Post, url, body));
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new ApiException(ErrorText(response, text));
            return JsonSerializer.Deserialize<T>(text, JsonOptions) ?? throw new ApiException("Empty response from server.");
        }

        public static Task PostAsync(string url, object body) => SendAsync(HttpMethod.Post, url, body);
        public static Task PutAsync(string url, object body) => SendAsync(HttpMethod.Put, url, body);
        public static Task DeleteAsync(string url) => SendAsync(HttpMethod.Delete, url, null);

        private static async Task SendAsync(HttpMethod method, string url, object? body)
        {
            using var response = await Http.SendAsync(Build(method, url, body));
            if (!response.IsSuccessStatusCode)
                throw new ApiException(ErrorText(response, await response.Content.ReadAsStringAsync()));
        }

        private static string ErrorText(HttpResponseMessage response, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return $"Server returned {(int)response.StatusCode} ({response.ReasonPhrase}).";

            var trimmed = text.Trim();
            if (trimmed.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = new List<string>();
                        foreach (var prop in errors.EnumerateObject())
                            foreach (var msg in prop.Value.EnumerateArray())
                                messages.Add(msg.GetString() ?? "");
                        if (messages.Count > 0) return string.Join(" ", messages);
                    }
                    if (doc.RootElement.TryGetProperty("title", out var title))
                        return title.GetString() ?? trimmed;
                }
                catch (JsonException) { }
            }
            return trimmed.Trim('"');
        }
    }
}