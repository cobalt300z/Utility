namespace Utility
{
    public class Parameters
    {
        private static string conString;
        private static string startupSP;
        private static string updateSP;
        private static string lc32Self;
        private static int handle;
        private static bool logEventEnabled;
        private static string readAlarm;
        private static string updateAlarm;
        public static string ConString
        {
            get
            {
                return conString;
            }
            set
            {
                conString = value;
            }

        }
        public static string StartupSP
        {
            get
            {
                return startupSP;
            }
            set
            {
                startupSP = value;
            }

        }
        public static string UpdateSP
        {
            get
            {
                return updateSP;
            }
            set
            {
                updateSP = value;
            }

        }
        public static string LC32Self
        {
            get
            {
                return lc32Self;
            }
            set
            {
                lc32Self = value;
            }
        }
        public static int Handle
        {
            get
            {
                return handle;
            }
            set
            {
                handle = value;
            }
        }
        public static bool LogEventEnabled
        {
            get
            {
                return logEventEnabled;
            }
            set
            {
                logEventEnabled = value;
            }

        }
        public static string ReadAlarm
        {
            get
            {
                return readAlarm;
            }
            set
            {
                readAlarm = value;
            }

        }
        public static string UpdateAlarm
        {
            get
            {
                return updateAlarm;
            }
            set
            {
                updateAlarm = value;
            }

        }
    }
}
