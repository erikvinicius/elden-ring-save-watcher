using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace EldenSaveWatcher
{
    internal class Program
    {
        private static readonly string workPath = string.Format("{0}\\Roaming\\EldenRing", Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).FullName);
        private static DateTime lastIteration;
        private static bool isDebugMode = false;
        private static int totalSaves = 0;
        private const string consoleTitle = "Elden Ring Save Watcher 1.0";

        static void Main(string[] args)
        {            
            Console.Title = consoleTitle;

            Console.WriteLine(consoleTitle);

            Console.WriteLine("Searching for saves...");

            if (!Directory.Exists(workPath))
            {
                Console.WriteLine("No saves found.");
                return;
            }

            Console.WriteLine("Found path: {0}", workPath);

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            //Process.Start("explorer.exe", workPath);

            InitWatchers();
            InitLineCommands();

            Process.GetCurrentProcess().WaitForExit();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine("Process exited. restarting...");
        }

        private static void OnErrorFileOrDirectory(object sender, ErrorEventArgs e)
        {
            Console.WriteLine(e);
        }

        private static void OnChangeFileOrDirectory(object sender, FileSystemEventArgs e)
        {
            LogReceivedEvent(e, isDebugMode);

            DateTime now = DateTime.Now;

            int compare = DateTime.Compare(now, lastIteration);

            bool inTimeout = compare < 0;

            if (inTimeout)
            {
                //Console.WriteLine("In Timeout now:{0} lastIteration:{1}", now, lastIteration);
                return;
            }

            Console.WriteLine("\nNew save detected");

            lastIteration = DateTime.Now.AddSeconds(10); 

            Task.Delay(3000).ContinueWith((_) => {
                Save(workPath);
            });

            IncrementTotalSaves();
        }

        private static void IncrementTotalSaves()
        {
            totalSaves++;

            Console.Title = string.Format("{0} | SavesCount: {1}", consoleTitle, totalSaves);
        }

        private static Task InitLineCommands()
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    ConsoleKeyInfo consoleKeyInfo = Console.ReadKey();

                    switch (consoleKeyInfo.Key)
                    {
                        case ConsoleKey.C:
                            Console.Clear();
                            Console.WriteLine(consoleTitle);
                            Console.WriteLine("Found path: {0}", workPath);
                            Console.WriteLine("Waiting for saves...");
                            Console.WriteLine("Waiting commands. press h for show commands");
                            break;
                        case ConsoleKey.S:
                            Console.Write("\nEnter the save file name (empty to auto generate): ");
                            string filename = Console.ReadLine();

                            Task.Run(() =>
                            {
                                Save(workPath, filename, "snapshot");
                                IncrementTotalSaves();
                                Console.WriteLine("Saved snapshot");
                            });
                            break;
                        case ConsoleKey.H:
                            StringBuilder stringBuilder = new StringBuilder();
                            stringBuilder.AppendLine("\nC - clear terminal");
                            stringBuilder.AppendLine("S - to save snapshot");
                            stringBuilder.AppendLine("H - to show all commands");

                            Console.WriteLine(stringBuilder.ToString());
                            break;
                        default:
                            Console.WriteLine("\nInvalid key", consoleKeyInfo.Key);
                            break;
                    }
                }
            });
        }

        private static Task InitWatchers()
        {
            return Task.Run(() =>
            {
                string[] filters = { "*.sl2", "*.co2", "*.bak" }; // elden ring save file types
                List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();

                foreach (string filter in filters)
                {
                    FileSystemWatcher watcher = new FileSystemWatcher
                    {
                        IncludeSubdirectories = true,
                        Path = workPath,
                        EnableRaisingEvents = true,
                        Filter = filter
                    };

                    watcher.Changed += OnChangeFileOrDirectory;
                    watcher.Created += OnChangeFileOrDirectory;
                    watcher.Renamed += OnChangeFileOrDirectory;
                    watcher.Deleted += OnChangeFileOrDirectory;
                    watcher.Error += OnErrorFileOrDirectory;

                    watchers.Add(watcher);
                }

                Console.WriteLine("Waiting for saves...");
            });
        }


        private static void Save(string fromDir, string filename = null, string toDir = "saves")
        {
            string currentDir = Directory.GetCurrentDirectory();
            string tempDir = $"{currentDir}\\temp";
            string fileDir = $"{currentDir}\\{toDir}";
            string timestamp = DateTime.Now.ToFileTime().ToString();

            if (string.IsNullOrEmpty(filename))
            {
                filename = timestamp;
            }

            string fullpath = $"{fileDir}\\{filename}.zip";

            Console.WriteLine("Will verify if filename exists...");

            if(File.Exists(fullpath))
            {
                Console.WriteLine("Filename already exists, adding timestamp on name");
                fullpath = $"{fileDir}\\{filename}-{timestamp}.zip";
            }

            Console.WriteLine("Cleaning...");

            Utils.DeleteFolder(tempDir);

            Console.WriteLine("Copying elden ring save path...");

            Utils.CopyDirectory(fromDir, tempDir);

            Directory.CreateDirectory(fileDir);

            Console.WriteLine("Writting new save...");

            ZipFile.CreateFromDirectory(tempDir, fullpath);

            Utils.DeleteFolder(tempDir);

            Console.WriteLine("Saved. Path: {0}", fullpath);
        }

        private static void LogReceivedEvent(FileSystemEventArgs e, bool debug = false)
        {
            if (!debug) return;

            Console.WriteLine("\n======================== EVENT ========================");
            Console.WriteLine("File: {0}", e.Name);
            Console.WriteLine("Type: {0}", e.ChangeType);
            Console.WriteLine("FullPath: {0}", e.FullPath);
            Console.WriteLine("======================== END EVENT ========================");
        }
    }
}
