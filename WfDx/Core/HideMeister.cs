using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EasyHook;

namespace WfDx.Core
{
    internal class HideMeister
    {
        private readonly ServerInterface client = EntryPoint.Server;
        private delegate IntPtr OpenProcessDelegate(uint processAccess, bool bInheritHandle, int processId);
        private delegate IntPtr CreateToolhelp32SnapshotDelegate(uint flags, uint th32ProcessId);

        private delegate int NtQueryInformationProcessDelegate(
            IntPtr processHandle, int processInformationClass, ref ProcessBasicInformation processInformation,
            uint processInformationLength, ref int returnLength);


        private readonly Hook<OpenProcessDelegate> openProcessHook;
        private readonly Hook<CreateToolhelp32SnapshotDelegate> snapshotHook;

        public HideMeister()
        {
            var address = LocalHook.GetProcAddress("kernel32.dll", "OpenProcess");
            openProcessHook = new Hook<OpenProcessDelegate>(address, new OpenProcessDelegate(MyOpenProcess), this);
            openProcessHook.Activate();

            address = LocalHook.GetProcAddress("kernel32.dll", "CreateToolhelp32Snapshot");
            snapshotHook = new Hook<CreateToolhelp32SnapshotDelegate>(address, new CreateToolhelp32SnapshotDelegate(MyCreateTool), this);
            snapshotHook.Activate();
            
            address = LocalHook.GetProcAddress("ntdll.dll", "NtQueryInformationProcess");
            var getInfo = Marshal.GetDelegateForFunctionPointer<NtQueryInformationProcessDelegate>(address);

            var process = Process.GetCurrentProcess();
            var returnLength = 0;
            var pbi = new ProcessBasicInformation();

            var status = getInfo(process.Handle, 0, ref pbi, pbi.Size, ref returnLength);
            if (status == 0)
            {
                client.SendMessage($"PEB address: {pbi.PebBaseAddress}");
                //var peb = Marshal.PtrToStructure<ProcessBasicInformation>((IntPtr) pbi.PebBaseAddress);
            }
            else
            {
                client.SendMessage($"Error during NtQuery.. : {status}");
            }
        }

        private IntPtr MyOpenProcess(uint processAccess, bool bInheritHandle, int processId)
        {
            //client.SendMessage($"Scanned by OpenProcess: {processId}");
            return IntPtr.Zero;
        }

        private IntPtr MyCreateTool(uint flags, uint thProcess)
        {
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            openProcessHook?.Dispose();
            snapshotHook?.Dispose();
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessBasicInformation
        {
            public int ExitStatus;
            public int PebBaseAddress;
            public int AffinityMask;
            public int BasePriority;
            public int UniqueProcessId;
            public int InheritedFromUniqueProcessId;
            public uint Size => (6 * sizeof(int));
        };

        [StructLayout(LayoutKind.Explicit)]
        private struct ProcessEnviromentBlock
        {
            [FieldOffset(0)] public uint[] buff;
            [FieldOffset(12)] public IntPtr ldr;
        }
    }
}
