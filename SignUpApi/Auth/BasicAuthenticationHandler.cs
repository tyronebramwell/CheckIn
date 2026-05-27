using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SignUpApi.Data;
using BCrypt.Net;

namespace SignUpApi.Auth;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AppDbContext _context;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AppDbContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.Fail("Missing Authorization Header");

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]!);
            
            // Handle QR Authentication (Internal only for this app)
            if (authHeader.Scheme == "QR")
            {
                var memberId = Guid.Parse(authHeader.Parameter!);
                var member = await _context.Members.FindAsync(memberId);
                if (member == null) return AuthenticateResult.Fail("Invalid QR Token");

                var qrClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, member.MemberId.ToString()),
                    new Claim(ClaimTypes.Name, member.Username),
                    new Claim("UserType", "Member")
                };
                return Success(qrClaims);
            }

            // Handle standard Basic Auth
            var credentialBytes = Convert.FromBase64String(authHeader.Parameter!);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            var username = credentials[0];
            var password = credentials[1];

            // Check Volunteers first
            var volunteer = await _context.Volunteers.SingleOrDefaultAsync(v => v.Username.ToLower() == username.ToLower());

            if (volunteer != null && BCrypt.Net.BCrypt.Verify(password, volunteer.PasswordHash))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, volunteer.VolunteerId.ToString()),
                    new Claim(ClaimTypes.Name, volunteer.Username),
                    new Claim("UserType", "Volunteer"),
                    new Claim("CanViewData", volunteer.CanViewData.ToString().ToLower()),
                    new Claim("CanAddUsers", volunteer.CanAddUsers.ToString().ToLower()),
                    new Claim("CanManageVolunteers", volunteer.CanManageVolunteers.ToString().ToLower())
                };

                return Success(claims);
            }

            // Check Members
            var memberCheck = await _context.Members.SingleOrDefaultAsync(m => m.Username != null && m.Username.ToLower() == username.ToLower());
            if (memberCheck != null && memberCheck.PasswordHash != null && BCrypt.Net.BCrypt.Verify(password, memberCheck.PasswordHash))
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, memberCheck.MemberId.ToString()),
                    new Claim(ClaimTypes.Name, memberCheck.Username!),
                    new Claim("UserType", "Member")
                };

                return Success(claims);
            }

            return AuthenticateResult.Fail("Invalid Username or Password");
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid Authorization Header");
        }
    }

    private AuthenticateResult Success(List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
