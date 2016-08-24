using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace _1CUpdatesDownloader
{
    public class Conf1CUpdateSettings
    {
        static public char VersionSeparator = '.';
        static public char DirectoryVersionSeparator = '_';
        static public string UserAgent = "1C+Enterprise/8.3";
        static public string UpdatesInfoDownloadPath = "http://downloads.1c.ru/ipp/ITSREPV/V8Update/Configs/";
        static public string TemplatesDownloadPath = "http://downloads.v8.1c.ru/tmplts/";
        static public string UpdatesInfoZIPFileName = "v8upd11.zip";
        static public string UpdatesInfoXMLFileName = "v8cscdsc.xml";

        public string ConfName;
        public string ConfDescription;
        public string ConfUpdateServerPath;
        public string DownloadDirectory;
        public string Users1CLogin;
        public string Users1CPassword;
    }

    public class Settings
    {
        [XmlElement]
        public string DownloadDirectory;
        [XmlElement]
        public string TemplatesDirectory;
        [XmlElement]
        public bool SyncTemplatesWithDownloads;
        [XmlArray("Confs1C"), XmlArrayItem("Conf1CUpdateSettings")]
        public List<Conf1CUpdateSettings> Confs1C;

        public Settings()
        {
            Confs1C = new List<Conf1CUpdateSettings>();
        }
    }

    public static class AppSettings
    {
        public static Settings settings = new Settings();

        public static void LoadSettings()
        {
            string settingsFilePath = Path.Combine(Environment.CurrentDirectory, "settings.xml");
            if (!File.Exists(settingsFilePath))
            {
                CreateTemplateSettingsFile(settingsFilePath);
                throw new Exception("Создан файл настроек. Заполните настройки и запустите заново!");
            }

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Settings));
            using (FileStream xmlStream = new FileStream(settingsFilePath, FileMode.Open))
            {
                settings = (Settings)xmlSerializer.Deserialize(xmlStream);
            }
        }

        public static void CheckSettings()
        {
            if (!Directory.Exists(settings.DownloadDirectory))
                throw new Exception($"Указан несуществующий каталог для скачивания обновлений: {settings.DownloadDirectory}!");

            if (settings.Confs1C.Count == 0)
                throw new Exception($"Отсутствует описание конфигураций!");
        }

        private static void CreateTemplateSettingsFile(string settingsFilePath)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Settings));

            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
            xmlWriterSettings.Indent = true;
            xmlWriterSettings.IndentChars = ("\t");
            xmlWriterSettings.OmitXmlDeclaration = true;

            Settings templateSettings = new Settings
            {
                DownloadDirectory = "Download directory",
                TemplatesDirectory = "Templates directory",
                SyncTemplatesWithDownloads = true
            };
            templateSettings.Confs1C.Add(new Conf1CUpdateSettings {
                ConfName = "Configuration name (БухгалтерияПредприятия)",
                ConfDescription = "Configuration description (Бухгалтерия предприятия 2.0)",
                ConfUpdateServerPath = "Configuration server path (Accounting/20/83//)",
                DownloadDirectory = "Download directory (Accounting_2_0)",
                Users1CLogin = "Login (downloads.v8.1c.ru)",
                Users1CPassword = "Password (downloads.v8.1c.ru)"
            });

            using (XmlWriter xmlWriter = XmlWriter.Create(settingsFilePath, xmlWriterSettings))
            {
                xmlSerializer.Serialize(xmlWriter, templateSettings);
            }
        }
    }
}
