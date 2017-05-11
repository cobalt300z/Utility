using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#if DEBUG
using System.Diagnostics;
#endif

namespace Utility
{
    public class Logger : IDisposable
    {

        #region private members
        private const int MAX_SIZE = 0x200000;
        private static readonly string _logDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private StreamWriter _logSW;

        private static Logger _logger;
        private static object _lockObject = new object();

        private bool _logEnabled = true;
        private Task _retry;
        private Task _writer;
        private CancellationTokenSource _cancelTokenSource;

        private ConcurrentQueue<string> _logQueue;

        private bool _disposed;

        #endregion

        #region public members

        public event EventHandler<LogEventArgs> LogUpdate;

        public bool Enabled
        {
            get
            {
                return _logEnabled;
            }
            set
            {
                _logEnabled = value;
            }
        }

        public static Logger Instance
        {
            get
            {
                lock (_lockObject)
                {
                    if (_logger == null)
                    {
                        _logger = new Logger();
                        _logger.Open();
                    }
                    return _logger;
                }
            }
        }

        #endregion

        #region constructor

        private Logger()
        {
            _disposed = false;

            Directory.CreateDirectory(_logDir + @"\Logs");
            _logQueue = new ConcurrentQueue<string>();

            _cancelTokenSource = new CancellationTokenSource();

            if (_writer == null || _writer.Status != TaskStatus.Running || _writer.Status != TaskStatus.WaitingToRun || _writer.Status != TaskStatus.WaitingForActivation)
            {
                _writer = Task.Factory.StartNew(() => writeLogs(_cancelTokenSource.Token), _cancelTokenSource.Token);
            }
        }

        #endregion

        #region finalizer

        ~Logger()
        {
            Dispose(false);
        }

        #endregion

        #region public Methods

        public void Open()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }

            try
            {
                string oldFile = "Not Found";

                if (File.Exists(_logDir + @"\LogFile.txt"))
                {
                    oldFile = String.Format(@"{0}\log_{1}.log", _logDir, DateTime.Now.ToString("yyMMdd-HHmmss"));
                    File.Move(_logDir + @"\LogFile.txt", oldFile);
                }

                this._logSW = File.CreateText(_logDir + @"\LogFile.txt");
                this._logSW.AutoFlush = true;
                this._logSW.WriteLine("*** New LogFile Created - Last LogFile: {0} ***", oldFile);
            }
            catch (IOException ioex)
            {
                ExceptionHandler.HandleError(ioex);
            }
        }

        public void Close()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }

            try
            {
                if (_logSW != null)
                {
                    _logSW.WriteLine("*** Closing Logfile ***");
                    _logSW.Flush();
                    _logSW.Dispose();
                }
            }
            catch (IOException ioex)
            {
                // Assuming something went really wrong trying to close the file so just destroy the handle
                if (_logSW != null)
                    _logSW.Dispose();
                ExceptionHandler.HandleError(ioex);
            }
        }

        public void Log(string logLine)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }

            // If logging is enabled, log, else wait for 5 minutes then renable logging to see if the problem went away.
            if (Enabled)
            {
                
#if DEBUG
                string logSentence = string.Empty;
                try
                {
                    StackFrame callStack = new StackFrame(1, true);
                    if (callStack != null)
                    {
                        logSentence = string.Format("[{0}] - {1} - {2}", DateTime.Now.ToString("yyyy/MM/dd - HH:mm:ss:ffff"), "File: " + callStack.GetFileName().Substring(callStack.GetFileName().LastIndexOf('\\') + 1) + " Line number: " + callStack.GetFileLineNumber(), logLine);
                    }
                    else
                    {
                        logSentence = logLine;
                    }
                }
                catch (NullReferenceException nullEx)
                {
                    logSentence = logLine;
                    ExceptionHandler.HandleError(nullEx);
                }
                catch (FormatException formatEx)
                {
                    logSentence = logLine;
                    ExceptionHandler.HandleError(formatEx);
                }

                Debug.WriteLine(logSentence);
                this._logQueue.Enqueue(logSentence);
                if (LogUpdate != null && Parameters.LogEventEnabled)
                {
                    LogUpdate(this, new LogEventArgs(logSentence));
                }
#else
                string logSentence = String.Format("[{0}] - {1}", DateTime.Now.ToString("yyyy/MM/dd - HH:mm:ss.ffff"), logLine);
                //WriteLine(String.Format("[{0}] - {1}", DateTime.Now.ToString("yyyy/MM/dd - HH:mm:ss"), message));
                this._logQueue.Enqueue(logSentence);
                if (LogUpdate != null && Parameters.LogEventEnabled)
                {
                    LogUpdate(this, new LogEventArgs(logSentence));
                }
#endif
            }
            else
            {
                if (this._retry == null || this._retry.Status != TaskStatus.Running || this._retry.Status != TaskStatus.WaitingToRun || this._retry.Status != TaskStatus.WaitingForActivation)
                {
                    this._retry = Task.Factory.StartNew(() =>
                    {
                        Thread.Sleep(300000);
                        this._logEnabled = true;
                    });
                }
            }
        }

        #endregion

        #region private methods

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        private bool writeLine(string line)
        {
            try
            {
                if (_logSW.BaseStream.Length > MAX_SIZE)
                {
                    Close();
                    Thread.Sleep(100);
                    Open();
                }
                _logSW.WriteLine(line);
                return true;
            }
            catch (IOException ioex)
            {
                //Either a problem writing to the stream or changing file, attempt to change file
                Close();
                Open();
                ExceptionHandler.HandleError(ioex);
                return false;
            }
        }

        private async void writeLogs(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                if (!this._logQueue.IsEmpty)
                {
                    string logLine = string.Empty;
                    while (this._logQueue.TryPeek(out logLine))
                    {
                        if (writeLine(logLine))
                        {
                            this._logQueue.TryDequeue(out logLine);
                        }
                    }
                }
                else
                {
                    await Task.Delay(100, cancelToken);
                }
            }
        }
        #endregion

        #region dispose

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    //Dispose managed resources
                    if (_writer != null && _writer.Status != TaskStatus.Canceled && _writer.Status != TaskStatus.Faulted && _writer.Status != TaskStatus.RanToCompletion)
                    {
                        _cancelTokenSource.Cancel();
                        //_writer.Wait();
                    }
                    _cancelTokenSource.Dispose();
                }

                //Dispose unmanaged resources
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    } // Logger
} // EtherDIO.Utility
