using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using MouseMotionInspector.Core;

namespace MouseMotionInspector.Native;

internal static class Win
{
    internal const int WM_INPUT = 0xFF, WM_DEVICE = 0xFE, WM_HOTKEY = 0x312, WM_STOP = 0x8001;
    internal const uint RID_INPUT = 0x10000003, RIDI_NAME = 0x20000007;
    [StructLayout(LayoutKind.Sequential)] internal struct RawHeader { public uint Type, Size; public nint Device, WParam; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] internal struct RawMouse
    {
        [FieldOffset(0)] public ushort Flags;
        [FieldOffset(4)] public ushort ButtonFlags;
        [FieldOffset(6)] public ushort ButtonData;
        [FieldOffset(8)] public uint RawButtons;
        [FieldOffset(12)] public int X;
        [FieldOffset(16)] public int Y;
        [FieldOffset(20)] public uint Extra;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct RawDevice { public ushort Page, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)] internal struct DeviceList { public nint Device; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct HookInfo { public Point Point; public uint MouseData, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct HidAttributes { public uint Size; public ushort Vendor, Product, Version; }
    [StructLayout(LayoutKind.Sequential)] internal struct InterfaceData { public uint Size; public Guid ClassGuid; public uint Flags; public nuint Reserved; }
    [StructLayout(LayoutKind.Sequential)] internal struct DevInfo { public uint Size; public Guid ClassGuid; public uint DevInst; public nuint Reserved; }
    internal delegate nint HookProc(int code, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterRawInputDevices([In] RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetRawInputBuffer(nint data, ref uint size, uint headerSize);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint GetRawInputDeviceList(nint data, ref uint count, uint size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern uint GetRawInputDeviceInfoW(nint device, uint command, nint data, ref uint size);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint SetWindowsHookExW(int id, HookProc proc, nint module, uint thread);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetCursorPos(out Point point);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandleW(string? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetAttributes(SafeFileHandle device, ref HidAttributes attributes);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetProductString(SafeFileHandle device, StringBuilder text, uint bytes);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetManufacturerString(SafeFileHandle device, StringBuilder text, uint bytes);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint SetupDiCreateDeviceInfoList(nint guid, nint parent);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiOpenDeviceInterfaceW(nint set, string path, uint flags, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDeviceInterfaceDetailW(nint set, ref InterfaceData data, nint detail, uint size, out uint required, ref DevInfo device);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDeviceRegistryPropertyW(nint set, ref DevInfo device, uint property, out uint type, byte[] buffer, uint size, out uint required);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDeviceInstanceIdW(nint set, ref DevInfo device, StringBuilder text, uint size, out uint required);
    [DllImport("setupapi.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiDestroyDeviceInfoList(nint set);

    internal static List<nint> MouseDevices()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint count = 0; var size = (uint)Marshal.SizeOf<DeviceList>();
            if (GetRawInputDeviceList(0, ref count, size) == uint.MaxValue) throw new Win32Exception();
            if (count == 0) return [];
            var memory = Marshal.AllocHGlobal(checked((int)(count * size)));
            try
            {
                var result = GetRawInputDeviceList(memory, ref count, size);
                if (result == uint.MaxValue) { if (Marshal.GetLastWin32Error() == 122) continue; throw new Win32Exception(); }
                List<nint> devices = [];
                for (var i = 0; i < result; i++) { var device = Marshal.PtrToStructure<DeviceList>(memory + checked((int)(i * size))); if (device.Type == 0) devices.Add(device.Device); }
                return devices;
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        throw new InvalidOperationException("Device list changed repeatedly; try again.");
    }
    internal static DeviceInfo Describe(int id, nint handle)
    {
        var handleText = $"0x{handle:X}";
        if (handle == 0) return new(id, handleText, "", "Unattributed Windows pointer", "", "", "", "Windows supplied no device handle. Precision touchpad input is one possible source; this is not proof of a specific device.");
        uint chars = 0; string path = "", name = "", manufacturer = "", ids = "", instance = "";
        if (GetRawInputDeviceInfoW(handle, RIDI_NAME, 0, ref chars) != uint.MaxValue && chars is > 0 and < 32768)
        {
            var buffer = Marshal.AllocHGlobal(checked((int)(chars + 1) * 2));
            try { if (GetRawInputDeviceInfoW(handle, RIDI_NAME, buffer, ref chars) != uint.MaxValue) path = Marshal.PtrToStringUni(buffer, (int)chars)?.TrimEnd('\0') ?? ""; }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        if (path.Length > 0)
        {
            // Query metadata only: zero desired access; never read/write feature or output reports.
            using var file = CreateFileW(path, 0, 3, 0, 3, 0, 0);
            if (!file.IsInvalid)
            {
                var attr = new HidAttributes { Size = (uint)Marshal.SizeOf<HidAttributes>() };
                if (HidD_GetAttributes(file, ref attr)) ids = $"{attr.Vendor:X4}:{attr.Product:X4}";
                var text = new StringBuilder(256);
                if (HidD_GetProductString(file, text, 512)) name = text.ToString();
                text.Clear(); if (HidD_GetManufacturerString(file, text, 512)) manufacturer = text.ToString();
            }
            var set = SetupDiCreateDeviceInfoList(0, 0);
            if (set != -1)
            {
                try
                {
                    var iface = new InterfaceData { Size = (uint)Marshal.SizeOf<InterfaceData>() };
                    var dev = new DevInfo { Size = (uint)Marshal.SizeOf<DevInfo>() };
                    if (SetupDiOpenDeviceInterfaceW(set, path, 0, ref iface))
                    {
                        SetupDiGetDeviceInterfaceDetailW(set, ref iface, 0, 0, out var required, ref dev);
                        if (required is > 0 and < 65536)
                        {
                            var detail = Marshal.AllocHGlobal((int)required);
                            try
                            {
                                Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                                if (SetupDiGetDeviceInterfaceDetailW(set, ref iface, detail, required, out _, ref dev))
                                {
                                    string Property(uint property)
                                    {
                                        var bytes = new byte[4096];
                                        return SetupDiGetDeviceRegistryPropertyW(set, ref dev, property, out _, bytes, (uint)bytes.Length, out var length)
                                            ? Encoding.Unicode.GetString(bytes, 0, Math.Min((int)length, bytes.Length)).TrimEnd('\0') : "";
                                    }
                                    if (name.Length == 0) name = Property(12); // SPDRP_FRIENDLYNAME
                                    if (name.Length == 0) name = Property(0);  // SPDRP_DEVICEDESC
                                    if (manufacturer.Length == 0) manufacturer = Property(11);
                                    var instanceText = new StringBuilder(1024);
                                    if (SetupDiGetDeviceInstanceIdW(set, ref dev, instanceText, 1024, out _)) instance = instanceText.ToString();
                                }
                            }
                            finally { Marshal.FreeHGlobal(detail); }
                        }
                    }
                }
                finally { SetupDiDestroyDeviceInfoList(set); }
            }
        }
        return new(id, handleText, path, name.Length > 0 ? name : $"Mouse interface {id}", manufacturer, ids, instance,
            path.Length > 0 ? "Identity is for a Windows mouse interface. A receiver or virtual driver may represent more than one physical device. Move only one device at a time to verify its row." : "Metadata unavailable (possibly removed). The capture handle still distinguishes this input stream during this attachment.");
    }
}
