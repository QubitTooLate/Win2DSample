using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace Win2DRenderer.Backend;

using Windows = TerraFX.Interop.Windows.Windows;

/// <summary>
/// A helper class to manage the creation and execution of Win32 applications.
/// </summary>
internal static unsafe class Win32ApplicationRunner
{
    /// <summary>
    /// The <see cref="HWND"/> for the application window.
    /// </summary>
    private static HWND hwnd;

    /// <summary>
    /// The application being run.
    /// </summary>
    private static Win32Application application = null!;

    /// <summary>
    /// Runs a specified application and starts the main loop to update its state. This is the entry point for a given application,
    /// and it should be called as soon as the process is launched, excluding any other additional initialization needed.
    /// </summary>
    /// <param name="application">The input application instance to run.</param>
    /// <param name="applicationName">Name of the input application instance to run.</param>
    /// <returns>The exit code for the application.</returns>
    public static int Run(Win32Application application, string applicationName)
    {
        Win32ApplicationRunner.application = application;

        HMODULE hInstance = Windows.GetModuleHandleW(null);

        Rectangle windowRect = new(0, 0, 1280, 720);

        int height = (windowRect.Bottom - windowRect.Top);
        int width = (windowRect.Right - windowRect.Left);

        fixed (char* name = applicationName)
        {
            // Initialize the window class
            WNDCLASSEXW windowClassEx = new()
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WindowProc,
                hInstance = hInstance,
                hCursor = Windows.LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW),
                lpszClassName = (ushort*)name
            };

            // Register the window class
            _ = Windows.RegisterClassExW(&windowClassEx);

            // Create the window and store a handle to it
            // Using composition its better to not have a redirection bitmap. It's always better for your window to be layered
            // Setting the layered window to be transparent so its "click trough"
            hwnd = Windows.CreateWindowExW(
                WS.WS_EX_NOREDIRECTIONBITMAP | WS.WS_EX_LAYERED | WS.WS_EX_TRANSPARENT | WS.WS_EX_TOPMOST, 
                windowClassEx.lpszClassName,
                (ushort*)name,
                WS.WS_POPUP,
                // Assuming a 1920x1080 display
                (1920 / 2) - (width / 2),
                (1080 / 2) - (height / 2),
                width,
                height,
                HWND.NULL,
                HMENU.NULL,
                hInstance,
                (void*)GCHandle.ToIntPtr(GCHandle.Alloc(application))
            );

            Windows.SetLayeredWindowAttributes(hwnd, default, 255, LWA.LWA_ALPHA);

            // Turns the whole window into Mica (only in newer versions of Windows 11)
            // DWM_SYSTEMBACKDROP_TYPE sbt = DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
            // _ = Windows.DwmSetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, &sbt, 4);

            // Turns the window Mica into dark mode
            // int yes = 1;
            // _ = Windows.DwmSetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &yes, 4);
        }

        // Initialize the application
        application.OnInitialize(hwnd, width, height);

        // Display the window
        _ = Windows.ShowWindow(hwnd, SW.SW_SHOWDEFAULT);

        MSG msg = default;
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Process any messages in the queue
        while (msg.message != WM.WM_QUIT)
        {
            if (Windows.PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE) != 0)
            {
                _ = Windows.DispatchMessageW(&msg);
            }
            else
            {
                application.OnUpdate(stopwatch.Elapsed);
            }
        }

        // Return this part of the WM_QUIT message to Windows
        return (int)msg.wParam;
    }

    /// <summary>
    /// Processes incoming messages for a window.
    /// </summary>
    /// <param name="hwnd">A handle to the window.</param>
    /// <param name="uMsg">The message.</param>
    /// <param name="wParam">Additional message information (the contents depend on the value of <paramref name="uMsg"/>).</param>
    /// <param name="lParam">Additional message information (the contents depend on the value of <paramref name="uMsg"/>).</param>
    /// <returns>The result of the message processing for the input message.</returns>
    [UnmanagedCallersOnly]
    private static LRESULT WindowProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        switch (uMsg)
        {
            // Shutdown
            case WM.WM_DESTROY:
            {
                Windows.PostQuitMessage(0);

                return 0;
            }
        }

        return Windows.DefWindowProcW(hwnd, uMsg, wParam, lParam);
    }
}