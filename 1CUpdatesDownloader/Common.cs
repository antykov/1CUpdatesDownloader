using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Linq;
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

        public static long GetVersionAsLong(string version, char? separator = null)
        {
            string[] split = version.Split((separator == null) ? Conf1CUpdateSettings.VersionSeparator : (char)separator);
            long lVersion = 0, lVersionPart = 0;
            for (int i = 0; i < split.Length; i++)
                if (Int64.TryParse(split[i], out lVersionPart))
                    lVersion += lVersionPart * (long)Math.Pow(10, 4 * (split.Length - i - 1));

            return lVersion;
        }

        public static bool CompareMajorMinorVersions(string version1, char separator1, string version2, char separator2)
        {
            return String.Join("", version1.Split(separator1).Take(2).ToArray<string>()) == String.Join("", version2.Split(separator2).Take(2).ToArray<string>());
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
            try
            {
                using (ZipInputStream s = new ZipInputStream(File.OpenRead(archive)))
                {
                    ZipEntry theEntry;
                    while ((theEntry = s.GetNextEntry()) != null)
                    {
                        string directoryName = Path.GetDirectoryName(RemovePathInvalidChars(theEntry.Name));

                        if (String.IsNullOrWhiteSpace(directoryName))
                            directoryName = destination;
                        else
                            directoryName = Path.Combine(destination, directoryName);

                        Directory.CreateDirectory(directoryName);

                        string fileName = Path.Combine(directoryName, Path.GetFileName(RemovePathInvalidChars(theEntry.Name)));
                        if (fileName != String.Empty)
                        {
                            using (FileStream streamWriter = File.Create(fileName))
                            {
                                int size = 2048;
                                byte[] data = new byte[2048];
                                while (true)
                                {
                                    size = s.Read(data, 0, data.Length);
                                    if (size > 0)
                                    {
                                        streamWriter.Write(data, 0, size);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                } catch (Exception e)
            {
                Common.LogException(e, $"Ошибка при разархивировании {archive} в {destination}");
            }
        }

        public static void CopyDirectoryRecursively(string source, string destination)
        {
            string destinationSubdir = Path.Combine(destination, Path.GetFileName(source));
            if (!Directory.Exists(destinationSubdir))
                Directory.CreateDirectory(destinationSubdir);

            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destinationSubdir, Path.GetFileName(file)), true);

            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectoryRecursively(dir, destinationSubdir);
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