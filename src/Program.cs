using Microsoft.Graphics.Canvas.Text;
using System;
using System.Runtime.CompilerServices;
using Win2DRenderer.Backend;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;

[assembly: DisableRuntimeMarshalling]

using Win32Application win32Application = new();

win32Application.Draw += static (_, e) =>
{
    // Draw frames here...
    using CanvasTextFormat textFormat = new()
    {
        FontFamily = "Segoe UI",
        FontSize = 100.0f,
        FontWeight = new FontWeight(700),
        HorizontalAlignment = CanvasHorizontalAlignment.Center,
        VerticalAlignment = CanvasVerticalAlignment.Center
    };

    e.DrawingSession.DrawText($"Hello, world!\n{e.TotalTime}", new Rect(0, 0, e.ScreenWidth, e.ScreenHeight), Color.FromArgb(255, 255, 255, 255), textFormat);
};

return Win32ApplicationRunner.Run(win32Application, "Win2D sample");
