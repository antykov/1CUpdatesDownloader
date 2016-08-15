using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;

namespace _1CUpdatesDownloader
{
    class ConfUpdateInfo
    {
        public string Version { get; set; }
        public long VersionAsLong { get; set; }
        public string DirectoryVersion { get; set; }
        public string FileURL { get; set; }

        public ConfUpdateInfo(string version, string fileURL = "")
        {
            this.Version = version;
            this.VersionAsLong = Common.GetVersionAsLong(version);
            this.DirectoryVersion = version.Replace(Conf1CUpdateSettings.VersionSeparator, Conf1CUpdateSettings.DirectoryVersionSeparator);
            this.FileURL = fileURL;
        }
    }

    class Conf1CClass
    {
        int lastDownloadPercent;
        long totalBytesToReceive;

        string fullDownloadDirectory;

        public Conf1CUpdateSettings ConfSettings;
        public SortedList<long, SortedList<long, ConfUpdateInfo>> AllUpdatesInfoByTarget;
        public SortedList<long, ConfUpdateInfo> AllUpdatesInfo;
        public SortedList<long, ConfUpdateInfo> ExistingUpdatesInfo;

        public string LastExistingVersion
        {
            get
            {
                if (ExistingUpdatesInfo.Count == 0)
                    return "";
                else
                    return ExistingUpdatesInfo.ElementAt(ExistingUpdatesInfo.Count - 1).Value.Version;
            }
        }
        public long LastExistingVersionAsLong
        {
            get
            {
                if (ExistingUpdatesInfo.Count == 0)
                    return 0;
                else
                    return ExistingUpdatesInfo.ElementAt(ExistingUpdatesInfo.Count - 1).Key;
            }
        }

        public Conf1CClass(Conf1CUpdateSettings conf1CSettings)
        {
            this.ConfSettings = conf1CSettings;

            this.fullDownloadDirectory = Path.Combine(AppSettings.settings.DownloadDirectory, conf1CSettings.DownloadDirectory);
            if (!Directory.Exists(this.fullDownloadDirectory))
                Directory.CreateDirectory(this.fullDownloadDirectory);

            Common.Log($"--------------------------------------------------------------------------------", ConsoleColor.Yellow);

            FillExistingUpdatesInfo();
            FillAllUpdatesInfo();

            RestoreExistingUpdatesSequence();
        }

        bool IsCorrectUpdate(string dir, string ver)
        {
            List<string> fileNames = new List<string>();
            fileNames.Add("UpdInfo.txt");
            fileNames.Add("VerInfo.txt");
            fileNames.Add("1cv8.mft");

            string substring = "";
            foreach (var fileName in fileNames)
            {
                if (File.Exists(Path.Combine(dir, fileName)))
                {
                    if (fileName == "VerInfo.txt")
                        substring = ver;
                    else
                        substring = $"Version={ver}";
                    if (File.ReadAllText(Path.Combine(dir, fileName)).IndexOf(substring) != -1)
                        return true;
                }

            }

            return false;
        }

        void FillExistingUpdatesInfo()
        {
            ExistingUpdatesInfo = new SortedList<long, ConfUpdateInfo>();

            Common.Log($"Получение списка загруженных обновлений конфигурации {ConfSettings.ConfDescription}...");

            DirectoryInfo dirInfo = new DirectoryInfo(fullDownloadDirectory);
            try
            {
                List<string> directoriesForDelete = new List<string>();
                foreach (var dir in dirInfo.EnumerateDirectories("* * * *".Replace(' ', Conf1CUpdateSettings.DirectoryVersionSeparator)))
                {
                    string version = dir.Name.Replace(Conf1CUpdateSettings.DirectoryVersionSeparator, Conf1CUpdateSettings.VersionSeparator);
                    if (IsCorrectUpdate(dir.FullName, version))
                        ExistingUpdatesInfo.Add(Common.GetVersionAsLong(version), new ConfUpdateInfo(version));
                    else
                        directoriesForDelete.Add(dir.FullName);
                }

                foreach (var dir in directoriesForDelete)
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception E)
            {
                Common.LogException(E);
                throw new Exception();
            }

        }

