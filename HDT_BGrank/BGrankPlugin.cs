using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Plugins;

namespace HDT_BGrank
{
    public class BGrankPlugin : IPlugin
    {
        public MenuItem MenuItem { get; private set; }
        private BGrank rank = null;
        private DateTime lastUpdate = DateTime.Now;

        public string Author
        {
            get { return "IBM5100"; }
        }

        public string ButtonText
        {
            get { return "Reset"; }
        }

        public string Description
        {
            get { return "A battleground plugin searchs opponents' MMR for you."; }
        }

        public string Name
        {
            get { return "HDT_BGrank"; }
        }

        public void OnButtonPress()
        {
            Settings.Reset();
            if (MenuItem.IsChecked) 
            {
                RemoveComponents(false); // Reset, do not save settings
                CreateComponents();
            }
        }

        public void OnLoad()
        {
            FileLogger.Initialize(LogLevel.Info, 5);
            CreateMenuItem();
            MenuItem.IsChecked = true;
        }

        public void OnUnload()
        {
            MenuItem.IsChecked = false;
            FileLogger.Instance.Clean();
        }

        public void OnUpdate()
        {
            if (rank != null) 
            {
                if ((DateTime.Now - lastUpdate).TotalSeconds >= 1) 
                {
                    lastUpdate = DateTime.Now;
                    rank.OnUpdate();
                }
            }
        }

        private void CreateMenuItem()
        {
            MenuItem = new MenuItem()
            {
                Header = "BGrank"
            };

            MenuItem.IsCheckable = true;

            MenuItem.Checked += (sender, args) =>
            {
                CreateComponents();
            };

            MenuItem.Unchecked += (sender, args) =>
            {
                RemoveComponents();
            };
        }

        private void CreateComponents()
        {
            rank ??= new BGrank();
        }

        private void RemoveComponents(bool save = true)
        {
            if (rank != null)
            {
                rank.Clean(save);
                rank = null;
            }
        }

        public Version Version
        {
            get { return new Version(1, 3, 2); }
        }

    }
}