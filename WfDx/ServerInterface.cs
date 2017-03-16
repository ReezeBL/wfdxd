using System;

namespace WfDx
{
    public class ServerInterface : MarshalByRefObject
    {
        public bool RunLibrary = true;
        public void IsInstalled(int clientPid)
        {
            Console.WriteLine($"Succesfully injected in {clientPid}");
        }

        public void SendMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void ReportException(Exception e)
        {
            Console.WriteLine($"An error has occured: {e}");
        }

        public void Ping()
        {
            
        }

        public void DebugMessage(string message)
        {
#if DEBUG
            Console.WriteLine(message);
#endif
        }
    }
}
