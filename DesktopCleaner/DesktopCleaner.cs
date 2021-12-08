using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Timers;

namespace DesktopCleaner
{
    public partial class DesktopCleaner : ServiceBase
    {
        EventLog eventLog;
        readonly string DESKTOP_PATH = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public DesktopCleaner(string[] args)
        {
            InitializeComponent();

            //var builder = new ConfigurationBuilder()
            //                   .SetBasePath(Directory.GetCurrentDirectory())
            //                   .AddJsonFile("Parameters.json", optional: true, reloadOnChange: true);


            eventLog = new System.Diagnostics.EventLog();

            if (!System.Diagnostics.EventLog.SourceExists("DesktopCleaner"))
            {
                System.Diagnostics.EventLog.CreateEventSource("DesktopCleaner", "");
            }
            eventLog.Source = "DesktopCleaner";
        }

        internal void TestStartupAndStop(string[] args)
        {
            this.OnStart(args);
            Console.ReadLine();
            this.OnStop();
        }

        protected override void OnStart(string[] args)
        {
            eventLog.WriteEntry("DesktopCleaner Service Started");

            EnableLastAccessTrackingInWindows();

            var timer = new Timer();
            timer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
            timer.Elapsed += new ElapsedEventHandler(TimerFired);
            timer.Start();
            TimerFired(null, null);
        }

        /// <summary>
        /// Sets the disableLastAccess windows property to 0 to force windows to always track file last access
        /// The change will take effect after a windows restart.
        /// Be careful what you wish for because it might have an effect(very minor if any) on windows performance since it has to keep track of accessed files at all time. 
        /// </summary>
        private void EnableLastAccessTrackingInWindows()
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C fsutil behavior set disablelastaccess 0"; // Must restart for it to take effect
            process.StartInfo = startInfo;
            process.Start();
        }

        public void TimerFired(object sender, ElapsedEventArgs args)
        {
            eventLog.WriteEntry("DesktopCleaner Service Checking");

            ArchiveFiles();

            ArchiveDirectories();
        }


        /// <summary>
        /// It will archive any file that hasn't been accessed for a month of more
        /// </summary>
        private void ArchiveFiles()
        {
            var desktopDirectoryInfo = new DirectoryInfo(this.DESKTOP_PATH);

            if (desktopDirectoryInfo.Exists)
            {
                var excludedExtenstions = new List<string>() { ".lnk", ".url" };

                var desktopFilesInfos = desktopDirectoryInfo.GetFiles().ToList()
                    .Where(f => !excludedExtenstions.Contains(f.Extension.ToLower()))
                    .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .Where(f => f.LastAccessTime <= DateTime.Now.AddMonths(-1))
                    .OrderBy(f => f.LastAccessTime).ToList();

                if (desktopFilesInfos.Count > 0)
                {
                    foreach (var fileInfo in desktopFilesInfos)
                    {
                        Console.WriteLine($"{fileInfo.LastAccessTime} :                  {fileInfo.Name}");

                        var destination = Directory.CreateDirectory(desktopDirectoryInfo.FullName + "\\Archived_Desktop");
                        File.Move(fileInfo.FullName, destination.FullName + "\\" + fileInfo.Name);
                    }
                }
            }
        }


        /// <summary>
        /// It will archive any folder that hasn't been written to for a month or more. (written to instead of accessed, becasue I think anti virus access all folders daily)
        /// </summary>
        private void ArchiveDirectories()
        {
            var desktopDirectoryInfo = new DirectoryInfo(this.DESKTOP_PATH);

            if (desktopDirectoryInfo.Exists)
            {

                var desktopDirectoriesInfos = desktopDirectoryInfo.GetDirectories().ToList()
                .Where(d => d.LastWriteTime <= DateTime.Now.AddMonths(-1))
                .OrderBy(d => d.LastAccessTime).ToList();

                if (desktopDirectoriesInfos.Count > 0)
                {
                    foreach (var dirInfo in desktopDirectoriesInfos)
                    {
                        Console.WriteLine($"{dirInfo.LastAccessTime} :                  {dirInfo.Name}");

                        var destination = Directory.CreateDirectory(desktopDirectoryInfo.FullName + "\\Archived_Desktop");
                        Directory.Move(dirInfo.FullName, destination.FullName + "\\" + dirInfo.Name);
                    }
                }
            }
        }

        protected override void OnStop()
        {
            eventLog.WriteEntry("DesktopCleaner Service Started");
        }
    }
}
