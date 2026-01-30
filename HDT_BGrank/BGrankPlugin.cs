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

        public string Name => "HDT_BGrank";

        public string Description => "A battlegrounds plugin shows opponents' MMR.";

        public string Author => "IBM5100";

        public Version Version => new Version(1, 3, 2);

        public string ButtonText => "Reset";

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
            MenuItem = new MenuItem
            {
                Header = "BGrank",
                IsCheckable = true
            };

            MenuItem.Checked += (sender, args) => CreateComponents();
            MenuItem.Unchecked += (sender, args) => RemoveComponents();
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
    }
}