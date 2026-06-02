using System;
using System.Collections.Generic;
using CheckInCommon.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace CheckIn.Components;

public partial class EventSelectionDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public List<Event> Events { get; set; } = new();

    private void Select(Guid eventId) => MudDialog.Close(DialogResult.Ok(eventId));
    private void Cancel() => MudDialog.Cancel();
}
