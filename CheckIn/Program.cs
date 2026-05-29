using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CheckIn;
using MudBlazor.Services;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var culture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

builder.Services.AddScoped(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiBaseUrl = config["ApiBaseUrl"];
    
    // Check if the URL is empty or the placeholder hasn't been replaced
    var baseAddress = (string.IsNullOrEmpty(apiBaseUrl) || apiBaseUrl == "API_BASE_URL_PLACEHOLDER") 
        ? builder.HostEnvironment.BaseAddress 
        : apiBaseUrl;
    
    return new HttpClient
    {
        BaseAddress = new Uri(baseAddress)
    };
});
builder.Services.AddScoped<CheckIn.Services.AuthService>();
builder.Services.AddMudServices();

await builder.Build().RunAsync();