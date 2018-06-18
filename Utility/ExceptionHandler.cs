using System;
using System.Reflection;
using System.Diagnostics;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading;

namespace Utility
{
    public static class ExceptionHandler
    {
        private static string AppName
        {
            get
            {
                return Path.GetFileNameWithoutExtension(typeof(ExceptionHandler).Assembly.Location);
            }
        }

        private static string AssemblyVersion
        {
            get
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return String.Format("Version: {0}.{1}.{2:D4}", v.Major, v.Minor, v.Revision);
            }
        }

        public static void HandleError(Exception ex)
        {
            try
            {
                string msg = String.Format("An error has occurred, see exception table for more info: {0}Type: {1}{0}Message: {2}{0}Stack Trace: {3}", Environment.NewLine, ex.GetType().ToString(), ex.Message, ex.StackTrace);

                if (ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
                {
                    Logger.Instance.Enabled = false;
                }
                else
                {
                    Logger.Instance.Log(msg);
                }
                InsException(ex);
            }
            catch (SqlException)
            {
                Logger.Instance.Log("************ Error inserting exception. ************");
            }
            catch (FormatException e)
            {
                InsException(e);
            }
            catch (ArgumentNullException e)
            {
                InsException(e);
            }
        }

        private static void InsException(Exception ex)
        {
            string SP = "up_lane_InsExceptions";
            string conStr = Parameters.ConString;

            using (SqlConnection con = new SqlConnection(conStr))
            {
                using (SqlCommand cmd = new SqlCommand(SP, con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    //Input Params
                    cmd.Parameters.Add("@ExceptionDT", SqlDbType.DateTime).Value = DateTime.Now;
                    cmd.Parameters.Add("@MachineName", SqlDbType.VarChar, 50).Value = Environment.MachineName;
                    cmd.Parameters.Add("@Source", SqlDbType.VarChar, 50).Value = AppName;
                    cmd.Parameters.Add("@Method", SqlDbType.VarChar, 50).Value = ex.TargetSite.Name;
                    cmd.Parameters.Add("@Type", SqlDbType.VarChar, 50).Value = ex.GetType().FullName;
                    cmd.Parameters.Add("@BaseType", SqlDbType.VarChar, 50).Value = ex.GetType().BaseType.FullName;
                    cmd.Parameters.Add("@Message", SqlDbType.VarChar, 256).Value = ex.Message;
                    cmd.Parameters.Add("@StackTrace", SqlDbType.VarChar, 512).Value = ex.StackTrace.ToString();
                    cmd.Parameters.Add("@CallStack", SqlDbType.VarChar, 512).Value = Environment.StackTrace.ToString();
                    cmd.Parameters.Add("@AppDomain", SqlDbType.VarChar, 256).Value = AppName;
                    cmd.Parameters.Add("@AssemblyName", SqlDbType.VarChar, 50).Value = Assembly.GetExecutingAssembly().FullName;
                    cmd.Parameters.Add("@AssemblyVersion", SqlDbType.VarChar, 50).Value = AssemblyVersion;
                    cmd.Parameters.Add("@ThreadID", SqlDbType.Int).Value = Thread.CurrentThread.ManagedThreadId;
                    cmd.Parameters.Add("@ThreadUser", SqlDbType.VarChar, 50).Value = Thread.CurrentPrincipal.Identity.Name;
                    cmd.Parameters.Add("@CLRVersion", SqlDbType.Char, 10).Value = Environment.Version.ToString();
                    cmd.Parameters.Add("@OSVersion", SqlDbType.VarChar, 50).Value = Environment.OSVersion.ToString();
                    cmd.Parameters.Add("@CurrentDirectory", SqlDbType.VarChar, 50).Value = Environment.CurrentDirectory.ToString();
                    cmd.Parameters.Add("@UserName", SqlDbType.VarChar, 50).Value = Environment.UserName.ToString();
                    cmd.Parameters.Add("@HelpLink", SqlDbType.VarChar, 256).Value = (ex.HelpLink == null || ex.HelpLink.Length == 0) ? " " : ex.HelpLink;
                    cmd.Parameters.Add("@Additional", SqlDbType.VarChar, 256).Value = " ";
                    cmd.Parameters.Add("@SqleSource", SqlDbType.VarChar, 256).Value = " ";
                    cmd.Parameters.Add("@SqleNumber", SqlDbType.Int).Value = 0;
                    cmd.Parameters.Add("@SqleState", SqlDbType.TinyInt).Value = 0;
                    cmd.Parameters.Add("@SqleClass", SqlDbType.TinyInt).Value = 0;
                    cmd.Parameters.Add("@SqleServer", SqlDbType.VarChar, 50).Value = " ";
                    cmd.Parameters.Add("@SqleMessage", SqlDbType.VarChar, 256).Value = " ";
                    cmd.Parameters.Add("@SqleProcedure", SqlDbType.VarChar, 50).Value = " ";
                    cmd.Parameters.Add("@SqleLineNumber", SqlDbType.Int).Value = 0;

                    if (ex.Source == ".Net SqlClient Data Provider")
                    {
                        SqlException sqlex = ex as SqlException;
                        cmd.Parameters["@SqleSource"].Value = sqlex.Errors[0].Source;
                        cmd.Parameters["@SqleNumber"].Value = sqlex.Errors[0].Number;
                        cmd.Parameters["@SqleState"].Value = sqlex.Errors[0].State;
                        cmd.Parameters["@SqleClass"].Value = sqlex.Errors[0].Class;
                        cmd.Parameters["@SqleServer"].Value = sqlex.Errors[0].Server;
                        cmd.Parameters["@SqleMessage"].Value = sqlex.Errors[0].Message;
                        cmd.Parameters["@SqleProcedure"].Value = sqlex.Errors[0].Procedure;
                        cmd.Parameters["@SqleLineNumber"].Value = sqlex.Errors[0].LineNumber;
                    }

                    cmd.Connection.Open();
                    cmd.ExecuteNonQuery();
                    cmd.Connection.Close();
                }
            }
            string errors = "";
            if (ex.Source == ".Net SqlClient Data Provider")
            {
                SqlException sqlEx = ex as SqlException;
                for (int i = 0; i < sqlEx.Errors.Count; i++)
                {
                    errors += String.Format("Index #{1}{0}Message: {2}{0}LineNumber: {3}{0}Source: {4}{0}Procedure: {5}{0}Class: {6}{0}Number: {7}{0}Server: {8}{0}State: {9}{0}Source: {10}{0}{0}",
                        Environment.NewLine, i,
                        sqlEx.Errors[i].Message,
                        sqlEx.Errors[i].LineNumber,
                        sqlEx.Errors[i].Source,
                        sqlEx.Errors[i].Procedure,
                        sqlEx.Errors[i].Class,
                        sqlEx.Errors[i].Number,
                        sqlEx.Errors[i].Server,
                        sqlEx.Errors[i].State,
                        sqlEx.Errors[i].Source);
                }

                Debug.WriteLine(errors);
            }
        }
    } // ErrorHandler
}
