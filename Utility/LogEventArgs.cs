using System;

namespace Utility
{
    public class LogEventArgs : EventArgs
    {
        string _logLine;


        public LogEventArgs(string logLine)
        {
            _logLine = logLine;
        }

        public string LogLine
        {
            get
            {
                return _logLine;
            }
        }
    }
}
