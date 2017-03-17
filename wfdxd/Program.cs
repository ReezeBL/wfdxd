using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Threading;
using EasyHook;
using WfDx;

namespace wfdxd
{
    public class Program
    {
        private static ServerInterface client;
        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            Process process = null;
            while (process == null)
            {
                process = Process.GetProcessesByName("Warframe.x64").FirstOrDefault();
            }

            string channelName = null;
            string libraryPath = "WfDx.dll";

            RemoteHooking.IpcCreateServer<ServerInterface>(ref channelName, WellKnownObjectMode.Singleton);
            client = RemoteHooking.IpcConnectClient<ServerInterface>(channelName);
            try
            {
                RemoteHooking.Inject(process.Id, libraryPath, libraryPath, channelName);
                Console.ReadLine();
                client.RunLibrary = false;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Something goes wrong {e}");
            }
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            if(client != null)
                client.RunLibrary = false;
            Thread.Sleep(1000);
        }
    }
}
