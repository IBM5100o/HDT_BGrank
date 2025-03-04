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
            get { return "No Settings"; }
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
        }

        public void OnLoad()
        {
            CreateMenuItem();
            MenuItem.IsChecked = true;
        }

        public void OnUnload()
        {
            MenuItem.IsChecked = false;
        }

        public void OnUpdate()
        {
            if (rank != null) 
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

            MenuItem.Checked += async (sender, args) =>
            {
                if (rank == null)
                {
                    rank = new BGrank();
                    GameEvents.OnGameStart.Add(rank.OnGameStart);
                    GameEvents.OnTurnStart.Add(rank.OnTurnStart);
                    leaderBoardPanel = new LeaderBoardPanel();
                    Core.OverlayCanvas.Children.Add(leaderBoardPanel);
                    leaderBoardPanel.SetHitTestVisible();
                }
            };

            MenuItem.Unchecked += (sender, args) =>
            {
                if (rank != null)
                {
                    rank = null;
                    leaderBoardPanel.SaveSettings();
                    Core.OverlayCanvas.Children.Remove(leaderBoardPanel);
                    leaderBoardPanel = null;
                }
            };
        }

        public Version Version
        {
            get { return new Version(1, 2, 4); }
        }

    }
}