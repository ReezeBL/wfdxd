using System;
using System.Threading;
using System.Windows.Forms;
using EasyHook;
using WfDx.Core;
using WfDx.Core.Hooks;

namespace WfDx
{
    public class EntryPoint : IEntryPoint
    {
        public static ServerInterface Server { get; private set; }
        private DirectXHook device;

        public EntryPoint(RemoteHooking.IContext context, string channelName)
        {
            Server = RemoteHooking.IpcConnectClient<ServerInterface>(channelName);
            Server.Ping();
        }

        public void Run(RemoteHooking.IContext context, string channelName)
        {
            HideMeister hideMeister = null;
            try
            {
                Server.IsInstalled(RemoteHooking.GetCurrentProcessId());
                RemoteHooking.WakeUpProcess();
                if (DetectDirectXVersion())
                {
                    device.InstallHook();
                }
                hideMeister = new HideMeister();
            }
            catch (Exception e)
            {
                Server.ReportException(e);
            }

            while (Server.RunLibrary)
            {
                Thread.Sleep(500);
            }
            

            device?.UninstallHook();
            hideMeister?.Dispose();
            LocalHook.Release();

            Server.DebugMessage("Hooks uninstalled, you can close app!");
        }

        private bool DetectDirectXVersion()
        {
            var d3D9Loaded = IntPtr.Zero;
            var d3D10Loaded = IntPtr.Zero;
            var d3D11Loaded = IntPtr.Zero;

            const int delayTime = 100;
            var retryCount = 0;
            while (d3D9Loaded == IntPtr.Zero && d3D10Loaded == IntPtr.Zero && d3D11Loaded == IntPtr.Zero)
            {
                retryCount++;
                d3D9Loaded = NativeMethods.GetModuleHandle("d3d9.dll");
                d3D10Loaded = NativeMethods.GetModuleHandle("d3d10.dll");
                d3D11Loaded = NativeMethods.GetModuleHandle("d3d11.dll");

                System.Threading.Thread.Sleep(delayTime);

                if (retryCount * delayTime > 5000)
                {
                    Server.SendMessage("Unsupported Direct3D version, or Direct3D DLL not loaded within 5 seconds.");
                    return false;
                }
            }

            if (d3D9Loaded != IntPtr.Zero)
            {
                Server.SendMessage("Detected DirectX9!");
                return false;

            }

            if (d3D10Loaded != IntPtr.Zero)
            {
                Server.SendMessage("Detected DirectX10!");
                return false;
            }

            if (d3D11Loaded != IntPtr.Zero)
            {
                Server.SendMessage("Detected DirectX11!");
                device = new DirectX11Hook();
                return true;
            }

            return false;
        }

    }
}
