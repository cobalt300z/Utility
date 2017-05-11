using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Utility
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
    public class Settings : IDisposable
    {
        #region private members

        private static Settings _settingsInstance;
        private static object _lockObject = new object();

        private static readonly string conString = Parameters.ConString;
        private static readonly string SP_StartUp = Parameters.StartupSP;
        private static readonly string SP_UpdateSP = Parameters.UpdateSP;

        private CancellationTokenSource _cancelTokenSource;
        private Task _refresh;

        private bool _disposed;

        private Cache<string, string> _settingsCache;


        #endregion

        #region constructor

        private Settings()
        {
            this._disposed = false;

            _settingsCache = new Cache<string, string>();

            _cancelTokenSource = new CancellationTokenSource();
            _refresh = Task.Factory.StartNew(() => refreshSettings(_cancelTokenSource.Token), _cancelTokenSource.Token);
        }

        #endregion

        #region public members

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static Settings Instance
        {
            get
            {
                lock (_lockObject)
                {
                    if (_settingsInstance == null)
                    {
                        _settingsInstance = new Settings();
                    }
                    return _settingsInstance;
                }
            }
        }

        #endregion

        #region private methods

        //wakeup every 30 minutes and get new settings.
        private async void refreshSettings(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {

#if DEBUG
                // 5min for testing purposes
                await Task.Delay(300000, cancelToken);
#else
                await Task.Delay(1800000, cancelToken);
#endif
                _settingsCache.Clear();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "PlazaID"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "LaneID")]
        private string readSetting(string parameter)
        {
            try
            {
                string value = string.Empty;
                using (SqlConnection con = new SqlConnection(conString))
                {
                    using (SqlCommand cmd = new SqlCommand(SP_StartUp, con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add("@Parameter", SqlDbType.VarChar, 50).Value = parameter;
                        cmd.Parameters.Add("@Value", SqlDbType.VarChar, 500).Direction = ParameterDirection.Output;

                        cmd.Connection.Open();
                        cmd.ExecuteNonQuery();

                        value = cmd.Parameters["@Value"].Value.ToString();
                        Logger.Instance.Log(string.Format("Settings: Reading {0}, Received {1}", parameter, value));
                        cmd.Connection.Close();

                        return value;
                    }
                }
            }
            catch (SqlException ex)
            {
                ExceptionHandler.HandleError(ex);
                return string.Empty;
            }
        }

        #endregion

        #region public methods

        public string GetSetting(string key)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Settings");
            }

            try
            {
                return _settingsCache.GetOrAdd(key, readSetting);
            }
            catch (ArgumentNullException ex)
            {
                ExceptionHandler.HandleError(ex);
                return string.Empty;
            }
        }

        internal bool UpdateSetting(string key, string value)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Settings");
            }

            string newValue = string.Empty;

            // insert/update database
            try
            {
                using (SqlConnection con = new SqlConnection(conString))
                {
                    using (SqlCommand cmd = new SqlCommand(SP_UpdateSP, con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add("@Parameter", SqlDbType.VarChar, 50).Value = key;
                        cmd.Parameters.Add("@Value", SqlDbType.VarChar, 500).Value = value;

                        cmd.Connection.Open();
                        cmd.ExecuteNonQuery();

                        Logger.Instance.Log(string.Format("Settings: Saving {0}, with {1}", key, value));
                        cmd.Connection.Close();
                    }
                }
            }
            catch (SqlException ex)
            {
                ExceptionHandler.HandleError(ex);
                return false;
            }

            // retrieve into/from cache
            try
            {
                //Force refresh the cache
                _settingsCache.Clear();
                newValue = _settingsCache.GetOrAdd(key, readSetting);
            }
            catch (ArgumentNullException ex)
            {
                ExceptionHandler.HandleError(ex);
                return false;
            }

            // compare to ensure it stuck, return pass/fail
            return value.Equals(newValue);
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
                    if (_refresh.Status != TaskStatus.Canceled && _refresh.Status != TaskStatus.Faulted && _refresh.Status != TaskStatus.RanToCompletion)
                    {
                        _cancelTokenSource.Cancel();
                        _refresh.Wait();
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



    } // Settings
} // TreadleCounter
