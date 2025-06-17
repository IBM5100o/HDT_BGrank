using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace HDT_BGrank
{
    public partial class LeaderBoardPanel : UserControl, IDisposable
    {
        private bool isFirst = true;
        private bool isChange = false;
        private bool finished = false;
        private bool isDragging = false;
        private Point originalGridPosition;
        private Point originalMousePosition;

        public LeaderBoardPanel()
        {
            InitializeComponent();
            Visibility = Visibility.Hidden;

            Settings.Load();
            if (Settings.Instance.ifLoad)
            {
                GridTransform.ScaleX = Settings.Instance.scaleRatio;
                GridTransform.ScaleY = Settings.Instance.scaleRatio;
                Thickness newMargin = new Thickness(Settings.Instance.positionLeft, Settings.Instance.positionTop, LeaderGrid.Margin.Right, LeaderGrid.Margin.Bottom);
                LeaderGrid.Margin = newMargin;
                isFirst = false;
            }
        }

        public void SetHitTestVisible()
        {
            OverlayExtensions.SetIsOverlayHitTestVisible(LeaderText, true);
            OverlayExtensions.SetIsOverlayHitTestVisible(DeleteButton, true);
            OverlayExtensions.SetIsOverlayHitTestVisible(HiddenButton, true);
        }

        public void OnUpdate(BGrank rank)
        {
            if (Core.Game.IsInMenu)
            {
                Visibility = Visibility.Hidden;
                finished = false;
            }
            else if (!finished && rank.done)
            {
                if (isFirst)
                {
                    double ratio = Core.OverlayWindow.Width / 1920.0;
                    if (ratio < 0.5) { ratio = 0.5; }
                    else if (ratio > 2.0) { ratio = 2.0; }
                    GridTransform.ScaleX = ratio;
                    GridTransform.ScaleY = ratio;
                    isFirst = false;
                }

                StringBuilder allText = new StringBuilder();
                if (rank.failToGetData) { allText.Append("\nNo data now"); }
                else
                {
                    foreach (var opp in rank.oppDict)
                    {
                        allText.Append($"\n{opp.Key} {opp.Value}");
                    }
                }
                LeaderText.Text = allText.ToString();
                finished = true;
                Visibility = Visibility.Visible;
                rank.ClearMemory();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
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
            isDragging = true;
            originalMousePosition = e.GetPosition(this);
            originalGridPosition = new Point(LeaderGrid.Margin.Left, LeaderGrid.Margin.Top);
        }

        private void LeaderText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }

        private void LeaderText_MouseLeave(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void LeaderText_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                Point currentPosition = e.GetPosition(this);
                double offsetX = currentPosition.X - originalMousePosition.X;
                double offsetY = currentPosition.Y - originalMousePosition.Y;

                double newLeft = originalGridPosition.X + offsetX;
                double newTop = originalGridPosition.Y + offsetY;
                if (newLeft < 0) { newLeft = 0; }
                if (newTop < 0) { newTop = 0; }

                Thickness newMargin = new Thickness(newLeft, newTop, LeaderGrid.Margin.Right, LeaderGrid.Margin.Bottom);
                LeaderGrid.Margin = newMargin;
                isChange = true;
            }
        }

        private void LeaderText_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double newRatio = GridTransform.ScaleX;

            if (e.Delta > 0) { newRatio -= 0.1; }
            else if (e.Delta < 0) { newRatio += 0.1; }

            if (newRatio < 0.5) { newRatio = 0.5; }
            else if (newRatio > 2.0) { newRatio = 2.0; }
    
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

        public void Dispose()
        {
        }
    }
}
