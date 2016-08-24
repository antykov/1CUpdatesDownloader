using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace _1CUpdatesDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;

            try
            {
                AppSettings.LoadSettings();
                AppSettings.CheckSettings();
            }
            catch (Exception E)
            {
                Common.LogException(E);
                Environment.Exit(1);
                return;
            }

            List<Conf1CClass> confs1C = new List<Conf1CClass>();
            foreach (var conf1CSettings in AppSettings.settings.Confs1C)
            {
                try
                {
                    Conf1CClass conf1C = new Conf1CClass(conf1CSettings);
                    conf1C.UpdateConf();

                    confs1C.Add(conf1C);
                }
                catch { }
            }

            if (confs1C.Count > 0)
                Platform1CClass.UpdatePlatform(confs1C[0].ConfSettings, confs1C[0].CorrectExistingVersion);
        }

    }
}
