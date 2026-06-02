using System;
using CheckIn.Services;
using Microsoft.AspNetCore.Components;

namespace CheckIn.Layout;

public partial class NavMenu : ComponentBase, IDisposable
{
    [Inject] private AuthService AuthService { get; set; } = default!;

    protected override void OnInitialized()
    {
        AuthService.OnAuthStateChanged += StateHasChanged;
    }

    public void Dispose()
    {
        AuthService.OnAuthStateChanged -= StateHasChanged;
    }
}
