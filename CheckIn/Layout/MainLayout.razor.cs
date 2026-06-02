using System;
using CheckIn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace CheckIn.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private bool _drawerOpen = false;

    private bool ShouldHideAppBar
    {
        get
        {
            try
            {
                var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri).ToLower();
                var isRoot = string.IsNullOrEmpty(relativePath) || relativePath == "/" || relativePath.StartsWith("/?");
                var isLogin = relativePath.Contains("login");
                
                // Hide AppBar if unauthenticated or if a Member is on the home/login pages
                if (!AuthService.IsLoggedIn) return isRoot || isLogin;
                if (AuthService.IsMember) return isRoot || isLogin;
            }
            catch { /* Fallback to showing AppBar if path parsing fails */ }
            
            return false;
        }
    }

    protected override void OnInitialized()
    {
        AuthService.OnAuthStateChanged += OnAuthStateChanged;
        NavigationManager.LocationChanged += HandleLocationChanged;
    }

    private void OnAuthStateChanged()
    {
        if (ShouldHideAppBar)
        {
            _drawerOpen = false;
        }
        StateHasChanged();
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (ShouldHideAppBar)
        {
            _drawerOpen = false;
        }
        StateHasChanged();
    }

    public void Dispose()
    {
        AuthService.OnAuthStateChanged -= OnAuthStateChanged;
        NavigationManager.LocationChanged -= HandleLocationChanged;
    }

    private void DrawerToggle()
    {
        _drawerOpen = !_drawerOpen;
    }

    private void Login()
    {
        NavigationManager.NavigateTo("/login");
    }

    private void Logout()
    {
        AuthService.Logout();
        NavigationManager.NavigateTo("/");
    }
}
