﻿using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using HDT_BGrank.Properties;
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
            if (Settings.Default.ifLoad)
            {
                GridTransform.ScaleX = Settings.Default.scaleRatio;
                GridTransform.ScaleY = Settings.Default.scaleRatio;
                Thickness newMargin = new Thickness(Settings.Default.positionLeft, Settings.Default.positionTop, LeaderGrid.Margin.Right, LeaderGrid.Margin.Bottom);
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
                int i = 0;
                string allText = "\n";

                if (isFirst)
                {
                    double ratio = Core.OverlayWindow.Width / 1920.0;
                    if (ratio < 0.5) { ratio = 0.5; }
                    else if (ratio > 2.0) { ratio = 2.0; }
                    GridTransform.ScaleX = ratio;
                    GridTransform.ScaleY = ratio;
                    isFirst = false;
                }
                
                if (rank.failToGetData) { allText += "No data now"; }
                else
                {
                    foreach (var opp in rank.oppDict)
                    {
                        allText += opp.Key + " " + opp.Value;
                        i++;
                        if (i < rank.oppDict.Count) { allText += "\n"; }
                    }
                }
                LeaderText.Text = allText;
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

        private void LeaderText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isDragging = true;
                originalMousePosition = e.GetPosition(this);
                originalGridPosition = new Point(LeaderGrid.Margin.Left, LeaderGrid.Margin.Top);
            }
        }

        private void LeaderText_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isDragging = false;
            }
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
                Settings.Default.scaleRatio = GridTransform.ScaleX;
                Settings.Default.positionLeft = LeaderGrid.Margin.Left;
                Settings.Default.positionTop = LeaderGrid.Margin.Top;
                Settings.Default.ifLoad = true;
                Settings.Default.Save();
            }
        }

        public void Dispose()
        {
        }
    }
}
