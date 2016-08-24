using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace _1CUpdatesDownloader
{

    class Platform1CClass
    {
        static private int lastDownloadPercent;

        public static void UpdatePlatform(Conf1CUpdateSettings confSettings, string lastConfVersion)
        {
            Common.Log($"--------------------------------------------------------------------------------", ConsoleColor.Yellow);
            Common.Log($"Получение версии последней платформы...");

            try
            {
                UpdatePlatformReference.PlatformUpdateInfoRequest platformInfoRequest = new UpdatePlatformReference.PlatformUpdateInfoRequest();
                platformInfoRequest.configurationName = confSettings.ConfName;
                platformInfoRequest.versionNumber = lastConfVersion;

                platformInfoRequest.additionalParameters = new UpdatePlatformReference.MapElements[1];
                UpdatePlatformReference.MapElements param = new UpdatePlatformReference.MapElements();
                param.key = "PlatformVersion";
                param.value = "";
                platformInfoRequest.additionalParameters.SetValue(param, 0);

                UpdatePlatformReference.UpdatePlatformApiClient client = new UpdatePlatformReference.UpdatePlatformApiClient();

                UpdatePlatformReference.PlatformUpdateInfoResult platformInfoResult = client.getPlatformUpdateInfo(platformInfoRequest);
                if (platformInfoResult == null)
                {
                    Common.Log($"Не удалось получить информацию об обновлении платформы для конфигурации {confSettings.ConfName} {lastConfVersion}", ConsoleColor.Red);
                    return;
                }
                if (platformInfoResult.errorMessage != "")
                {
                    Common.Log($"Сервер обновлений вернул ошибку {platformInfoResult.errorName} {platformInfoResult.errorMessage}", ConsoleColor.Red);
                    return;
                }
                if (!Common.CheckPlatformUpdateNecessity(AppSettings.settings.DownloadDirectory, platformInfoResult.platformVersion))
                {
                    Common.Log($"Последняя версия платформы ({platformInfoResult.platformVersion}) уже получена.", ConsoleColor.Green);
                    return;
                }

                Common.Log($"Получение новой версии платформы: {platformInfoResult.platformVersion}...");

                UpdatePlatformReference.PlatformUpdateRequest platformRequest = new UpdatePlatformReference.PlatformUpdateRequest();
                platformRequest.login = confSettings.Users1CLogin;
                platformRequest.password = confSettings.Users1CPassword;
                platformRequest.distributionUin = platformInfoResult.distributionUin;

                UpdatePlatformReference.PlatformUpdateResult platformResult = client.getPlatformUpdate(platformRequest);
                if (platformResult == null)
                {
                    Common.Log($"Не удалось получить ссылку для получения обновления платформы!", ConsoleColor.Red);
                    return;
                }
                if (platformResult.errorMessage != "")
                {
                    Common.Log($"Сервер обновлений вернул ошибку {platformResult.errorName} {platformResult.errorMessage}", ConsoleColor.Red);
                    return;
                }

                using (WebClient wc = new WebClient())
                {
                    lastDownloadPercent = 0;
                    string downloadPath = Path.Combine(AppSettings.settings.DownloadDirectory, platformInfoResult.platformVersion);
                    if (!Directory.Exists(downloadPath))
                        Directory.CreateDirectory(downloadPath);
                    string downloadFileName = Path.Combine(downloadPath, Path.GetFileName(platformResult.downloadUrl));

                    wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                    wc.DownloadDataCompleted += Wc_DownloadDataCompleted;
                    wc.Credentials = new NetworkCredential(confSettings.Users1CLogin, confSettings.Users1CPassword);
                    wc.DownloadFileTaskAsync(new Uri(platformResult.downloadUrl), downloadFileName).Wait();

                    Common.Log("\nПолучение файла с обновлением платформы успешно завершено");

                    Common.Log("Разархивирование полученного обновления платформы...");
                    Common.ExtractArchiveToDirectory(downloadFileName, downloadPath);
                    File.Delete(downloadFileName);

                    Common.Log($"Получение обновления платформы {platformInfoResult.platformVersion} успешно завершено!", ConsoleColor.Green);
                }
            }
            catch (Exception E)
            {
                Common.LogException(E);
            }
        }

        private static void Wc_DownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            lastDownloadPercent = 100;
        }

        private static void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (lastDownloadPercent >= e.ProgressPercentage)
                return;

            lastDownloadPercent = e.ProgressPercentage;

            Common.Log($"--> Получение файла {lastDownloadPercent}% ({e.BytesReceived} байт из {e.TotalBytesToReceive})", ConsoleColor.White, false);
        }
    }
}
