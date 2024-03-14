using Hearthstone_Deck_Tracker.Utility.Extensions;
using System;
using System.Windows;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;

namespace HDT_BGrank
{
    /// <summary>
    /// UserControl1.xaml 的互動邏輯
    /// </summary>
    public partial class LeaderBoardPanel : UserControl, IDisposable
    {
        bool finished = false;
        public LeaderBoardPanel()
        {
            InitializeComponent();
            OverlayExtensions.SetIsOverlayHitTestVisible(DeleteButton, true);
            OverlayExtensions.SetIsOverlayHitTestVisible(HiddenButton, true);
        }
        public void OnUpdate(BGrank rank)
        {
            if (!finished && rank.done)
            {
                int i = 0;
                string allText = "\n";
                foreach (var opp in rank.oppDict)
                {
                    allText += opp.Key + " " + opp.Value;
                    i++;
                    if (i < rank.oppDict.Count) { allText += "\n"; }
                }
                LeaderText.Text = allText;
                finished = true;
                Visibility = Visibility.Visible;
            }
            if (Core.Game.IsInMenu) 
            { 
                Visibility = Visibility.Hidden;
                finished = false;
            }
        }
        public void Dispose()
        {
        }

        private void HiddenButton_Click(object sender, RoutedEventArgs e)
        {
            if (LeaderText.IsVisible)
            {
                LeaderText.Visibility = Visibility.Hidden;
            }
            else
            {
                LeaderText.Visibility = Visibility.Visible;
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
        }

    }
}
