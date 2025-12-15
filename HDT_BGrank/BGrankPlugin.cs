using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Windows;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
            Settings.Instance.scaleRatio = 1.0;
            Settings.Instance.positionLeft = 0.0;
            Settings.Instance.positionTop = 0.0;
            Settings.Instance.ifLoad = false;
            Settings.Save();

            if (rank != null)
            {
                rank = null;
                Core.OverlayCanvas.Children.Remove(leaderBoardPanel);
                leaderBoardPanel = null;

                leaderBoardPanel = new LeaderBoardPanel();
                rank = new BGrank(leaderBoardPanel);
                GameEvents.OnGameStart.Add(rank.OnGameStart);
                GameEvents.OnTurnStart.Add(rank.OnTurnStart);

                //Core.OverlayCanvas.Children.Add(leaderBoardPanel);
                var movables = (Dictionary<UIElement, ResizeGrip>)typeof(OverlayWindow).GetField("_movableElements").GetValue(Core.OverlayWindow);
                movables.Add(leaderBoardPanel, new ResizeGrip());

                leaderBoardPanel.SetHitTestVisible();
            }
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
                    leaderBoardPanel.OnUpdate();

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
                if (rank == null)
                {
                    leaderBoardPanel = new LeaderBoardPanel();
                    rank = new BGrank(leaderBoardPanel);
                    GameEvents.OnGameStart.Add(rank.OnGameStart);
                    GameEvents.OnTurnStart.Add(rank.OnTurnStart);
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
            get { return new Version(1, 2, 8); }
        }

    }
}