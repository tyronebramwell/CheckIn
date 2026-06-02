using System;
using Microsoft.AspNetCore.Components;

namespace CheckIn.Pages;

public partial class NotFound
{
    [Inject] private NavigationManager NavManager { get; set; } = default!;

    private void HomePage()
    {
        NavManager.NavigateTo("/");
    }

    private string ErrorImage()
    {
        string[] images = new string[]
        {
            "images/Error/1.gif",
            "images/Error/2.gif",
            "images/Error/Error.webp",
        };
        Random rand = new Random();
        int index = rand.Next(images.Length);
        return images[index];
    }
}
