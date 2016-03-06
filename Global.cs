using System;
using System.IO;
using System.Linq;
using SFXChallenger.Library.Logger;

namespace TwistedFate
{
    internal class Global
    {
        public static string Prefix = "TF";
        public static string Name = "TwistedFate";
        public static ILogger Logger;
        public static string LogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Name + " - Logs");

        static Global()
        {
            Logger = new SimpleFileLogger(LogDir) { LogLevel = LogLevel.High };

            try
            {
                Directory.GetFiles(LogDir)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTime < DateTime.Now.AddDays(-7))
                    .ToList()
                    .ForEach(f => f.Delete());
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex));
            }
        }

        public class Reset
        {
            public static bool Enabled = true;
            public static DateTime MaxAge = new DateTime(2015, 10, 6);
        }
    }
}
