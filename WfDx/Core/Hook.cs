using System;
using System.Runtime.InteropServices;
using EasyHook;



/*
48 83 EC 28 E8 37 0B 00 00
48 83 EC 28 E8 37 0B 00 00

    2EF3BB88

    13F330000
    141309000

    */
namespace WfDx.Core
{
    internal class Hook <T> : IDisposable where T: class
    {
        private readonly LocalHook localHook;
        private bool isActive;

        public T Origin { get; private set; }

        public Hook(IntPtr funcPtr, Delegate newFunc, object owner)
        {
            System.Diagnostics.Debug.Assert(typeof(Delegate).IsAssignableFrom(typeof(T)));
            Origin = Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
            localHook = LocalHook.Create(funcPtr, newFunc, owner);
        }

        public void Activate()
        {
            if (isActive)
                return;

            isActive = true;
            localHook.ThreadACL.SetExclusiveACL(new []{0});
        }

        public void Deactivate()
        {
            if (!isActive)
                return;

            isActive = false;
            localHook.ThreadACL.SetInclusiveACL(new[] {0});
        }

        public void Dispose()
        {
            localHook.Dispose();
        }
    }
}
