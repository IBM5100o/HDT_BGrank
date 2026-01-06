using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Utility.Extensions;

namespace HDT_BGrank
{
    public partial class LeaderBoardPanel : UserControl
    {
        private bool isFirst = true;
        private bool isChange = false;
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
                LeaderGrid.Margin = new Thickness(Settings.Instance.positionLeft, Settings.Instance.positionTop, 0.0, 0.0);
                isFirst = false;
            }
            Core.OverlayCanvas.Children.Add(this);
            OverlayExtensions.SetIsOverlayHitTestVisible(LeaderText, true);
            OverlayExtensions.SetIsOverlayHitTestVisible(DeleteButton, true);
            OverlayExtensions.SetIsOverlayHitTestVisible(HiddenButton, true);
        }

        public void DisplayResult(string text)
        {
            if (isFirst)
            {
                double ratio = Math.Max(0.5, Math.Min(Core.OverlayWindow.Width / 1920.0, 2.0));
                GridTransform.ScaleX = ratio;
                GridTransform.ScaleY = ratio;
                isFirst = false;
            }

            LeaderText.Text = text;
            Visibility = Visibility.Visible;
            SetPosition(LeaderGrid.Margin.Left, LeaderGrid.Margin.Top);
        }

        public void Clean(bool save)
        {
            if (save && isChange)
            {
                Settings.Instance.scaleRatio = GridTransform.ScaleX;
                Settings.Instance.positionLeft = LeaderGrid.Margin.Left;
                Settings.Instance.positionTop = LeaderGrid.Margin.Top;
                Settings.Instance.ifLoad = true;
                Settings.Save();
            }
            Core.OverlayCanvas.Children.Remove(this);
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

        private void SetPosition(double nowLeft, double nowTop)
        {
            double newLeft = Math.Max(0.0, Math.Min(nowLeft, Core.OverlayWindow.Width - LeaderText.ActualWidth*GridTransform.ScaleX));
            double newTop = Math.Max(0.0, Math.Min(nowTop, Core.OverlayWindow.Height - LeaderText.ActualHeight*GridTransform.ScaleX));
            LeaderGrid.Margin = new Thickness(newLeft, newTop, 0.0, 0.0);
        }
    }
}
