using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MegaBulkUploader.Modules.Misc;
using MegaBulkUploader.Modules.Output;

namespace MegaBulkUploader.Modules.Clients
{
    public static class MailClient
    {
        
        public static async Task<T?> GetAsync<T>(string baseUrl, string endPoint, string? auth = null)
        {
            using HttpClient httpClient = new();

            if (auth is not null)
            {
                httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {auth}");
            }

            HttpResponseMessage response = await httpClient.GetAsync($"{baseUrl}{endPoint}");

            return !response.IsSuccessStatusCode ? default : JsonSerializer.Deserialize<T>(response.Content.ReadAsStringAsync().Result);

        }

        public static async Task<T?> PostAsync<T>(string baseUrl, string endPoint, HttpContent? httpContent = null, string? auth = null)
        {
            using HttpClient httpClient = new();

            if (auth is not null)
            {
                httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {auth}");
            }

            HttpResponseMessage response = await httpClient.PostAsync($"{baseUrl}{endPoint}", httpContent);

            return !response.IsSuccessStatusCode ? default : JsonSerializer.Deserialize<T>(response.Content.ReadAsStringAsync().Result);

        }

        public static async Task<IReadOnlyList<(string, string)>> GetEmails(string token)
        {
            List<(string, string)> emails = [];

            Records.Emails? messages = await GetAsync<Records.Emails>("https://api.mail.tm", "/messages", token);
            if (messages is null)
                return emails.AsReadOnly();

            foreach (Records.EmailHydraMember message in messages.HydraMember)
            {
                Records.EmailData? email = await GetAsync<Records.EmailData>("https://api.mail.tm", $"/messages/{message.Id}", token);
                emails.Add((message.Subject, Regex.Unescape(string.Join('\n', email?.Html ?? []))));
            }

            return emails.AsReadOnly();
        }

        public static async Task<(string, string, string)> CreateEmail()
        {
            const string baseUrl = "https://api.mail.tm";
            Log logger = new ("Temp Email Creation");
            logger.LogInformation("Creating temporary email address. (https://mail.tm)");

            Records.Domains domains = GetAsync<Records.Domains>(baseUrl, "/domains").Result!;

            string usableDomain = domains.HydraMember[0].Domain;
            string email = ($"{Program.RandomString(12)}@{usableDomain}").ToLower();
            string password = $"@@{Program.RandomString(10)}!!";

            logger.LogInformation("Sending sign-up request...");
            Records.SignUp? signupReturn = await PostAsync<Records.SignUp>(baseUrl, "/accounts", new StringContent($"{{\"address\":\"{email}\",\"password\":\"{password}\"}}", Encoding.UTF8, "application/ld+json"));
            logger.LogInformation($"Sign-up Successful, email Id: {signupReturn?.Id}");

            logger.LogInformation("Sending token request and verification...");
            Records.TokenData? tokenReturn = await PostAsync<Records.TokenData>(baseUrl, "/token", new StringContent($"{{\"address\":\"{email}\",\"password\":\"{password}\"}}", Encoding.UTF8, "application/json"));
            GetAsync<dynamic>(baseUrl, "/me", tokenReturn?.Token).Wait();

            logger.LogInformation($"Email created: {email} {password}");
            return (email, password, tokenReturn?.Token ?? throw new Exception("Failed to get email token"));
        }
    }
}
