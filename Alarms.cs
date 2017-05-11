using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace Utility
{
    public class Alarms
    {
        #region private members
        private const int RECOVER_MOMS_ALARM = 0;
        private const int THROW_MOMS_ALARM = 1;
        private string conString;

        private static Alarms _alarmsInstance;
        private static object _lockObject = new object();

        private ConcurrentDictionary<string, Tuple<int, bool>> _alarmStates = new ConcurrentDictionary<string, Tuple<int, bool>>();

        private string _alarmLC32Message = "lc32_ms_moms";
        private string _alarmRecipient = "moms";

        #endregion

        #region constructor

        private Alarms()
        {
            conString = Parameters.ConString;
        }

        #endregion

        #region public members
        public static Alarms Instance
        {
            get
            {
                lock (_lockObject)
                {
                    if (_alarmsInstance == null)
                    {
                        _alarmsInstance = new Alarms();
                        _alarmsInstance.readAlarms();
                    }
                    return _alarmsInstance;
                }
            }
        }
        #endregion

        #region public methods
        public void AddAlarm(string alarm, int alarmID)
        {
            if (alarm == null)
            {
                throw new ArgumentNullException("alarm");
            }
            if (alarmID < 0)
            {
                throw new ArgumentOutOfRangeException("alarmID");
            }

            createAlarm(alarm, new Tuple<int, bool>(alarmID, false));
        }

        public bool ThrowAlarm(string alarm)
        {
            Tuple<int, bool> alarmInfo;

            if (_alarmStates.TryGetValue(alarm, out alarmInfo))
            {
                if (LC32.Send(_alarmRecipient, _alarmLC32Message, THROW_MOMS_ALARM, alarmInfo.Item1))
                {
                    if (_alarmStates.TryUpdate(alarm, new Tuple<int, bool>(alarmInfo.Item1, true), alarmInfo))
                    {
                        if (updateDatabaseAlarm(alarm, new Tuple<int, bool>(alarmInfo.Item1, true)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool RecoverAlarm(string alarm)
        {
            Tuple<int, bool> alarmInfo;

            if (_alarmStates.TryGetValue(alarm, out alarmInfo))
            {
                if (LC32.Send(_alarmRecipient, _alarmLC32Message, RECOVER_MOMS_ALARM, alarmInfo.Item1))
                {
                    if (_alarmStates.TryUpdate(alarm, new Tuple<int, bool>(alarmInfo.Item1, false), alarmInfo))
                    {
                        if (updateDatabaseAlarm(alarm, new Tuple<int, bool>(alarmInfo.Item1, false)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool CheckAlarm(string alarm, out bool state)
        {
            Tuple<int, bool> alarmInfo;
            try
            {
                if (_alarmStates.TryGetValue(alarm, out alarmInfo))
                {
                    state = alarmInfo.Item2;
                    return true;
                }
                else
                {
                    state = false;
                    return false;
                }
            }
            catch (ArgumentNullException ex)
            {
                ExceptionHandler.HandleError(ex);
                state = false;
                return false;
            }
        }

        #endregion

        #region private methods

        private void createAlarm(string alarm, Tuple<int, bool> alarmInfo)
        {
            if (!_alarmStates.ContainsKey(alarm))
            {
                while (!_alarmStates.TryAdd(alarm, alarmInfo))
                {
                    Task.Delay(100);
                    Task.Yield();
                }
                Logger.Instance.Log("Creating new Alarm: alarm=" + alarm + " alarmID=" + alarmInfo.Item1.ToString() + " alarmState=" + alarmInfo.Item2.ToString());
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private void readAlarms()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(conString))
                {
                    using (SqlCommand cmd = new SqlCommand(Parameters.ReadAlarm, con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Connection.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    createAlarm(reader["Alarm"].ToString(), new Tuple<int, bool>(int.Parse(reader["AlarmID"].ToString()), bool.Parse((reader["AlarmState"].ToString()))));
                                }
                            }
                        }

                        cmd.Connection.Close();
                    }
                }
            }
            catch (SqlException ex)
            {
                ExceptionHandler.HandleError(ex);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        private bool updateDatabaseAlarm(string alarm, Tuple<int, bool> alarmInfo)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(conString))
                {
                    using (SqlCommand cmd = new SqlCommand(Parameters.UpdateAlarm, con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add("AlarmID", SqlDbType.Int).Value = alarmInfo.Item1;
                        cmd.Parameters.Add("Alarm", SqlDbType.VarChar, 50).Value = alarm;
                        cmd.Parameters.Add("AlarmState", SqlDbType.Bit).Value = (alarmInfo.Item2 ? 1 : 0);

                        cmd.Connection.Open();
                        cmd.ExecuteNonQuery();
                        cmd.Connection.Close();
                    }
                }
                return true;
            }
            catch (SqlException ex)
            {
                ExceptionHandler.HandleError(ex);
                return false;
            }
        }
        #endregion
    }
}
