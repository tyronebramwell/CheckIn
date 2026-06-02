using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using CheckInApi.Auth;
using CheckInApi.Data;
using CheckInApi.Services;
using CheckInCommon.Models;
using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using CheckInApi.Endpoints;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Set global culture to UK
var culture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Fix for PostgreSQL DateTime issues
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CsvService>();
builder.Services.AddScoped<LogService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<CsvSyncWorker>();

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanViewData", policy => policy.RequireClaim("CanViewData", "true"));
    options.AddPolicy("CanAddUsers", policy => policy.RequireClaim("CanAddUsers", "true"));
    options.AddPolicy("CanManageVolunteers", policy => policy.RequireClaim("CanManageVolunteers", "true"));
    options.AddPolicy("CanManageEvents", policy => policy.RequireClaim("CanManageEvents", "true"));
    options.AddPolicy("IsMember", policy => policy.RequireClaim("UserType", "Member"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Charity Event API", Version = "v1" });
    
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Basic Authorization header."
    };
    
    c.AddSecurityDefinition("basic", securityScheme);
    
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("basic", doc),
            new List<string>()
        }
    });

});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Charity Event API V1");
    c.RoutePrefix = string.Empty;
});

app.UseCors("AllowAllOrigins");

app.UseAuthentication();
app.UseAuthorization();

// Register domain-specific endpoints
app.MapAuthEndpoints();
app.MapConfigEndpoints();
app.MapMemberEndpoints();
app.MapEventEndpoints();
app.MapVolunteerEndpoints();
app.MapAttendanceEndpoints();
app.MapLogEndpoints();

// Ensure database is created and seed initial volunteer if needed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    
    if (!db.Volunteers.Any())
    {
        db.Volunteers.Add(new Volunteer
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            CanViewData = true,
            CanAddUsers = true,
            CanManageVolunteers = true
        });
        db.SaveChanges();
    }
}

app.Run();
