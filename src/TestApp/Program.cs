// Copyright (c) Martin Costello, 2012-2018. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace MartinCostello.SqlLocalDb
{
    /// <summary>
    /// An application that acts as a test harness for the <c>MartinCostello.SqlLocalDb</c> assembly.  This class cannot be inherited.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// The main entry point to the application.
        /// </summary>
        /// <param name="args">The command-line arguments passed to the application.</param>
        internal static void Main(string[] args)
        {
            PrintBanner();

            var options = new SqlLocalDbOptions()
            {
                AutomaticallyDeleteInstanceFiles = true,
                StopOptions = StopInstanceOptions.NoWait,
            };

            var loggerFactory = new LoggerFactory()
                .AddConsole(LogLevel.Debug);

            var localDB = new SqlLocalDbApi(options, loggerFactory);

            if (!localDB.IsLocalDBInstalled())
            {
                Console.WriteLine(SR.SqlLocalDbApi_NotInstalledFormat, Environment.MachineName);
                return;
            }

            if (args?.Length == 1 &&
                (string.Equals(args[0], "/deleteuserinstances", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(args[0], "--delete-user-instances", StringComparison.OrdinalIgnoreCase)))
            {
                localDB.DeleteUserInstances(deleteFiles: true);
            }

            IReadOnlyList<ISqlLocalDbVersionInfo> versions = localDB.GetVersions();

            Console.WriteLine(Strings.Program_VersionsListHeader);
            Console.WriteLine();

            foreach (ISqlLocalDbVersionInfo version in versions)
            {
                Console.WriteLine(version.Name);
            }

            Console.WriteLine();

            IReadOnlyList<ISqlLocalDbInstanceInfo> instances = localDB.GetInstances();

            Console.WriteLine(Strings.Program_InstancesListHeader);
            Console.WriteLine();

            foreach (ISqlLocalDbInstanceInfo instanceInfo in instances)
            {
                Console.WriteLine(instanceInfo.Name);
            }

            Console.WriteLine();

            string instanceName = Guid.NewGuid().ToString();

            ISqlLocalDbInstanceInfo instance = localDB.CreateInstance(instanceName);

            var manager = new SqlLocalDbInstanceManager(instance, localDB);
            manager.Start();

            try
            {
                if (IsCurrentUserAdmin())
                {
                    manager.Share(Guid.NewGuid().ToString());
                }

                try
                {
                    using (SqlConnection connection = manager.CreateConnection())
                    {
                        connection.Open();

                        try
                        {
                            using (SqlCommand command = new SqlCommand("create database [MyDatabase]", connection))
                            {
                                command.ExecuteNonQuery();
                            }

                            using (SqlCommand command = new SqlCommand("drop database [MyDatabase]", connection))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        finally
                        {
                            connection.Close();
                        }
                    }
                }
                finally
                {
                    if (IsCurrentUserAdmin())
                    {
                        manager.Unshare();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                manager.Stop();
                localDB.DeleteInstance(instance.Name);
            }

            Console.WriteLine();
            Console.Write(Strings.Program_ExitPrompt);
            Console.ReadKey();
        }

        /// <summary>
        /// Returns whether the current user is in the administrators group on the local machine.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the current user is in the administrators
        /// group on the local machine; otherwise <see langword="false"/>.
        /// </returns>
        private static bool IsCurrentUserAdmin()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        /// <summary>
        /// Prints a banner to the console containing assembly and operating system information.
        /// </summary>
        private static void PrintBanner()
        {
            Assembly assembly = typeof(SqlLocalDbApi).Assembly;
            AssemblyName assemblyName = assembly.GetName();

            Console.WriteLine(
                Strings.Program_BannerFormat,
                assemblyName.Name,
                assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright,
                assemblyName.Version,
                assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version,
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
                assembly.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration,
                Environment.UserDomainName,
                Environment.UserName,
                IsCurrentUserAdmin(),
                Environment.OSVersion,
                Environment.Is64BitOperatingSystem,
                Environment.Is64BitProcess);
        }
    }
}
