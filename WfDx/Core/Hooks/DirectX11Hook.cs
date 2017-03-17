using System;
using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
/*
    C38 - оффсет для патронов
    2EB379F0-DFF0

    522A7A00
     */
namespace WfDx.Core.Hooks
{
    internal class DirectX11Hook : DirectXHook
    {
        private const int SwapChainMethodsCount = 18;
        private const int DeviceContextMethodsCount = 107;

        private IntPtr[] swapChainAdresses;
        private IntPtr[] deviceContextAddresses;
        private readonly ServerInterface server = EntryPoint.Server;

        private Hook<PresentDelegate> presentHook;
        private Hook<DrawIndexedDelegate> drawIndexedHook;

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate int PresentDelegate(IntPtr swapChainPtr, int syncInterval, PresentFlags flags);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        private delegate void DrawIndexedDelegate(
            IntPtr pContext, int indexCount, int startIndexLocation, int baseVertexLocation);

        public override void InstallHook()
        {
            SharpDX.Direct3D11.Device device;
            SwapChain swapChain;
            var form = new RenderForm();

            SharpDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport,
                new SwapChainDescription
                {
                    BufferCount = 1,
                    Flags = SwapChainFlags.None,
                    IsWindowed = true,
                    ModeDescription = new ModeDescription(100, 100, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                    OutputHandle = form.Handle,
                    SampleDescription = new SampleDescription(1, 0),
                    SwapEffect = SwapEffect.Discard,
                    Usage = Usage.RenderTargetOutput
                }, out device, out swapChain);

            if (device != null && swapChain != null)
            {
                server.DebugMessage("Device succesfuly created");
                swapChainAdresses = GetVTableAdresses(swapChain.NativePointer, SwapChainMethodsCount);
                deviceContextAddresses = GetVTableAdresses(device.ImmediateContext.NativePointer, DeviceContextMethodsCount);
               
                presentHook = new Hook<PresentDelegate>(swapChainAdresses[(int)SwapChainVTbl.Present], new PresentDelegate(MyPresent), this);
                presentHook.Activate();

                drawIndexedHook = new Hook<DrawIndexedDelegate>(deviceContextAddresses[12], new DrawIndexedDelegate(MyDrawIndexed), this);
                drawIndexedHook.Activate();

                server.SendMessage("Hook enabled!");

                device.Dispose();
                swapChain.Dispose();
            }
            else
            {
                server.DebugMessage("Device creation failed!");
            }
            form.Dispose();
        }

        public override void UninstallHook()
        {
            presentHook.Dispose();
            drawIndexedHook.Dispose();
        }

        private int MyPresent(IntPtr swapChainPtr, int syncInterval, PresentFlags flags)
        {

            return presentHook.Origin(swapChainPtr, syncInterval, flags);
        }

        private void MyDrawIndexed(IntPtr pContext, int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            drawIndexedHook.Origin(pContext, indexCount, startIndexLocation, baseVertexLocation);
        }
    }
}
