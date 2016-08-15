using SharpCompress.Common;
using SharpCompress.Reader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace _1CUpdatesDownloader
{
    static class Common
    {
        [DllImport("KERNEL32.DLL", EntryPoint = "GetPrivateProfileStringW",
           SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true,
           CallingConvention = CallingConvention.StdCall)]
        private static extern int GetPrivateProfileString(
              string lpAppName,
              string lpKeyName,
              string lpDefault,
              string lpReturnString,
              int nSize,
              string lpFilename);

        public static long GetVersionAsLong(string version)
        {
            string[] split = version.Split(Conf1CUpdateSettings.VersionSeparator);
            long lVersion = 0, lVersionPart = 0;
            for (int i = 0; i < split.Length; i++)
                if (Int64.TryParse(split[i], out lVersionPart))
                    lVersion += lVersionPart * (long)Math.Pow(10, 4 * (split.Length - i - 1));

            return lVersion;
        }

        public static bool CheckPlatformUpdateNecessity(string dir, string newVersion)
        {
            string dirNewVersion = Path.Combine(dir, newVersion);
            if (!Directory.Exists(dirNewVersion))
                return true;

            try
            {
                string setupIniPath = Path.Combine(dirNewVersion, "setup.ini");
                if (!File.Exists(setupIniPath))
                {
                    Directory.Delete(dirNewVersion, true);
                    return true;
                }

                string returnString = new string(' ', 1024);
                GetPrivateProfileString("Startup", "ProductVersion", "", returnString, 1024, setupIniPath);
                if (returnString.Split('\0')[0] != newVersion)
                {
                    Directory.Delete(dirNewVersion, true);
                    return true;
                }

                return false;
            }
            catch (Exception E)
            {
                LogException(E, "Не удалось проверить необходимость обновления платформы");
                return false;
            }
        }

        public static void ExtractArchiveToDirectory(string archive, string destination)
        {
            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            using (FileStream fls = new FileStream(archive, FileMode.Open, FileAccess.Read))
            {
                IReader reader = ReaderFactory.Open(fls);
                if (null != reader)
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory)
                        {
                            string path = Path.Combine(destination, RemovePathInvalidChars(reader.Entry.FilePath));
                            new FileInfo(path).Directory.Create();
                            using (FileStream fls_out = new FileStream(path, FileMode.Create, FileAccess.Write))
                            {
                                reader.WriteEntryTo(fls_out);
                            }
                        }
                    }
                }
            }
        }

        public static string RemovePathInvalidChars(string path)
        {
            string result = path;

            char[] invalidChars = Path.GetInvalidPathChars();
            foreach (var c in invalidChars)
            {
                result = result.Replace(c.ToString(), "");
            }

            return result;
        }

        public static void LogException(Exception E, string info = "")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if (info.Trim().Length == 0)
                Console.WriteLine(E.Message);
            else
                Console.WriteLine($"{info}: {E.Message}");
            Exception inner = E.InnerException;
            while (inner != null)
            {
                Console.WriteLine($"    --> {inner.Message}");
                inner = inner.InnerException;
            }
        }

        public static void Log(string message, ConsoleColor color = ConsoleColor.White, bool newLine = true, int emptyLineLength = 0)
        {
            Console.ForegroundColor = color;
            if (emptyLineLength > 0)
                Console.Write($"\r{new String(' ', emptyLineLength)}");
            if (newLine)
                Console.WriteLine(message);
            else
                Console.Write($"\r{message}");
            if (emptyLineLength > 0)
                Console.Write($"\r");
        }
    }

}