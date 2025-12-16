using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;

namespace HDT_BGrank
{
    public class BGrankPlugin : IPlugin
    {
        public MenuItem MenuItem { get; private set; }
        private BGrank rank = null;
        private LeaderBoardPanel leaderBoardPanel = null;
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
            RemoveComponents();

            Settings.Instance.scaleRatio = 1.0;
            Settings.Instance.positionLeft = 0.0;
            Settings.Instance.positionTop = 0.0;
            Settings.Instance.ifLoad = false;
            Settings.Save();

            CreateComponents();
        }

        public void OnLoad()
        {
            FileLogger.Initialize(LogLevel.Info, 5);
            CreateMenuItem();
            MenuItem.IsChecked = true;
        }

        public void OnUnload()
        {
            try { FileLogger.Instance.Dispose(); }
            catch { /* ignore Dispose errors */ }
            MenuItem.IsChecked = false;
        }

        public void OnUpdate()
        {
            if (rank != null && leaderBoardPanel != null) 
            {
                if ((DateTime.Now - lastUpdate).TotalSeconds >= 1) 
                {
                    rank.OnUpdate();
                    leaderBoardPanel.OnUpdate(rank);
                    lastUpdate = DateTime.Now;
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
            if (rank == null)
            {
                rank = new BGrank();
                GameEvents.OnGameStart.Add(rank.OnGameStart);
                GameEvents.OnTurnStart.Add(rank.OnTurnStart);
            }
            if (leaderBoardPanel == null)
            {
                leaderBoardPanel = new LeaderBoardPanel();
                Core.OverlayCanvas.Children.Add(leaderBoardPanel);
                leaderBoardPanel.SetHitTestVisible();
            }
        }

        private void RemoveComponents()
        {
            if (rank != null)
            {
                rank.ClearMemory();
                rank = null;
            }
            if (leaderBoardPanel != null)
            {
                leaderBoardPanel.SaveSettings();
                Core.OverlayCanvas.Children.Remove(leaderBoardPanel);
                leaderBoardPanel = null;
            }
        }

        public Version Version
        {
            get { return new Version(1, 2, 9); }
        }

    }
}