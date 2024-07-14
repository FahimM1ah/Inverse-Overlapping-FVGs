using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class InverseOverlappingFVGs : Indicator
    {
        [Parameter("Period", DefaultValue = "500")]
        public int Period { get; set; }

        [Parameter("Bull Color", DefaultValue = "4001AF50", Group = "Colors")]
        public Color BullColor { get; set; }

        [Parameter("Bear Color", DefaultValue = "43FF0000", Group = "Colors")]
        public Color BearColor { get; set; }

        [Parameter("Panel Color", DefaultValue = "FF1B1818", Group = "Colors")]
        public Color PanelColor { get; set; }

        [Parameter("Button Color", DefaultValue = "FF737373", Group = "Colors")]
        public Color ButtonColor { get; set; }

        [Parameter("Button X Position", DefaultValue = HorizontalAlignment.Right, Group = "Toggle Button")]
        public HorizontalAlignment HorizontalAlignmentToggle { get; set; }

        [Parameter("Button Y Position", DefaultValue = VerticalAlignment.Top, Group = "Toggle Button")]
        public VerticalAlignment VerticalAlignmentToggle { get; set; }

        int lastIndex = 0;
        private bool toggled = true;

        protected override void Initialize()
        {
            if (Account.UserId != Account.UserId)
            {
                Print("No license for " + Account.UserId);
                return;
            }

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignmentToggle,
                VerticalAlignment = VerticalAlignmentToggle,
                BackgroundColor = PanelColor,
                Opacity = 0.7
            };
            var toggleButton = new ToggleButton
            {
                Text = "Toggle",
                BackgroundColor = ButtonColor,
                Margin = 10
            };

            toggleButton.Checked += ToggleButton_Checked;
            toggleButton.Unchecked += ToggleButton_Unchecked;

            stackPanel.AddChild(toggleButton);

            Chart.AddControl(stackPanel);
        }

        private void ToggleButton_Checked(ToggleButtonEventArgs obj)
        {
            toggled = true;
            DrawBullishOverlap();
            DrawBearishOverlap();
            Print("Toggled on - drawings added.");
            obj.ToggleButton.Text = "Toggle ON";
        }

        private void ToggleButton_Unchecked(ToggleButtonEventArgs obj)
        {
            toggled = false;
            RemoveDrawingsOnToggle();
            Print("Toggled off - drawings removed.");
            obj.ToggleButton.Text = "Toggle OFF";
        }


        public override void Calculate(int index)
        {
            if (Account.UserId != Account.UserId)
            {
                return;
            }
            if (index < Period)
                return;
            if (!IsLastBar)
                return;

            if (index != lastIndex && toggled)
            {
                for (int i = index - 1; i > index - Period; i--)
                {
                    FVG(i);
                }

                RemoveDrawings();

                RemoveInvalidBearishOverlaps();
                RemoveInvalidBullishOverlaps();
                OverlappingBullishFVG();
                OverlappingBearishFVG();


                DrawBullishOverlap();
                DrawBearishOverlap();

                lastIndex = index;
            }
            else if (!toggled)
            {
                RemoveDrawingsOnToggle();
            }
        }

        private List<Bar> upBars = new();
        private List<Bar> downBars = new();

        private Dictionary<Bar,Bar> overlappingBulls = new();
        private Dictionary<Bar, Bar> overlappingBears = new();

        private void FVG(int index)
        {
            var bars = new[]
            {
                Bars[index],
                Bars[index - 1],
                Bars[index - 2]
            };

            if (bars[2].Low - bars[0].High > 0 && !downBars.Contains(bars[1]))
            {
                downBars.Add(bars[1]);
            }

            if (bars[0].Low - bars[2].High > 0 && !upBars.Contains(bars[1]))
            {
                upBars.Add(bars[1]);
            }
        }

        private void OverlappingBullishFVG()
        {
            foreach (var downBar in downBars)
            {
                var downBarIndex = Bars.OpenTimes.GetIndexByExactTime(downBar.OpenTime);
                var downBarHigh = Bars.LowPrices[downBarIndex - 1];
                var downBarLow = Bars.HighPrices[downBarIndex + 1];

                foreach (var upBar in upBars)
                {
                    var upBarIndex = Bars.OpenTimes.GetIndexByExactTime(upBar.OpenTime);
                    var upBarHigh = Bars.LowPrices[upBarIndex + 1];
                    var upBarLow = Bars.HighPrices[upBarIndex - 1];

                    if (!(downBarIndex > upBarIndex))
                    {
                        if ((downBarHigh <= upBarHigh && downBarHigh >= upBarLow) || (downBarLow < upBarHigh && downBarLow > upBarLow))
                        {
                            if (!IsUpBarInvalidated(upBarIndex)
                                && !overlappingBulls.ContainsKey(downBar) && !overlappingBulls.Values.Contains(upBar)
                                && !IsDownBarInvalidated(downBarIndex, upBarIndex))
                            {
                                overlappingBulls.Add(downBar, upBar);
                            }
                        }

                    }
                }
            }
        }

        private void OverlappingBearishFVG()
        {
            foreach (var upBar in upBars)
            {
                var upBarIndex = Bars.OpenTimes.GetIndexByExactTime(upBar.OpenTime);
                var upBarHigh = Bars.LowPrices[upBarIndex + 1];
                var upBarLow = Bars.HighPrices[upBarIndex - 1];

                foreach (var downBar in downBars)
                {
                    var downBarIndex = Bars.OpenTimes.GetIndexByExactTime(downBar.OpenTime);
                    var downBarHigh = Bars.LowPrices[downBarIndex - 1];
                    var downBarLow = Bars.HighPrices[downBarIndex + 1];

                    if (!(upBarIndex > downBarIndex))
                    {
                        if ((upBarHigh <= downBarHigh && upBarHigh >= downBarLow) || (upBarLow < downBarHigh && upBarLow > downBarLow))
                        {
                            if (!IsDownBarInvalidated(downBarIndex)
                                && !overlappingBears.ContainsKey(upBar) && !overlappingBears.Values.Contains(downBar)
                                && !IsUpBarInvalidated(upBarIndex, downBarIndex))
                            {
                                overlappingBears.Add(upBar, downBar);
                            }
                        }
                    }
                }
            }
        }

        private bool IsUpBarInvalidated(int upBarIndex)
        {
            for (int i = upBarIndex; i < Bars.Count - 1; i++)
            {
                if (Bars[i].Close < Bars[upBarIndex - 1].High)
                    return true;
            }
            return false;
        }

        private bool IsUpBarInvalidated(int upBarIndex, int downBarIndex)
        {
            for (int i = upBarIndex; i < downBarIndex; i++)
            {
                if (Bars[i].Close < Bars[upBarIndex - 1].High)
                    return true;
            }
            return false;
        }

        private bool IsDownBarInvalidated(int downBarIndex)
        {
            for (int i = downBarIndex; i < Bars.Count - 1; i++)
            {
                if (Bars[i].Close > Bars[downBarIndex - 1].Low)
                    return true;
            }
            return false;
        }

        private bool IsDownBarInvalidated(int downBarIndex, int upBarIndex)
        {
            for (int i = downBarIndex; i < upBarIndex; i++)
            {
                if (Bars[i].Close > Bars[downBarIndex - 1].Low)
                    return true;
            }
            return false;
        }

        #region Drawing
        private void DrawBullishOverlap()
        {
            foreach(var downBar in overlappingBulls.Keys)
            {
                var downBarIndex = Bars.OpenTimes.GetIndexByExactTime(downBar.OpenTime);
                var downBarHigh = Bars.LowPrices[downBarIndex - 1];
                var downBarLow = Bars.HighPrices[downBarIndex + 1];

                Chart.DrawRectangle($"{downBarIndex}_FVG", downBar.OpenTime, downBarLow, Server.Time, downBarHigh, BearColor).IsFilled = true;
            }

            foreach (var upBar in overlappingBulls.Values)
            {
                var upBarIndex = Bars.OpenTimes.GetIndexByExactTime(upBar.OpenTime);
                var upBarHigh = Bars.LowPrices[upBarIndex + 1];
                var upBarLow = Bars.HighPrices[upBarIndex - 1];

                Chart.DrawRectangle($"{upBarIndex}_FVG", upBar.OpenTime, upBarLow, Server.Time, upBarHigh, BullColor).IsFilled = true;
            }
        }


        private void DrawBearishOverlap()
        {
            foreach (var downBar in overlappingBears.Values)
            {
                var downBarIndex = Bars.OpenTimes.GetIndexByExactTime(downBar.OpenTime);
                var downBarHigh = Bars.LowPrices[downBarIndex - 1];
                var downBarLow = Bars.HighPrices[downBarIndex + 1];

                Chart.DrawRectangle($"{downBarIndex}_FVG", downBar.OpenTime, downBarLow, Server.Time, downBarHigh, BearColor).IsFilled = true;
            }

            foreach (var upBar in overlappingBears.Keys)
            {
                var upBarIndex = Bars.OpenTimes.GetIndexByExactTime(upBar.OpenTime);
                var upBarHigh = Bars.LowPrices[upBarIndex + 1];
                var upBarLow = Bars.HighPrices[upBarIndex - 1];

                Chart.DrawRectangle($"{upBarIndex}_FVG", upBar.OpenTime, upBarLow, Server.Time, upBarHigh, BullColor).IsFilled = true;
            }
        }
        #endregion

        private void RemoveInvalidBullishOverlaps()
        {
            foreach (var downBar in overlappingBulls.Keys)
            {
                var upBarIndex = Bars.OpenTimes.GetIndexByExactTime(overlappingBulls[downBar].OpenTime);
                if (IsUpBarInvalidated(upBarIndex))
                    overlappingBulls.Remove(downBar);
            }
        }

        private void RemoveInvalidBearishOverlaps()
        {
            foreach (var upBar in overlappingBears.Keys)
            {
                var downBarIndex = Bars.OpenTimes.GetIndexByExactTime(overlappingBears[upBar].OpenTime);
                if (IsDownBarInvalidated(downBarIndex))
                    overlappingBears.Remove(upBar);
            }
        }


        private void RemoveDrawings()
        {
            foreach (var obj in Chart.Objects)
            {
                var split = obj.Name.Split('_');
                var barIndex = BarIndex(split[0]);
                if (barIndex == -1)
                    return;
                if (!overlappingBears.ContainsKey(Bars[barIndex])
                    && !overlappingBulls.ContainsKey(Bars[barIndex])
                    && !overlappingBears.Values.Contains(Bars[barIndex])
                    && !overlappingBulls.Values.Contains(Bars[barIndex]))
                {
                    Chart.RemoveObject(obj.Name);
                }

            }
        }

        private void RemoveDrawingsOnToggle()
        {
            List<ChartObject> list = new();
            foreach (var obj in Chart.Objects)
            {
                var split = obj.Name.Split('_');
                var barIndex = BarIndex(split[0]);
                if (upBars.Contains(Bars[barIndex]) || downBars.Contains(Bars[barIndex])) { list.Add(obj); }
            }
            foreach (var obj in list)
            {
                Chart.RemoveObject(obj.Name);
            }
        }

        private int BarIndex(string name)
        {
            int num;
            bool success = int.TryParse(name, out num);
            if (success)
                return num;
            return -1;
        }
    }
}