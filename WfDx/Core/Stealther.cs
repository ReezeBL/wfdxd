using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EasyHook;

/*
Library: EasyHook64.dll
Library: EasyLoad64.dll
Library: MSCOREE.DLL
Library: mscoreei.dll
Library: clr.dll
Library: MSVCR120_CLR0400.dll
Library: mscorlib.ni.dll
Library: api-ms-win-core-xstate-l2-1-0.dll
Library: clrjit.dll
Library: nlssorting.dll
Library: Accessibility.ni.dll
Library: System.Runtime.Remoting.ni.dll
Library: System.Core.ni.dll
Library: System.Configuration.ni.dll
Library: System.Xml.ni.dll
Library: System.Runtime.ni.dll
Library: System.Drawing.ni.dll
Library: System.Windows.Forms.ni.dll
Library: System.Threading.ni.dll
Library: System.Collections.ni.dll
Library: System.Runtime.InteropServices.ni.dll
Library: System.Reflection.ni.dll
 * */

namespace WfDx.Core
{
    //TODO: Link unlinked
    internal class Stealther
    {
        private readonly ServerInterface client = EntryPoint.Server;
        private delegate IntPtr OpenProcessDelegate(uint processAccess, bool bInheritHandle, int processId);
        private delegate IntPtr CreateToolhelp32SnapshotDelegate(uint flags, uint th32ProcessId);

        private delegate bool EnumProcessModulesDelegate(IntPtr hProcess,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U4)] [In] [Out] uint[] lphModule, uint cb,
            [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded);

        private delegate int NtQueryInformationProcessDelegate(
            IntPtr processHandle, int processInformationClass, ref ProcessBasicInformation processInformation,
            uint processInformationLength, ref int returnLength);


        private readonly Hook<OpenProcessDelegate> openProcessHook;
        private readonly Hook<CreateToolhelp32SnapshotDelegate> snapshotHook;
        private readonly Hook<EnumProcessModulesDelegate> enumProcessModulesHook;

        private readonly HashSet<string> dllsToUnlink = new HashSet<string>
        {
            "EasyHook64.dll",
            "EasyLoad64.dll",
            //"clr.dll",
            //"MSVCR120_CLR0400.dll"
        };
        private readonly List<UnloadedEntry> unlinkedEntries = new List<UnloadedEntry>();

        public Stealther()
        {
            var address = LocalHook.GetProcAddress("kernel32.dll", "OpenProcess");
            openProcessHook = new Hook<OpenProcessDelegate>(address, new OpenProcessDelegate(MyOpenProcess), this);
            openProcessHook.Activate();

            address = LocalHook.GetProcAddress("kernel32.dll", "CreateToolhelp32Snapshot");
            snapshotHook = new Hook<CreateToolhelp32SnapshotDelegate>(address, new CreateToolhelp32SnapshotDelegate(MyCreateTool), this);
            snapshotHook.Activate();

            address = LocalHook.GetProcAddress("psapi.dll", "EnumProcessModules");
            enumProcessModulesHook = new Hook<EnumProcessModulesDelegate>(address, new EnumProcessModulesDelegate(MyEnumProcessModules), this);
            enumProcessModulesHook.Activate();

            UnlinkNessesaryModules();
        }

        private void RestoreUnlinkedModules()
        {
            foreach (var unlinkedEntry in unlinkedEntries)
            {
                RestoreModule(unlinkedEntry.origin, unlinkedEntry.baseAddres);
            }
            unlinkedEntries.Clear();
        }

