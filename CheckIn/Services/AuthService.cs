using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using CheckIn.Shared.Models;

namespace CheckIn.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private AuthenticationHeaderValue? _authHeaderValue;

        public string? UserType { get; private set; }
        public bool IsLoggedIn => _authHeaderValue != null;
        public bool IsVolunteer => UserType == "Volunteer";
        public bool IsMember => UserType == "Member";
        public bool CanViewData { get; private set; }
        public bool CanAddUsers { get; private set; }
        public bool CanManageVolunteers { get; private set; }
        public bool CanManageEvents { get; private set; }
        public bool MustChangePassword { get; private set; }
        public User? CurrentUser { get; private set; } // Added this property

        public AuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { username, password });
            if (!response.IsSuccessStatusCode)
            {
                Logout();
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<LoginResult>();
            if (result == null || string.IsNullOrEmpty(result.Token))
            {
                Logout();
                return false;
            }

            _authHeaderValue = new AuthenticationHeaderValue("Basic", result.Token);
            _httpClient.DefaultRequestHeaders.Authorization = _authHeaderValue;

            var claims = ParseClaimsFromJwt(result.Token);
            UserType = claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            CanViewData = claims.Any(c => c.Type == "CanViewData" && c.Value == "true");
            CanAddUsers = claims.Any(c => c.Type == "CanAddUsers" && c.Value == "true");
            CanManageVolunteers = claims.Any(c => c.Type == "CanManageVolunteers" && c.Value == "true");
            CanManageEvents = claims.Any(c => c.Type == "CanManageEvents" && c.Value == "true");
            MustChangePassword = claims.Any(c => c.Type == "MustChangePassword" && c.Value == "true");

            CurrentUser = new User
            {
                Username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "",
                // Populate other user properties if available in the token
            };

            return true;
        }

        public void Logout()
        {
            _authHeaderValue = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
            UserType = null;
            CanViewData = false;
            CanAddUsers = false;
            CanManageVolunteers = false;
            CanManageEvents = false;
            MustChangePassword = false;
            CurrentUser = null;
        }

        private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs != null)
            {
                keyValuePairs.TryGetValue(ClaimTypes.Role, out var roles);
                if (roles != null)
                {
                    if (roles.ToString()!.Trim().StartsWith("["))
                    {
                        var parsedRoles = System.Text.Json.JsonSerializer.Deserialize<string[]>(roles.ToString()!);
                        claims.AddRange(parsedRoles!.Select(r => new Claim(ClaimTypes.Role, r)));
                    }
                    else
                    {
                        claims.Add(new Claim(ClaimTypes.Role, roles.ToString()!));
                    }
                }

                // Add other claims
                claims.Add(new Claim(ClaimTypes.Name, keyValuePairs["unique_name"]?.ToString() ?? ""));
                if (keyValuePairs.ContainsKey("CanViewData")) claims.Add(new Claim("CanViewData", keyValuePairs["CanViewData"].ToString()!.ToLower()));
                if (keyValuePairs.ContainsKey("CanAddUsers")) claims.Add(new Claim("CanAddUsers", keyValuePairs["CanAddUsers"].ToString()!.ToLower()));
                if (keyValuePairs.ContainsKey("CanManageVolunteers")) claims.Add(new Claim("CanManageVolunteers", keyValuePairs["CanManageVolunteers"].ToString()!.ToLower()));
                if (keyValuePairs.ContainsKey("CanManageEvents")) claims.Add(new Claim("CanManageEvents", keyValuePairs["CanManageEvents"].ToString()!.ToLower()));
                if (keyValuePairs.ContainsKey("MustChangePassword")) claims.Add(new Claim("MustChangePassword", keyValuePairs["MustChangePassword"].ToString()!.ToLower()));
            }
            return claims;
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }

    public class LoginResult
    {
        public string Token { get; set; } = "";
    }
}
