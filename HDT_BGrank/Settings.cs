using System;
using System.IO;
using Hearthstone_Deck_Tracker;

namespace HDT_BGrank
{
    public class Settings
    {
        public bool ifLoad = false;
        public double scaleRatio = 1.0;
        public double positionLeft = 0.0;
        public double positionTop = 0.0;
        private static Settings _settings;
        
        public static Settings Instance
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new Settings();
                }
                return _settings;
            }
        }

        public static void Load()
        {
            string path = Path.Combine(Config.AppDataPath, "BGrank", "BGrankSettings.xml");
            if (File.Exists(path))
            {
                try
                {
                    _settings = XmlManager<Settings>.Load(path);
                }
                catch (Exception ex)
                {
                    FileLogger.Instance.Error("Failed to load settings", ex);
                }
            }
        }

        public static void Save()
        {
            string path = Path.Combine(Config.AppDataPath, "BGrank", "BGrankSettings.xml");
            try
            {
                XmlManager<Settings>.Save(path, Instance);
            }
            catch (Exception ex)
            {
                FileLogger.Instance.Error("Failed to save settings", ex);
            }
        }
    }
}
