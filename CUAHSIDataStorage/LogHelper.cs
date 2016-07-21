﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using CUAHSI.Common;

namespace CUAHSIDataStorage
{
    public class LogHelper
    {
        /// <summary>
        /// Configuration setting of connection string to storage account responsible for state information generated by users in the process of servicing application features, e.g. data exports.
        /// </summary>
        public static string cuahsiApplicationDataStore = "CUAHSIDataExport";

        /// <summary>
        /// Confinguration setting of connection string to storage account responsible for usage logs and other forms of platform metrics.
        /// </summary>
        public static string cuahsiLogStore = "cuahsiservicelogs";

        /// <summary>
        /// Abstraction for providing access to the cuahsidata cloud storage account. Uses emulator storage in debug mode.
        /// </summary>
        /// <returns></returns>
        public static CloudStorageAccount GetCUAHSIDataStorage()
        { 
#if (DEBUG)
            //return CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue(cuahsiApplicationDataStore));
            return CloudStorageAccount.DevelopmentStorageAccount;
            
#else 
            return CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue(cuahsiApplicationDataStore));
#endif
        }

        public static CloudStorageAccount GetCUAHSILogsStorageAccount()
        {
#if (DEBUG)
            //return CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue(cuahsiLogStore));
            return CloudStorageAccount.DevelopmentStorageAccount;

#else 
            return CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue(cuahsiLogStore));
#endif
        }
        
        public static string DescendingRowKey(DateTime d)
        {
            return (DateTime.MaxValue.Ticks - d.Ticks).ToString();
        }

        public static void LogGetAsyncDataValuesException(string serviceURL, string siteCode, string variableCode, DateTime startTime, DateTime endTime, Exception e)
        {
            CloudStorageAccount csa = GetCUAHSILogsStorageAccount();
            CloudTableClient tableClient = csa.CreateCloudTableClient();
            CloudTable errorTable = tableClient.GetTableReference(DiscoveryStorageTableNames.GetValuesException);

            DynamicTableEntity er = new DynamicTableEntity(DiscoveryStorageTableNames.GetValuesException, LogHelper.DescendingRowKey(DateTime.UtcNow));
            er.Properties.Add("message", new EntityProperty(e.Message));
            if (e.InnerException != null)
            {
                er.Properties.Add("innerex", new EntityProperty(e.InnerException.ToString()));
            }
            
            er.Properties.Add("serviceURL", new EntityProperty(serviceURL));
            er.Properties.Add("siteCode", new EntityProperty(siteCode));
            er.Properties.Add("variableCode", new EntityProperty(variableCode));
            er.Properties.Add("startTime", new EntityProperty(startTime));
            er.Properties.Add("endTime", new EntityProperty(endTime));
            
            TableOperation logerror = TableOperation.Insert(er);
            errorTable.BeginExecute(logerror, null, null);
            // errorTable.BeginExecute(logerror, null, null); // store without tracking callback (fire and forget (application layer not network layer))
        }

        /// <summary>
        /// Persist information for tracking new session initializations, with server-generated timestamp
        /// </summary>
        /// <param name="sessionID">ID to be assigned to all subsequent requests</param>        
        /// <param name="ipaddr">IP address of the user</param>
        public static void LogNewAPIUse(string sessionID)
        {
            CloudStorageAccount csa = GetCUAHSILogsStorageAccount();
            CloudTableClient tableClient = csa.CreateCloudTableClient();
            CloudTable logTbl = tableClient.GetTableReference(DiscoveryStorageTableNames.SessionStart);

            DynamicTableEntity log = new DynamicTableEntity(DiscoveryStorageTableNames.SessionStart, LogHelper.DescendingRowKey(DateTime.UtcNow));            
            log.Properties.Add("sessionID", new EntityProperty(sessionID));

            TableOperation logerror = TableOperation.Insert(log);
            logTbl.BeginExecute(logerror, null, null);
        }

        /// <summary>
        /// Fire-and-forget logging of new sessions to nosql storage
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="uri"></param>
        public static void LogAPIUse(string sessionID, string uri)
        {
            CloudStorageAccount csa = GetCUAHSILogsStorageAccount();
            CloudTableClient tableClient = csa.CreateCloudTableClient();
            CloudTable logTbl = tableClient.GetTableReference(DiscoveryStorageTableNames.SessionData);

            DynamicTableEntity log = new DynamicTableEntity(sessionID, LogHelper.DescendingRowKey(DateTime.UtcNow));
            log.Properties.Add("uri", new EntityProperty(uri));

            TableOperation logerror = TableOperation.Insert(log);
            logTbl.BeginExecute(logerror, null, null);
        }
    }
}