using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Core = Hearthstone_Deck_Tracker.API.Core;

namespace HDT_BGrank
{
    public partial class LeaderBoardPanel : UserControl, IDisposable
    {
        public Dictionary<string, string> oppDict = new Dictionary<string, string>();
        private bool isFirst = true;
        private bool isChange = false;
        private bool hidden = false;
        private Point originalGridPosition;
        private Point originalMousePosition;
        public bool IsDragging { get; private set; }

        public LeaderBoardPanel()
        {
            InitializeComponent();
            Visibility = Visibility.Hidden;

            Settings.Load();
            if (Settings.Instance.ifLoad)
            {
                GridTransform.ScaleX = Settings.Instance.scaleRatio;
                GridTransform.ScaleY = Settings.Instance.scaleRatio;
                LeaderGrid.Margin = new Thickness(Settings.Instance.positionLeft, Settings.Instance.positionTop, 0.0, 0.0);
                isFirst = false;
            }
        }

        public void SetHitTestVisible()
        {
            OverlayExtensions.SetIsOverlayHitTestVisible(LeaderText, true);
            OverlayExtensions.SetIsOverlayHitTestVisible(DeleteButton, true);
            OverlayExtensions.SetIsOverlayHitTestVisible(HiddenButton, true);
        }

        public void OnUpdate()
        {
            if (Core.Game.IsInMenu)
            {
                Visibility = Visibility.Hidden;
            }
            else if (!hidden && oppDict.Count > 0)
            {
                Visibility = Visibility.Visible;
            }
        }

        public bool TryAddOpponent(string playerName, int? rank)
        {
            if (oppDict.ContainsKey(playerName))
                return false;

            var playerRank = rank?.ToString();
            playerRank ??= (Core.Game.CurrentRegion == Region.CHINA ? "-" : "8000↓");

            oppDict.Add(playerName, playerRank);
            UpdateText();
            return true;
        }

        public void UpdateText()
        {
            if (isFirst)
            {
                double ratio = Math.Max(0.5, Math.Min(Core.OverlayWindow.Width / 1920.0, 2.0));
                GridTransform.ScaleX = ratio;
                GridTransform.ScaleY = ratio;
                SetPosition(LeaderGrid.Margin.Left, LeaderGrid.Margin.Top);
                isFirst = false;
            }

            StringBuilder allText = new StringBuilder();
            foreach (var opp in oppDict)
            {
                allText.Append($"\n{opp.Key} {opp.Value}");
            }
            
            for (var i = oppDict.Count; i < 7; i ++)
            {
                allText.Append($"\n? ?");
            }

            LeaderText.Text = allText.ToString();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            hidden = true;
            Visibility = Visibility.Hidden;
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

        private void LeaderText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            IsDragging = true;
            originalMousePosition = e.GetPosition(this);
            originalGridPosition = new Point(LeaderGrid.Margin.Left, LeaderGrid.Margin.Top);
        }

        private void LeaderText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            IsDragging = false;
        }

        private void LeaderText_MouseLeave(object sender, MouseEventArgs e)
        {
            IsDragging = false;
        }

        private void LeaderText_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsDragging)
            {
                Point currentPosition = e.GetPosition(this);
                double offsetX = currentPosition.X - originalMousePosition.X;
                double offsetY = currentPosition.Y - originalMousePosition.Y;

                double newLeft = originalGridPosition.X + offsetX;
                double newTop = originalGridPosition.Y + offsetY;

                SetPosition(newLeft, newTop);
                isChange = true;
            }
        }

        private void LeaderText_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double newRatio = GridTransform.ScaleX;

            if (e.Delta > 0) { newRatio -= 0.1; }
            else if (e.Delta < 0) { newRatio += 0.1; }

            newRatio = Math.Max(0.5, Math.Min(newRatio, 2.0));

            GridTransform.ScaleX = newRatio;
            GridTransform.ScaleY = newRatio;
            isChange = true;
        }

        public void SaveSettings()
        {
            if (isChange)
            {
                Settings.Instance.scaleRatio = GridTransform.ScaleX;
                Settings.Instance.positionLeft = LeaderGrid.Margin.Left;
                Settings.Instance.positionTop = LeaderGrid.Margin.Top;
                Settings.Instance.ifLoad = true;
                Settings.Save();
            }
        }

        private void SetPosition(double nowLeft, double nowTop)
        {
            double newLeft = Math.Max(0.0, Math.Min(nowLeft, Core.OverlayWindow.Width - LeaderText.ActualWidth*GridTransform.ScaleX));
            double newTop = Math.Max(0.0, Math.Min(nowTop, Core.OverlayWindow.Height - LeaderText.ActualHeight*GridTransform.ScaleX));
            LeaderGrid.Margin = new Thickness(newLeft, newTop, 0.0, 0.0);
        }

        public void Dispose()
        {
        }

        internal void Clear()
        {
            oppDict.Clear();
            UpdateText();
        }
    }
}
