using ABI.Microsoft.Graphics.Canvas;
using System;
using System.Diagnostics.CodeAnalysis;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using TerraFX.Interop.WinRT;
using Windows.Graphics.DirectX;
using WinRT;

namespace Win2DRenderer.Backend;

using Color = Windows.UI.Color;
using CanvasDevice = Microsoft.Graphics.Canvas.CanvasDevice;
using CanvasDrawingSession = Microsoft.Graphics.Canvas.CanvasDrawingSession;
using CanvasSwapChain = Microsoft.Graphics.Canvas.CanvasSwapChain;
using Windows = TerraFX.Interop.Windows.Windows;

/// <summary>
/// A simple Win32 application handling a D2D swapchain.
/// </summary>
internal sealed class Win32Application : IDisposable
{
    /// <summary>
    /// The <see cref="CanvasDevice"/> instance to use.
    /// </summary>
    private CanvasDevice? canvasDevice;

    /// <summary>
    /// The <see cref="CanvasSwapChain"/> instance to use to render frames.
    /// </summary>
    private CanvasSwapChain? canvasSwapChain;

    /// <summary>
    /// The current screen width in raw pixels.
    /// </summary>
    private uint screenWidth;

    /// <summary>
    /// The current screen height in raw pixels.
    /// </summary>
    private uint screenHeight;

    /// <summary>
    /// Composition device to set up the composition
    /// </summary>
    private ComPtr<IDCompositionDevice> dcompDevice;

    /// <summary>
    /// A composition target to the window
    /// </summary>
    private ComPtr<IDCompositionTarget> dcompTarget;

    /// <summary>
    /// A visual which displays the swapchain backbuffer
    /// </summary>
    private ComPtr<IDCompositionVisual> dcompVisual;

    /// <summary>
    /// Raised whenever a draw operation can be performed.
    /// </summary>
    public event EventHandler<DrawEventArgs>? Draw;