        private void UnlinkNessesaryModules()
        {
            var address = LocalHook.GetProcAddress("ntdll.dll", "NtQueryInformationProcess");
            var getInfo = Marshal.GetDelegateForFunctionPointer<NtQueryInformationProcessDelegate>(address);

            var process = Process.GetCurrentProcess();
            var returnLength = 0;
            var pbi = new ProcessBasicInformation();

            var status = getInfo(process.Handle, 0, ref pbi, (uint)Marshal.SizeOf(pbi), ref returnLength);
            if (status == 0)
            {
                client.DebugMessage($"PEB address: 0x{pbi.pebAddress.ToString("X")}");
                var peb = Marshal.PtrToStructure<ProcessEnviromentBlock>(pbi.pebAddress);
                client.DebugMessage($"LDR address: 0x{peb.ldr.ToString("X")}");

                var ldr = Marshal.PtrToStructure<PebLdrData>(peb.ldr);
                var head = ldr.InLoadOrder.next;
                var tmp = Marshal.PtrToStructure<LdrModule>(head);
                while (tmp.InLoadOrderList.next != head)
                {
                    var libName = tmp.BaseDllName.GetString();
                    if (dllsToUnlink.Contains(libName) || libName.EndsWith(".ni.dll"))
                    {
                        UnlinkModule(tmp.InLoadOrderList);
                        UnlinkModule(tmp.InMemoryOrderList);
                        UnlinkModule(tmp.InInitialisationOrderList);
                    }
                    else
                        client.DebugMessage($"Library: {libName}");
                    tmp = Marshal.PtrToStructure<LdrModule>(tmp.InLoadOrderList.next);
                }

            }
            else
            {
                client.SendMessage($"Error during NtQuery.. : 0x{(uint)status:X}");
            }
        }

        private void RestoreModule(ListEntry module, IntPtr baseAddress)
        {
            var prevAddr = module.prev;
            var prev = Marshal.PtrToStructure<ListEntry>(prevAddr);
            var nextAddr = prev.next;
            var next = Marshal.PtrToStructure<ListEntry>(nextAddr);
            module.next = prev.next;
            prev.next = baseAddress;
            next.prev = baseAddress;
            Marshal.StructureToPtr(module, baseAddress, true);
            Marshal.StructureToPtr(prev, prevAddr, true);
            Marshal.StructureToPtr(next, nextAddr, true);
        }

        private void UnlinkModule(ListEntry module)
        {
            var prevAddr = module.prev;
            var nextAddr = module.next;
            var prev = Marshal.PtrToStructure<ListEntry>(prevAddr);
            var next = Marshal.PtrToStructure<ListEntry>(nextAddr);
            var baseAddres = prev.next;

            unlinkedEntries.Add(new UnloadedEntry{baseAddres = baseAddres, origin = module});

            prev.next = module.next;
            next.prev = module.prev;
            Marshal.StructureToPtr(prev, prevAddr, true);
            Marshal.StructureToPtr(next, nextAddr, true);
        }

        private bool MyEnumProcessModules(IntPtr hProcess,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U4)] [In] [Out] uint[] lphModule, uint cb,
            [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded)
        {
            client.SendMessage("Scanned memory modules!");
            return enumProcessModulesHook.Origin(hProcess, lphModule, cb, out lpcbNeeded);
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
            client.SendMessage("Disposing stealther");
            RestoreUnlinkedModules();

            openProcessHook?.Dispose();
            snapshotHook?.Dispose();
            enumProcessModulesHook?.Dispose();
        }

        #region Structs
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessBasicInformation
        {
            public IntPtr reserved1;
            public IntPtr pebAddress;
            public IntPtr reserved2;
            public IntPtr reserved3;
            public IntPtr proccessId;
            public IntPtr reserved4;
        };

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessEnviromentBlock
        {
            public short reserved1;
            public byte beingDebugged;
            public byte reserved2;
            public IntPtr reserved3;
            public IntPtr reserved4;
            public IntPtr ldr;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PebLdrData
        {
            public long reserved1;
            public IntPtr reserved2;
            public ListEntry InLoadOrder;
            public ListEntry InMemoryOrder;
            public ListEntry InInitORder;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ListEntry
        {
            public IntPtr next;
            public IntPtr prev;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct UnicodeString
        {
            public ushort length;
            public ushort maxLength;
            public IntPtr buffer;

            public string GetString() => Marshal.PtrToStringUni(buffer, length/2);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LdrModule
        {
            public ListEntry InLoadOrderList;
            public ListEntry InMemoryOrderList;
            public ListEntry InInitialisationOrderList;
            public IntPtr BaseAddress;
            public IntPtr EntryPoint;
            public ulong SizeOfImage;
            public UnicodeString FullDllName;
            public UnicodeString BaseDllName;
        }

        private struct UnloadedEntry
        {
            public ListEntry origin;
            public IntPtr baseAddres;
        }

#endregion
    }
}
