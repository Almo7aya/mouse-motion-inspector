using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MouseMotionInspector.Core;

namespace MouseMotionInspector.Native;

internal sealed class CaptureEngine : IDisposable
{
    private readonly ConcurrentQueue<Packet> queue = new();
    private readonly Thread thread;
    private nint windowHandle;
    private int queued;
    private long dropped;
    private int disposed;
    private volatile bool ready, hookInstalled, hotkeyInstalled;
    private volatile string? fatalError;
    public long Dropped => Interlocked.Read(ref dropped);
    public bool Ready => ready;
    public bool HookInstalled => hookInstalled;
    public bool HotkeyInstalled => hotkeyInstalled;
    public string? FatalError => fatalError;
    public static double Now => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
    public CaptureEngine()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "Mouse Motion Inspector input listener" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
    }
    public bool TryRead(out Packet packet) { if (!queue.TryDequeue(out packet)) return false; Interlocked.Decrement(ref queued); return true; }
    public bool HasPendingBefore(double time) => queue.TryPeek(out var packet) && packet.Time < time;
    private void Enqueue(Packet packet)
    {
        if (Interlocked.Increment(ref queued) > 100_000) { Interlocked.Decrement(ref queued); Interlocked.Increment(ref dropped); return; }
        queue.Enqueue(packet);
    }
    private void Run()
    {
        try
        {
            using var listener = new Listener(this);
            Interlocked.Exchange(ref windowHandle, listener.Handle);
            if (Volatile.Read(ref disposed) != 0) return;
            ready = true;
            Application.Run();
            if (Volatile.Read(ref disposed) == 0) throw new InvalidOperationException("The input listener stopped unexpectedly. Restart the app before testing.");
        }
        catch (Exception e) { fatalError = e.Message; Enqueue(new(PacketKind.Error, Now, Detail: e.Message)); }
        finally { ready = false; Interlocked.Exchange(ref windowHandle, 0); }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        var handle = Interlocked.CompareExchange(ref windowHandle, 0, 0);
        if (handle != 0) Win.PostMessageW(handle, Win.WM_STOP, 0, 0);
        thread.Join(2000);
    }

    private sealed class Listener : NativeWindow, IDisposable
    {
        private const int Capacity = 65536;
        private readonly CaptureEngine owner;
        private readonly Dictionary<nint, int> devices = [];
        private readonly nint buffer;
        private readonly Win.HookProc hookProc;
        private nint hook;
        private int nextDevice;
        private bool reportedReadError;
        private static readonly uint HeaderSize = (uint)Marshal.SizeOf<Win.RawHeader>();
        public Listener(CaptureEngine engine)
        {
            owner = engine; hookProc = OnHook;
            buffer = Marshal.AllocHGlobal(Capacity);
            try
            {
                CreateHandle(new CreateParams { Caption = "Mouse Motion Inspector raw input listener", Parent = new nint(-3) });
                Win.RawDevice[] registrations = [new() { Page = 1, Usage = 2, Flags = 0x100 | 0x2000, Target = Handle }];
                if (!Win.RegisterRawInputDevices(registrations, 1, (uint)Marshal.SizeOf<Win.RawDevice>())) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register Windows Raw Input.");
                foreach (var device in Win.MouseDevices()) EnsureDevice(device);
                hook = Win.SetWindowsHookExW(14, hookProc, Win.GetModuleHandleW(null), 0);
                owner.hookInstalled = hook != 0;
                if (hook == 0) owner.Enqueue(new(PacketKind.Error, Now, Detail: $"Injected-input monitor unavailable (Win32 {Marshal.GetLastWin32Error()}). Raw device capture remains available."));
                owner.hotkeyInstalled = Win.RegisterHotKey(Handle, 1, 0x4000 | 0x2 | 0x4, 0x77); // Ctrl+Shift+F8; no repeat
                if (!owner.HotkeyInstalled) owner.Enqueue(new(PacketKind.Error, Now, Detail: "Ctrl+Shift+F8 is already reserved. Use the Start button and Esc while the window is focused."));
            }
            catch { Dispose(); throw; }
        }
        private int EnsureDevice(nint handle)
        {
            if (devices.TryGetValue(handle, out var id)) return id;
            id = ++nextDevice; devices.Add(handle, id);
            owner.Enqueue(new(PacketKind.Arrival, Now, id, handle.ToInt64())); return id;
        }
        private nint OnHook(int code, nint wParam, nint lParam)
        {
            // Keep the low-level hook short: no metadata, disk, locks or UI work here.
            if (code >= 0 && wParam == 0x200)
            {
                var value = Marshal.PtrToStructure<Win.HookInfo>(lParam);
                if ((value.Flags & 1) != 0) owner.Enqueue(new(PacketKind.Injected, Now, X: value.Point.X, Y: value.Point.Y,
                    Flags: (ushort)value.Flags, WindowsTime: value.Time));
            }
            return Win.CallNextHookEx(hook, code, wParam, lParam);
        }
        private void Error(string detail)
        {
            if (reportedReadError) return; reportedReadError = true;
            owner.Enqueue(new(PacketKind.Error, Now, Detail: detail));
        }
        private void ReadPacket(nint ptr, int available)
        {
            if (available < HeaderSize) { Error("Truncated Raw Input header."); return; }
            var header = Marshal.PtrToStructure<Win.RawHeader>(ptr);
            if (header.Type != 0) return;
            if (header.Size < HeaderSize + 24 || header.Size > available) { Error("Invalid Raw Input mouse packet size."); return; }
            var mouse = Marshal.PtrToStructure<Win.RawMouse>(ptr + (int)HeaderSize);
            var id = EnsureDevice(header.Device);
            owner.Enqueue(new(PacketKind.Raw, Now, id, header.Device.ToInt64(), mouse.X, mouse.Y, mouse.Flags, mouse.ButtonFlags));
        }
        private void ReadInput(nint input)
        {
            uint size = Capacity;
            var read = Win.GetRawInputData(input, Win.RID_INPUT, buffer, ref size, HeaderSize);
            if (read == uint.MaxValue) Error($"GetRawInputData failed (Win32 {Marshal.GetLastWin32Error()}). Results may be incomplete.");
            else ReadPacket(buffer, (int)read);
            // Read the current WM_INPUT first, then drain later messages without double-counting.
            // Native x64 avoids the distinct WOW64 buffer layout.
            for (var batch = 0; batch < 8; batch++)
            {
                size = Capacity; var count = Win.GetRawInputBuffer(buffer, ref size, HeaderSize);
                if (count == uint.MaxValue) { Error($"GetRawInputBuffer failed (Win32 {Marshal.GetLastWin32Error()}). Results may be incomplete."); break; }
                if (count == 0) break;
                var offset = 0;
                for (uint i = 0; i < count; i++)
                {
                    if (offset > Capacity - HeaderSize) { Error("Invalid raw buffer offset."); return; }
                    var header = Marshal.PtrToStructure<Win.RawHeader>(buffer + offset);
                    if (header.Size < HeaderSize || header.Size > Capacity - offset) { Error("Invalid raw buffer size."); return; }
                    ReadPacket(buffer + offset, (int)header.Size);
                    offset += checked(((int)header.Size + IntPtr.Size - 1) & ~(IntPtr.Size - 1));
                }
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win.WM_STOP) { Application.ExitThread(); m.Result = 0; return; }
            try
            {
                if (m.Msg == Win.WM_INPUT) ReadInput(m.LParam);
                else if (m.Msg == Win.WM_DEVICE)
                {
                    if (m.WParam == 1) EnsureDevice(m.LParam);
                    else if (devices.Remove(m.LParam, out var id)) owner.Enqueue(new(PacketKind.Removal, Now, id, m.LParam.ToInt64()));
                }
                else if (m.Msg == Win.WM_HOTKEY) owner.Enqueue(new(PacketKind.Hotkey, Now));
            }
            catch (Exception e) { Error($"Input processing error: {e.Message}"); }
            base.WndProc(ref m); // DefWindowProc performs foreground WM_INPUT cleanup.
        }
        public void Dispose()
        {
            if (hook != 0) { Win.UnhookWindowsHookEx(hook); hook = 0; }
            if (Handle != 0)
            {
                Win.UnregisterHotKey(Handle, 1);
                Win.RawDevice[] remove = [new() { Page = 1, Usage = 2, Flags = 1, Target = 0 }];
                Win.RegisterRawInputDevices(remove, 1, (uint)Marshal.SizeOf<Win.RawDevice>());
                DestroyHandle();
            }
            Marshal.FreeHGlobal(buffer);
        }
    }
}