        void FillAllUpdatesInfo()
        {
            AllUpdatesInfoByTarget = new SortedList<long, SortedList<long, ConfUpdateInfo>>();
            AllUpdatesInfo = new SortedList<long, ConfUpdateInfo>();

            Common.Log($"Получение списка обновлений с сервера 1С для конфигурации {ConfSettings.ConfDescription}...");

            try
            {
                string tempPath = Path.GetTempPath();
                string tempUpdatesInfoZIPFileName = Path.Combine(tempPath, Conf1CUpdateSettings.UpdatesInfoZIPFileName);
                string tempUpdatesInfoXMLFileName = Path.Combine(tempPath, Conf1CUpdateSettings.UpdatesInfoXMLFileName);
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(Conf1CUpdateSettings.UpdatesInfoDownloadPath + ConfSettings.ConfUpdateServerPath + Conf1CUpdateSettings.UpdatesInfoZIPFileName, tempUpdatesInfoZIPFileName);
                    if (File.Exists(tempUpdatesInfoXMLFileName))
                        File.Delete(tempUpdatesInfoXMLFileName);
                    Common.ExtractArchiveToDirectory(tempUpdatesInfoZIPFileName, tempPath);
                    if (!File.Exists(tempUpdatesInfoXMLFileName))
                        throw new Exception($"После разархивирования {Conf1CUpdateSettings.UpdatesInfoZIPFileName} не удалось найти файл {Conf1CUpdateSettings.UpdatesInfoXMLFileName}!");
                }
                using (XmlReader xml = XmlReader.Create(tempUpdatesInfoXMLFileName))
                {
                    string elementName, elementContent;
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.Element && xml.Name == "v8u:update")
                        {
                            Hashtable updateValues = new Hashtable();
                            List<string> targetVersionsList = new List<string>();

                            while (xml.Read())
                            {
                                if (xml.NodeType == XmlNodeType.EndElement && xml.Name == "v8u:update")
                                    break;

                                if (xml.NodeType == XmlNodeType.Element)
                                {
                                    elementName = xml.Name;
                                    elementContent = xml.ReadElementContentAsString();

                                    if (elementName == "v8u:target")
                                        targetVersionsList.Add(elementContent);
                                    else
                                        updateValues[elementName] = elementContent;
                                }
                            }

                            if (updateValues["v8u:version"] == null || updateValues["v8u:file"] == null || targetVersionsList.Count == 0)
                                continue;

                            long updateInfoKey = Common.GetVersionAsLong((string)updateValues["v8u:version"]);

                            if (AllUpdatesInfo.IndexOfKey(updateInfoKey) != -1)
                                continue;

                            AllUpdatesInfo.Add(updateInfoKey, new ConfUpdateInfo((string)updateValues["v8u:version"], (string)updateValues["v8u:file"]));

                            foreach (var targetVersion in targetVersionsList)
                            {
                                ConfUpdateInfo updateInfo = new ConfUpdateInfo((string)updateValues["v8u:version"], (string)updateValues["v8u:file"]);
                                long targetKey = Common.GetVersionAsLong(targetVersion);
                                if (AllUpdatesInfoByTarget.IndexOfKey(targetKey) == -1)
                                    AllUpdatesInfoByTarget.Add(targetKey, new SortedList<long, ConfUpdateInfo>());
                                AllUpdatesInfoByTarget[targetKey].Add(updateInfo.VersionAsLong, updateInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception E)
            {
                Common.LogException(E);
                throw new Exception();
            }
        }

        void RestoreExistingUpdatesSequence()
        {
            if (ExistingUpdatesInfo.Count == 0 || AllUpdatesInfoByTarget.Count == 0)
                return;

            long correctVersion = ExistingUpdatesInfo.ElementAt(0).Key;
            int targetIndex;
            ConfUpdateInfo updInfo;
            SortedList<long, ConfUpdateInfo> targetValues;
            for (int i = 1; i < ExistingUpdatesInfo.Count; i++)
            {
                targetIndex = AllUpdatesInfoByTarget.IndexOfKey(correctVersion);
                if (targetIndex == -1)
                    break;
                targetValues = AllUpdatesInfoByTarget.ElementAt(targetIndex).Value;

                updInfo = ExistingUpdatesInfo.ElementAt(i).Value;

                if (targetValues.ElementAt(targetValues.Count - 1).Key != updInfo.VersionAsLong)
                    break;

                correctVersion = updInfo.VersionAsLong;
            }

            try
            {
                int firstIncorrectIndex = ExistingUpdatesInfo.IndexOfValue(ExistingUpdatesInfo[correctVersion]) + 1;
                int removeCount = ExistingUpdatesInfo.Count - firstIncorrectIndex;
                while (removeCount > 0)
                {
                    Common.Log($"Удаление обновления, нарушающего последовательность: {ExistingUpdatesInfo.ElementAt(firstIncorrectIndex).Value.Version}");
                    Directory.Delete(Path.Combine(fullDownloadDirectory, ExistingUpdatesInfo.ElementAt(firstIncorrectIndex).Value.DirectoryVersion), true);
                    ExistingUpdatesInfo.RemoveAt(firstIncorrectIndex);
                    removeCount--;
                }
            }
            catch (Exception E)
            {
                Common.LogException(E, $"Ошибка при восстановлении последовательности обновлений конфигурации {ConfSettings.ConfDescription}");
                throw new Exception();
            }
        }

        public void UpdateConf()
        {
            try
            {
                Common.Log($"Получение обновлений для конфигурации {ConfSettings.ConfDescription} ({LastExistingVersion})...");
                if (AllUpdatesInfoByTarget.Count == 0)
                {
                    Common.Log($"Список обновлений пуст! Возможно произошла ошибка при получении списка обновлений!!!", ConsoleColor.Red);
                    return;
                }

                long versionAsLong;
                if (LastExistingVersion == "")
                    versionAsLong = AllUpdatesInfoByTarget.ElementAt(0).Key;
                else
                    versionAsLong = LastExistingVersionAsLong;
                if (AllUpdatesInfo.ElementAt(AllUpdatesInfo.Count - 1).Key == versionAsLong)
                {
                    Common.Log($"Версия {LastExistingVersion} является актуальной!", ConsoleColor.Green);
                    return;
                }
                if (AllUpdatesInfoByTarget.IndexOfKey(versionAsLong) == -1)
                {
                    Common.Log($"Не удалось найти обновления для указанной конфигурации!!!", ConsoleColor.Red);
                    return;
                }

                List<ConfUpdateInfo> updatesSequence = new List<ConfUpdateInfo>();
                do
                {
                    ConfUpdateInfo updateInfo = AllUpdatesInfoByTarget[versionAsLong].ElementAt(AllUpdatesInfoByTarget[versionAsLong].Count - 1).Value;
                    updatesSequence.Add(updateInfo);

                    versionAsLong = updateInfo.VersionAsLong;
                } while (AllUpdatesInfoByTarget.IndexOfKey(versionAsLong) != -1);

                try
                {
                    foreach (var updateInfo in updatesSequence)
                    {
                        DownloadConfUpdate(updateInfo);
                    }
                }
                catch (Exception E)
                {
                    foreach (var updateInfo in updatesSequence)
                    {
                        try
                        {
                            if (Directory.Exists(Path.Combine(fullDownloadDirectory, updateInfo.DirectoryVersion)))
                                Directory.Delete(Path.Combine(fullDownloadDirectory, updateInfo.DirectoryVersion), true);

                        }
                        catch { }
                    }

                    throw E;
                }
            }
            catch (Exception E)
            {
                Common.LogException(E);
                throw new Exception();
            }
        }

        private void DownloadConfUpdate(ConfUpdateInfo updateInfo)
        {
            Common.Log($"Получение обновления конфигурации {updateInfo.Version}...");

            using (WebClient wc = new WebClient())
            {
                string dir = Path.Combine(fullDownloadDirectory, updateInfo.DirectoryVersion);
                string filePath = Path.Combine(dir, "1cv8.zip");

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                lastDownloadPercent = 0;
                totalBytesToReceive = 0;

                wc.Credentials = new NetworkCredential(ConfSettings.Users1CLogin, ConfSettings.Users1CPassword);
                wc.Headers.Add("Accept-Charset", "utf-8");
                wc.Headers.Add("user-agent", Conf1CUpdateSettings.UserAgent);
                wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                wc.DownloadFileTaskAsync(new Uri(Conf1CUpdateSettings.TemplatesDownloadPath + updateInfo.FileURL), filePath).Wait();

                Common.ExtractArchiveToDirectory(filePath, dir);

                if (Directory.Exists(AppSettings.settings.TemplatesDirectory))
                {
                    dir = Path.GetDirectoryName(Path.Combine(AppSettings.settings.TemplatesDirectory, updateInfo.FileURL));
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                    Common.ExtractArchiveToDirectory(filePath, dir);
                }

                File.Delete(filePath);
            }
        }

        private void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            Common.Log($"\rПолучение файла 100% ({totalBytesToReceive} байт из {totalBytesToReceive})", ConsoleColor.White);
        }

        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (lastDownloadPercent >= e.ProgressPercentage)
                return;

            lastDownloadPercent = e.ProgressPercentage;
            totalBytesToReceive = e.TotalBytesToReceive;

            Common.Log($"Получение файла {lastDownloadPercent}% ({e.BytesReceived} байт из {e.TotalBytesToReceive})", ConsoleColor.White, false);
        }
    }
}
