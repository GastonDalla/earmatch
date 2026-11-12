using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace Earmatch.App.Capture;

/// The Direct3D 11 device Windows.Graphics.Capture renders the frames on, and the CPU copy of a frame's texture. The
/// few COM calls are made through their vtable slots (d3d11.h order), so no interop package is needed. A device is used
/// from one thread at a time (its immediate context is not thread-safe): the capture serializes its frames.
internal sealed unsafe class Direct3D : IDisposable
{
    private const int DriverTypeHardware = 1;
    private const int DriverTypeWarp = 5;
    private const uint CreateDeviceBgraSupport = 0x20;
    private const uint SdkVersion = 7;
    private const int FormatB8G8R8A8Unorm = 87;
    private const int UsageStaging = 3;
    private const uint CpuAccessRead = 0x20000;
    private const int MapRead = 1;

    private static Guid DxgiDeviceId = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
    private static Guid Texture2DId = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    private static Guid DxgiInterfaceAccessId = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
    private static Guid CaptureItemInteropId = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static Guid CaptureItemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [StructLayout(LayoutKind.Sequential)]
    private struct Texture2DDescription
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public int Format;
        public uint SampleCount;
        public uint SampleQuality;
        public int Usage;
        public uint BindFlags;
        public uint CpuAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MappedSubresource
    {
        public byte* Data;
        public uint RowPitch;
        public uint DepthPitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Box
    {
        public uint Left;
        public uint Top;
        public uint Front;
        public uint Right;
        public uint Bottom;
        public uint Back;
    }

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr featureLevels, uint levels,
        uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(IntPtr className, ref Guid iid, out IntPtr factory);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string text, int length, out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    private readonly IntPtr _device;
    private readonly IntPtr _context;
    private IntPtr _staging;
    private (uint Width, uint Height) _stagingSize;

    private Direct3D(IntPtr device, IntPtr context, IDirect3DDevice winRTDevice)
    {
        _device = device;
        _context = context;
        Device = winRTDevice;
    }

    /// The device as Windows.Graphics.Capture takes it.
    public IDirect3DDevice Device { get; }

    /// A hardware device, or WARP (Windows' software rasterizer) where there is no usable GPU.
    public static Direct3D Create()
    {
        var result = D3D11CreateDevice(IntPtr.Zero, DriverTypeHardware, IntPtr.Zero, CreateDeviceBgraSupport, IntPtr.Zero, 0, SdkVersion, out var device,
            out _, out var context);
        if (result < 0)
        {
            result = D3D11CreateDevice(IntPtr.Zero, DriverTypeWarp, IntPtr.Zero, CreateDeviceBgraSupport, IntPtr.Zero, 0, SdkVersion, out device, out _,
                out context);
        }
        Marshal.ThrowExceptionForHR(result);
        IntPtr dxgi = IntPtr.Zero, inspectable = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, in DxgiDeviceId, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            return new Direct3D(device, context, MarshalInterface<IDirect3DDevice>.FromAbi(inspectable));
        }
        catch
        {
            Marshal.Release(context);
            Marshal.Release(device);
            throw;
        }
        finally
        {
            if (dxgi != IntPtr.Zero) Marshal.Release(dxgi);
            if (inspectable != IntPtr.Zero) Marshal.Release(inspectable);
        }
    }

    /// The capture item of a top-level window (IGraphicsCaptureItemInterop::CreateForWindow).
    public static GraphicsCaptureItem ItemForWindow(IntPtr window)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var name));
        IntPtr factory = IntPtr.Zero, item = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, ref CaptureItemInteropId, out factory));
            var createForWindow = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)(*(void***)factory)[3];
            fixed (Guid* id = &CaptureItemId) Marshal.ThrowExceptionForHR(createForWindow(factory, window, id, &item));
            return GraphicsCaptureItem.FromAbi(item);
        }
        finally
        {
            if (item != IntPtr.Zero) Marshal.Release(item);
            if (factory != IntPtr.Zero) Marshal.Release(factory);
            WindowsDeleteString(name);
        }
    }

    /// Copies the top-left `width` x `height` pixels of a captured frame's surface (BGRA) into `buffer`, `stride` bytes
    /// a row, through a staging texture the CPU can read. A surface smaller than that is copied whole: the size copied is
    /// returned, and only those pixels of `buffer` were written.
    public (int Width, int Height) Copy(IDirect3DSurface surface, int width, int height, byte[] buffer, int stride)
    {
        IntPtr access = IntPtr.Zero, texture = IntPtr.Zero;
        try
        {
            var native = ((IWinRTObject)surface).NativeObject.ThisPtr;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native, in DxgiInterfaceAccessId, out access));
            var getInterface = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)(*(void***)access)[3];
            fixed (Guid* id = &Texture2DId) Marshal.ThrowExceptionForHR(getInterface(access, id, &texture));

            Texture2DDescription description;
            ((delegate* unmanaged[Stdcall]<IntPtr, Texture2DDescription*, void>)(*(void***)texture)[10])(texture, &description);
            var copyWidth = Math.Min(description.Width, (uint)Math.Max(width, 0));
            var copyHeight = Math.Min(description.Height, (uint)Math.Max(height, 0));
            if (copyWidth == 0 || copyHeight == 0) return (0, 0);
            EnsureStaging(copyWidth, copyHeight);

            var box = new Box { Right = copyWidth, Bottom = copyHeight, Back = 1 };
            ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, uint, uint, uint, IntPtr, uint, Box*, void>)(*(void***)_context)[46])(
                _context, _staging, 0, 0, 0, 0, texture, 0, &box);
            MappedSubresource mapped;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, int, uint, MappedSubresource*, int>)(*(void***)_context)[14])(
                _context, _staging, 0, MapRead, 0, &mapped));
            try
            {
                var rowBytes = (int)copyWidth * 4;
                fixed (byte* destination = buffer)
                {
                    for (var row = 0; row < copyHeight; row++)
                    {
                        Buffer.MemoryCopy(mapped.Data + row * mapped.RowPitch, destination + row * stride, buffer.Length - row * stride, rowBytes);
                    }
                }
            }
            finally
            {
                ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, uint, void>)(*(void***)_context)[15])(_context, _staging, 0);
            }
            return ((int)copyWidth, (int)copyHeight);
        }
        finally
        {
            if (texture != IntPtr.Zero) Marshal.Release(texture);
            if (access != IntPtr.Zero) Marshal.Release(access);
        }
    }

    private void EnsureStaging(uint width, uint height)
    {
        if (_staging != IntPtr.Zero && _stagingSize == (width, height)) return;
        if (_staging != IntPtr.Zero) Marshal.Release(_staging);
        _staging = IntPtr.Zero;
        var description = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = FormatB8G8R8A8Unorm,
            SampleCount = 1,
            Usage = UsageStaging,
            CpuAccessFlags = CpuAccessRead,
        };
        IntPtr staging;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<IntPtr, Texture2DDescription*, IntPtr, IntPtr*, int>)(*(void***)_device)[5])(
            _device, &description, IntPtr.Zero, &staging));
        _staging = staging;
        _stagingSize = (width, height);
    }

    public void Dispose()
    {
        if (_staging != IntPtr.Zero) Marshal.Release(_staging);
        _staging = IntPtr.Zero;
        (Device as IDisposable)?.Dispose();
        Marshal.Release(_context);
        Marshal.Release(_device);
    }
}
