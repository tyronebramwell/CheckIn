using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace CheckIn.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private string? _authHeaderValue;

    public AuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string? Username { get; private set; }
    public string? UserType { get; private set; }
    public bool IsLoggedIn => _authHeaderValue != null;
    public bool IsVolunteer => UserType == "Volunteer";
    public bool IsMember => UserType == "Member";
    public bool CanViewData { get; private set; }
    public bool CanAddUsers { get; private set; }
    public bool CanManageVolunteers { get; private set; }
    public bool CanManageEvents { get; private set; }
    public bool MustChangePassword { get; private set; }

    public event Action? OnAuthStateChanged;

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (result != null)
                {
                    _authHeaderValue = authValue;
                    Username = username;
                    UserType = result.UserType;
                    CanViewData = result.CanViewData;
                    CanAddUsers = result.CanAddUsers;
                    CanManageVolunteers = result.CanManageVolunteers;
                    CanManageEvents = result.CanManageEvents;
                    MustChangePassword = result.MustChangePassword;
                    
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _authHeaderValue);
                    OnAuthStateChanged?.Invoke();
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Usually 'Failed to fetch' due to untrusted SSL cert on port 5001
            return false;
        }

        return false;
    }

    public async Task<bool> ForgotPasswordAsync(string email)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/forgot-password", new { email });
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ChangePasswordResetAsync(string newPassword)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/change-password-reset", new { newPassword });
            if (response.IsSuccessStatusCode)
            {
                MustChangePassword = false;
                OnAuthStateChanged?.Invoke();
                return true;
            }
        }
        catch { }
        return false;
    }

    public async Task<bool> QrLoginAsync(string username, Guid qrSecret)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/qr-login", new { username, qrSecret });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QrLoginResponse>();
                if (result != null)
                {
                    Username = result.Username;
                    UserType = result.UserType;
                    MustChangePassword = false;
                    
                    _authHeaderValue = "QR_AUTH";
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("QR", result.MemberId.ToString());
                    
                    OnAuthStateChanged?.Invoke();
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    public void Logout()
    {
        _authHeaderValue = null;
        Username = null;
        UserType = null;
        CanViewData = false;
        CanAddUsers = false;
        CanManageVolunteers = false;
        CanManageEvents = false;
        MustChangePassword = false;
        _httpClient.DefaultRequestHeaders.Authorization = null;
        OnAuthStateChanged?.Invoke();
    }

    private record LoginResponse(string UserType, bool CanViewData, bool CanAddUsers, bool CanManageVolunteers, bool CanManageEvents, bool MustChangePassword);
    private record QrLoginResponse(string UserType, string Username, Guid MemberId);
}
