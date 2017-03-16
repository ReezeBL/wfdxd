using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WfDx.Core.Hooks
{
    internal abstract class DirectXHook
    {
        public abstract void InstallHook();

        public abstract void UninstallHook();

        protected IntPtr[] GetVTableAdresses(IntPtr pointer, int numberOfMethods)
        {
            var addresses = new List<IntPtr>();
            var vTable = Marshal.ReadIntPtr(pointer);
            for(var i = 0; i < numberOfMethods; i++)
                addresses.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size));

            return addresses.ToArray();
        }
    }
}