    /// <summary>
    /// Initializes the current application.
    /// </summary>
    /// <param name="hwnd">The handle for the window.</param>
    [MemberNotNull(nameof(canvasDevice))]
    [MemberNotNull(nameof(canvasSwapChain))]
    public unsafe void OnInitialize(HWND hwnd, int width, int height)
    {
        // Create a new canvas device, which will handle DX11/D2D initialization
        CanvasDevice canvasDevice = new();
        CanvasSwapChain canvasSwapChain;

        // Create the swap chain to display frames
        using (ComPtr<IUnknown> direct3DDeviceUnknown = default)
        using (ComPtr<IDXGIDevice> directDxgiDevice = default)
        using (ComPtr<IDXGIFactory2> dxgiFactory2 = default)
        using (ComPtr<IDXGISwapChain1> dxgiSwapChain1 = default)
        {
            HRESULT hresult;

            // Extract the underlying Direct3D device from the canvas device.
            // This is needed for CreateSwapChainForHwnd, as the owning device.
            using (ComPtr<IUnknown> canvasDeviceUnknown = default)
            using (ComPtr<IDirect3DDxgiInterfaceAccess> direct3DDxgiInterfaceAccess = default)
            {
                canvasDeviceUnknown.Attach((IUnknown*)MarshalInspectable<CanvasDevice>.FromManaged(canvasDevice));

                hresult = canvasDeviceUnknown.CopyTo(direct3DDxgiInterfaceAccess.GetAddressOf());

                ExceptionHelpers.ThrowExceptionForHR(hresult);

                hresult = direct3DDxgiInterfaceAccess.Get()->GetInterface(Windows.__uuidof<IUnknown>(), (void**)direct3DDeviceUnknown.GetAddressOf());

                ExceptionHelpers.ThrowExceptionForHR(hresult);

                hresult = direct3DDeviceUnknown.Get()->QueryInterface(Windows.__uuidof<IDXGIDevice>(), (void**)directDxgiDevice.GetAddressOf());

                ExceptionHelpers.ThrowExceptionForHR(hresult);
            }

            // Create the DXGIFactory2 instance to create the swapchain with
            hresult = DirectX.CreateDXGIFactory2(DXGI.DXGI_CREATE_FACTORY_DEBUG, Windows.__uuidof<IDXGIFactory2>(), (void**)dxgiFactory2.GetAddressOf());

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            DXGI_SWAP_CHAIN_DESC1 dxgiSwapChainDesc1 = default;
            dxgiSwapChainDesc1.AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_PREMULTIPLIED;
            dxgiSwapChainDesc1.BufferCount = 2;
            dxgiSwapChainDesc1.BufferUsage = DXGI.DXGI_USAGE_RENDER_TARGET_OUTPUT;
            dxgiSwapChainDesc1.Flags = 0;
            dxgiSwapChainDesc1.Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM;
            dxgiSwapChainDesc1.Width = this.screenWidth = 1;
            dxgiSwapChainDesc1.Height = this.screenHeight = 1;
            dxgiSwapChainDesc1.SampleDesc = new DXGI_SAMPLE_DESC(count: 1, quality: 0);
            dxgiSwapChainDesc1.Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH;
            dxgiSwapChainDesc1.Stereo = 0;
            dxgiSwapChainDesc1.SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;

            // Create the native DXGI swapchain object to wrap
            hresult = dxgiFactory2.Get()->CreateSwapChainForComposition(
                direct3DDeviceUnknown.Get(),
                &dxgiSwapChainDesc1,
                null,
                dxgiSwapChain1.GetAddressOf());

            ExceptionHelpers.ThrowExceptionForHR(hresult);
            
            // Create the composition visuals and layers for the wrapped swapchain to draw on
            hresult = DirectX.DCompositionCreateDevice(
                directDxgiDevice.Get(),
                Windows.__uuidof<IDCompositionDevice>(),
                (void**)dcompDevice.GetAddressOf()
            );

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            hresult = dcompDevice.Get()->CreateTargetForHwnd(
                hwnd,
                false,
                dcompTarget.GetAddressOf()
            );

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            hresult = dcompDevice.Get()->CreateVisual(
                dcompVisual.GetAddressOf()
            );

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            // Everything thats drawn white whill instead be the inverted color of whats underneath
            hresult = dcompVisual.Get()->SetCompositeMode(DCOMPOSITION_COMPOSITE_MODE.DCOMPOSITION_COMPOSITE_MODE_DESTINATION_INVERT);

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            hresult = dcompVisual.Get()->SetContent(
                (IUnknown*)dxgiSwapChain1.Get()
            );

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            hresult = dcompTarget.Get()->SetRoot(dcompVisual.Get());

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            hresult = dcompDevice.Get()->Commit();

            ExceptionHelpers.ThrowExceptionForHR(hresult);

            using ComPtr<ICanvasFactoryNative> canvasFactoryNative = default;

            // Get the activation factory for CanvasDevice
            using (ComPtr<IUnknown> canvasFactoryNativeUnknown = default)
            {
                ICanvasFactoryNative.Interface canvasDeviceActivationFactory = CanvasDevice.As<ICanvasFactoryNative.Interface>();

                canvasFactoryNativeUnknown.Attach((IUnknown*)MarshalInterface<ICanvasFactoryNative.Interface>.FromManaged(canvasDeviceActivationFactory));

                hresult = canvasFactoryNativeUnknown.CopyTo(canvasFactoryNative.GetAddressOf());

                ExceptionHelpers.ThrowExceptionForHR(hresult);
            }

            // Create a Win2D wrapper for the swapchain
            using (ComPtr<IUnknown> canvasDeviceUnknown = default)
            using (ComPtr<IUnknown> canvasSwapChainUnknown = default)
            {
                canvasDeviceUnknown.Attach((IUnknown*)MarshalInspectable<CanvasDevice>.FromManaged(canvasDevice));

                hresult = canvasFactoryNative.Get()->GetOrCreate(
                    device: canvasDeviceUnknown.Get(),
                    resource: (IUnknown*)dxgiSwapChain1.Get(),
                    dpi: 96.0f,
                    wrapper: (void**)canvasSwapChainUnknown.GetAddressOf());

                ExceptionHelpers.ThrowExceptionForHR(hresult);

                // Marshal to a WinRT managed object
                canvasSwapChain = MarshalInspectable<CanvasSwapChain>.FromAbi((IntPtr)canvasSwapChainUnknown.Get());
            }

            canvasSwapChain!.ResizeBuffers(
                newWidth: width,
                newHeight: height,
                newDpi: 96.0f,
                newFormat: DirectXPixelFormat.Unknown,
                bufferCount: 0
            );
        }

        // Save the Win2D objects for later use
        this.canvasDevice = canvasDevice;
        this.canvasSwapChain = canvasSwapChain;
        this.screenWidth = (uint)width;
        this.screenHeight = (uint)height;
    }

    /// <summary>
    /// Updates the current application.
    /// </summary>
    /// <param name="time">The current time since the start of the application.</param>
    public void OnUpdate(TimeSpan time)
    {
        // Create the drawing session and invoke all registered draw handler
        using (CanvasDrawingSession canvasDrawingSession = this.canvasSwapChain!.CreateDrawingSession(default(Color)))
        {
            Draw?.Invoke(this, new DrawEventArgs
            {
                ScreenWidth = this.screenWidth,
                ScreenHeight = this.screenHeight,
                TotalTime = time,
                DrawingSession = canvasDrawingSession
            });
        }

        // Present the new frame
        this.canvasSwapChain.Present(syncInterval: 4);
    }

    public void Dispose()
    {
        this.canvasDevice?.Dispose();
        this.canvasDevice = null;
        this.canvasSwapChain?.Dispose();
        this.canvasSwapChain = null;
        this.dcompDevice.Dispose();
        this.dcompTarget.Dispose();
        this.dcompVisual.Dispose();
    }

    /// <summary>
    /// Arguments for <see cref="Draw"/>.
    /// </summary>
    public sealed class DrawEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the screen width in raw pixels.
        /// </summary>
        public required uint ScreenWidth { get; init; }

        /// <summary>
        /// Gets the screen height in raw pixels.
        /// </summary>
        public required uint ScreenHeight { get; init; }

        /// <summary>
        /// Gets the total time for the rendering loop.
        /// </summary>
        public required TimeSpan TotalTime { get; init; }

        /// <summary>
        /// Gets the <see cref="CanvasDrawingSession"/> instance to use.
        /// </summary>
        public required CanvasDrawingSession DrawingSession { get; init; }
    }
}
