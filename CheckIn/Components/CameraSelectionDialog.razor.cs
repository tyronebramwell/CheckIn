using BlazorCameraStreamer;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class CameraSelectionDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public MediaDeviceInfoModel[]? Cameras { get; set; }
    [Parameter] public string? CurrentDeviceId { get; set; }
    [Parameter] public bool IsEnabled { get; set; }

    private string? _selectedDeviceId;
    private bool _isCameraEnabled;

    protected override void OnInitialized()
    {
        _selectedDeviceId = CurrentDeviceId;
        _isCameraEnabled = IsEnabled;
        
        // If no device is selected but cameras are available, pick the first one
        if (string.IsNullOrEmpty(_selectedDeviceId) && Cameras != null && Cameras.Length > 0)
        {
            _selectedDeviceId = Cameras[0].DeviceId;
        }
    }

    private string GetCameraDisplayName(MediaDeviceInfoModel camera)
    {
        // If a friendly label exists, use it.
        if (!string.IsNullOrEmpty(camera.Label))
        {
            return camera.Label;
        }

        // Otherwise, create a name from the Device ID, with safety checks.
        if (string.IsNullOrEmpty(camera.DeviceId))
        {
            return "Unnamed Camera";
        }

        // Safely truncate the device ID.
        if (camera.DeviceId.Length > 8)
        {
            return $"Camera ({camera.DeviceId.Substring(0, 8)}...)";
        }

        return $"Camera ({camera.DeviceId})";
    }

    private void Apply()
    {
        MudDialog.Close(DialogResult.Ok(new CameraSelectionResult 
        { 
            DeviceId = _selectedDeviceId, 
            IsEnabled = _isCameraEnabled 
        }));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    public class CameraSelectionResult
    {
        public string? DeviceId { get; set; }
        public bool IsEnabled { get; set; }
    }
}
