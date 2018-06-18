using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Utility
{
    public class LC32 : IDisposable
    {
        #region private classes
        private static class NativeMethods
        {
            private const string CommonDLL30 = "CommonDLL30.dll";
            private const string User32 = "User32.dll";
            private const string Kernel32 = "Kernel32.dll";

            // Windows functions for registering messages and mailboxes
            [DllImport(User32, CharSet = CharSet.Unicode)]
            public static extern int RegisterWindowMessage(String strMessageName);

            //[DllImport(Kernel32, CharSet = CharSet.Unicode)]
            //public static extern IntPtr CreateMailslot(String slotName, int maxMessageSize, int readTimeout, IntPtr securityAttributes);

            //[DllImport(Kernel32)]
            //public static extern IntPtr GetMailslotInfo(IntPtr mailSlot, IntPtr messageSize, IntPtr nextSize, IntPtr messageCount, IntPtr readTimeout);

            //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Interoperability", "CA1415:DeclarePInvokesCorrectly", Justification = "Not worth switching entire project to compile with /unsafe for a single method import to follow correct style.")]
            //[DllImport(Kernel32)]
            //[return: MarshalAs(UnmanagedType.Bool)]
            //public static extern bool ReadFile(IntPtr hFile, out byte[] buffer, IntPtr msgSize, out uint bytesRead, IntPtr overlaped);

            //[DllImport(Kernel32)]
            //public static extern int CloseHandle(int handle);

            //InTrans implimentation for passing data between programs
            //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "4"), DllImport(CommonDLL30, EntryPoint = "_ReadTheSlot@20")]
            //public static extern int ReadTheSlot(ref int MsgID, ref int AndID, ref int RptID, ref int SubFunc, StringBuilder theData);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "0"), DllImport(CommonDLL30, EntryPoint = "_RegisterHandle@8")]
            public static extern int RegisterHandle(string myModuleName, int myProcessHandle);

            //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "0"), DllImport(CommonDLL30, EntryPoint = "_CreateTheSlot@4")]
            //public static extern int CreateTheSlot(string myModuleName);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "0"), DllImport(CommonDLL30, EntryPoint = "_GetModuleId@4")]
            public static extern int GetModuleID(string moduleName);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "0"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "5"), DllImport(CommonDLL30, EntryPoint = "_WriteTheSlot@24")]
            public static extern int WriteTheSlot(string moduleName, int msgID, int ansID, int rptID, int subFunc, string theData);

            //[DllImport(CommonDLL30, EntryPoint = "_DeleteTheSlot@0")]
            //public static extern long DeleteTheSlot();

            [DllImport(CommonDLL30, EntryPoint = "_SendMsg@24")]
            public static extern int SendMsg(int targetProcessID, int msgID, int ansID, int rptID, int subFunc, int lParam);
        }

        //private static class MailSlot
        //{
        //    public static int MsgID;
        //    public static int AnsID;
        //    public static int RptID;
        //    public static int SubFunc;
        //    public static string Data;

        //    public static bool Refresh()
        //    {
        //        clear();
        //        StringBuilder data = new StringBuilder(Data);
        //        int result = NativeMethods.ReadTheSlot(ref MsgID, ref AnsID, ref RptID, ref SubFunc, data);
        //        Data = data.ToString().Trim((char)0);
        //        return result > 0;
        //    }

        //    private static void clear()
        //    {
        //        MsgID = AnsID = RptID = SubFunc = -1;
        //        Data = "".PadRight(2048, (char)0);
        //    }
        //}

        #endregion

        private static string SELF;
        private static int ID_SELF;
        private static int handle;

        private static Cache<string, int> _idCache = new Cache<string, int>();
        private static ImmutableDictionary<int, MessageReceived> _messageHandlers = ImmutableDictionary.Create<int, MessageReceived>();

        private static bool _disposed = false;

        public delegate void MessageReceived(Message m);

        public static bool Init()
        {
            SELF = Parameters.LC32Self;
            handle = Parameters.Handle;
            if (NativeMethods.RegisterHandle(SELF, handle) == 0)
            {
                return false;
            }

            ID_SELF = NativeMethods.GetModuleID(SELF);
            Logger.Instance.Log(string.Format("LC32: Retrieved Module ID \"{0}\", {1}", SELF, ID_SELF));

            return true;
        }

        public static bool IsLC32Message(Message message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            return _idCache.ContainsValue(message.Msg);
        }

        public static void RegisterLC32Message(string lc32Message, MessageReceived msgRec)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            _messageHandlers = _messageHandlers.Add(_idCache.GetOrAdd(lc32Message, NativeMethods.RegisterWindowMessage), msgRec);
        }

        public static void HandleLC32Message(Message lc32Message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            MessageReceived messageReceivedDelegate;

            if (_messageHandlers.TryGetValue(lc32Message.Msg, out messageReceivedDelegate))
            {
                messageReceivedDelegate(lc32Message);
            }
        }

        public static bool Send(string target, string message, int subFunc, int data)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            int targetID = _idCache.GetOrAdd(target, NativeMethods.GetModuleID);
            int messageID = _idCache.GetOrAdd(message, NativeMethods.RegisterWindowMessage);

            if (NativeMethods.SendMsg(targetID, messageID, ID_SELF, ID_SELF, subFunc, data) != 0)
            {
                return false;
            }
            return true;
        }

        public static bool Send(string target, string message, int data)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            return Send(target, message, 0, data);
        }

        public static bool Send(string target, string message)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            return Send(target, message, 0, 0);
        }

        public static bool SendMailSlot(string slot, string message, int subFunc, string data)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            int messageID;
            int response;

            try
            {
                messageID = _idCache.GetOrAdd(message, NativeMethods.RegisterWindowMessage);
            }
            catch (ArgumentNullException ex)
            {
                ExceptionHandler.HandleError(ex);
                return false;
            }

            response = NativeMethods.WriteTheSlot(slot, messageID, ID_SELF, ID_SELF, subFunc, data);

            if (response > 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public static bool SendMailSlot(string slot, string message, string data)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("LC32");
            }

            return SendMailSlot(slot, message, 0, data);
        }

        public static bool testc (string slot,string message,string data)
        {
            int messageID = _idCache.GetOrAdd(message, NativeMethods.RegisterWindowMessage);
            NativeMethods.WriteTheSlot(slot, messageID, ID_SELF, ID_SELF, 0, data);

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    //Dispose managed resources
                }

                //Dispose unmanaged resources

                _idCache.Clear();

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }// LC32 Class
}
