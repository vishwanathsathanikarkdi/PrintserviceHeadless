using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ScottPlot;

namespace PrintserviceHeadless.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ScotPlotController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        [HttpPost("plot-scotplot")]
        public IActionResult PlotTracksScotPlot([FromBody] PlotTrackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WidgetsAsJsonString))
                return BadRequest("widgetsAsJsonString is required.");

            var widgets = JsonDocument.Parse(request.WidgetsAsJsonString).RootElement;
            var config = widgets[0].GetProperty("configuration").GetProperty("widgetConfiguration");
            var tracks = config.GetProperty("tracks");
            int orientation = config.TryGetProperty("orientation", out var orientElem) ? orientElem.GetInt32() : 1; // default to horizontal

            int width = 1200, height = 800;
            int pointsPerTrack = 100;

            var (trackNames, trackCurves) = ParseTracks(tracks, pointsPerTrack);

            var plt = new ScottPlot.Plot();
            plt.Title("Tracks (Grouped)");
            plt.XLabel("Index");
            plt.YLabel("Track");

            if (orientation == 1) // Horizontal
            {
                DrawScotPlotHorizontal(plt, trackNames, trackCurves, pointsPerTrack);
            }
            else // Vertical
            {
                DrawScotPlotVertical(plt, trackNames, trackCurves, pointsPerTrack);
            }

            var imageBytes = plt.GetImageBytes(width, height, ScottPlot.ImageFormat.Png);
            return File(imageBytes, "image/png");
        }

        [HttpPost("plot-scotplot-multiaxis")]
        public IActionResult PlotTracksMultiaxisScotPlot([FromBody] PlotTrackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WidgetsAsJsonString))
                return BadRequest("widgetsAsJsonString is required.");

            var widgets = JsonDocument.Parse(request.WidgetsAsJsonString).RootElement;
            var config = widgets[0].GetProperty("configuration").GetProperty("widgetConfiguration");
            var tracks = config.GetProperty("tracks");
            int orientation = config.TryGetProperty("orientation", out var orientElem) ? orientElem.GetInt32() : 1; // default to horizontal

            int width = 1200, height = 800;
            int pointsPerTrack = 100;

            var (trackNames, trackCurves) = ParseTracks(tracks, pointsPerTrack);

            var plt = new ScottPlot.Plot();
            plt.Title("Tracks (Multi-Axis)");
            plt.XLabel("Index");
            plt.YLabel("Track");

            if (orientation == 1) // Horizontal
            {
                DrawScotPlotHorizontalMultiAxis(plt, trackNames, trackCurves, pointsPerTrack);
            }
            else // Vertical
            {
                DrawScotPlotVerticalMultiAxis(plt, trackNames, trackCurves, pointsPerTrack);
            }

            var imageBytes = plt.GetImageBytes(width, height, ScottPlot.ImageFormat.Png);
            return File(imageBytes, "image/png");
        }

        private void DrawScotPlotHorizontal(
            ScottPlot.Plot plt,
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves,
            int pointsPerTrack)
        {
            int nTracks = trackNames.Count;
            const double trackHeight = 1.0; // Uniform track height

            // Build custom tick labels with curve data stacked vertically with color indicators
            var customLabels = new List<string>();
            foreach (var (trackName, curves) in trackCurves)
            {
                var labelBuilder = new System.Text.StringBuilder();
                labelBuilder.AppendLine($"{trackName}"); // Track name
                customLabels.Add(labelBuilder.ToString().TrimEnd());
            }

            // Set Y axis to categorical with uniform spacing
            double[] yPositions = Enumerable.Range(0, nTracks).Select(i => i * trackHeight).ToArray();
            plt.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(yPositions, customLabels.ToArray());

            // Set custom colors for tick labels to match curve colors
            plt.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Colors.Black;

            // Increase left margin to accommodate multi-line labels and prevent cutoff
            plt.Layout.Fixed(new ScottPlot.PixelPadding(220, 60, 60, 150));

            // Set X axis range with uniform track heights
            //plt.Axes.SetLimits(0, pointsPerTrack, -0.5 * trackHeight, (nTracks - 0.5) * trackHeight);

            // Remove automatic axis margins that create unwanted padding
            plt.Axes.Margins(0, 0);

            int trackBand = 0;
            foreach (var (trackName, curves) in trackCurves)
            {
                double trackBaseY = trackBand * trackHeight;
                var isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Clamp normalized data to stay strictly within track band (0.1 to 0.9 range)
                    var xData = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();
                    var yData = data.Select(d =>
                    {
                        double yNorm = (d - min) / (max - min + 1e-9);
                        // Clamp between 0.1 and 0.9 to ensure curves stay within band
                        yNorm = Math.Max(0.0, Math.Min(1.0, yNorm));
                        return trackBaseY + (0.1 + yNorm * 0.8) * trackHeight;
                    }).ToArray();

                    if (isFirstCurve)
                    {
                        // Fill the first curve
                        var fillYData = new List<double>(yData);
                        fillYData.Add(trackBaseY + 0.1 * trackHeight); // Bottom boundary
                        fillYData.Insert(0, trackBaseY + 0.1 * trackHeight); // Bottom boundary at start

                        var fillXData = new List<double>(xData);
                        fillXData.Add(xData[xData.Length - 1]); // Last X
                        fillXData.Insert(0, xData[0]); // First X

                        var polygon = plt.Add.Polygon(fillXData.ToArray(), fillYData.ToArray());
                        polygon.FillColor = ScottPlot.Color.FromHex(color).WithAlpha(0.3);
                        polygon.LineWidth = 0;

                        isFirstCurve = false;
                    }

                    var scatter = plt.Add.ScatterLine(xData, yData);
                    scatter.Color = ScottPlot.Color.FromHex(color);
                    scatter.LineWidth = (float)thickness;
                }

                // Add separator line at uniform intervals
                if (trackBand < trackCurves.Count - 1)
                {
                    double separatorY = (trackBand + 1) * trackHeight - 0.5 * trackHeight;
                    var hline = plt.Add.HorizontalLine(separatorY);
                    hline.Color = ScottPlot.Colors.Gray;
                    hline.LineWidth = 1;
                    hline.LinePattern = ScottPlot.LinePattern.Dashed;
                }

                // hardcoded marker at specific position
                trackBand++;
            }

            // Don't show legend
            plt.ShowLegend(Edge.Top);
            plt.HideGrid();
            plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D3D3D3");
            plt.ShowGrid();

            // Add legend entries for all curves
            foreach (var (trackName, curves) in trackCurves)
            {
                foreach (var (curveTitle, color, _, _, thickness, _) in curves)
                {
                    // Legend entries are automatically added when you set the label
                    var dummyScatter = plt.Add.ScatterLine(new double[] { }, new double[] { });
                    dummyScatter.Color = ScottPlot.Color.FromHex(color);
                    dummyScatter.LineWidth = (float)thickness;
                    dummyScatter.LegendText = $"{trackName} - {curveTitle}";
                }
            }

        }

        private void DrawScotPlotVertical(
            ScottPlot.Plot plt,
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves,
            int pointsPerTrack)
        {
            int nTracks = trackNames.Count;
            const double trackWidth = 1.0; // Uniform track width

            // Build custom tick labels with curve data stacked vertically with color indicators
            var customLabels = new List<string>();
            foreach (var (trackName, curves) in trackCurves)
            {
                var labelBuilder = new System.Text.StringBuilder();
                labelBuilder.AppendLine($"█ {trackName}"); // Track name with block indicator
                customLabels.Add(labelBuilder.ToString().TrimEnd());
            }

            // Set X axis to categorical with uniform spacing
            double[] xPositions = Enumerable.Range(0, nTracks).Select(i => i * trackWidth + trackWidth * 0.5).ToArray();
            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(xPositions, customLabels.ToArray());

            // Set custom colors for tick labels
            plt.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Colors.Black;

            // Increase bottom margin significantly to accommodate multi-line labels and prevent vertical cutoff
            plt.Layout.Fixed(new ScottPlot.PixelPadding(100, 60, 60, 200));

            // Set axis range with uniform track widths
         //   plt.Axes.SetLimits(0, (nTracks - 0.5) * trackWidth, 0, pointsPerTrack);
            plt.Axes.Margins(0, 0); // Remove automatic margins

            int trackBand = 0;
            foreach (var (trackName, curves) in trackCurves)
            {
                double trackBaseX = trackBand * trackWidth;
                var isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Clamp normalized data to stay strictly within track band
                    var yData = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();
                    var xData = data.Select(d =>
                    {
                        double xNorm = (d - min) / (max - min + 1e-9);
                        // Clamp between 0.1 and 0.9 to ensure curves stay within band
                        xNorm = Math.Max(0.0, Math.Min(1.0, xNorm));
                        return trackBaseX + (0.1 + xNorm * 0.8) * trackWidth;
                    }).ToArray();

                    if (isFirstCurve)
                    {
                        // Fill the first curve
                        var fillXData = new List<double>(xData);
                        fillXData.Add(trackBaseX + 0.1 * trackWidth); // Left boundary
                        fillXData.Insert(0, trackBaseX + 0.1 * trackWidth); // Left boundary at start

                        var fillYData = new List<double>(yData);
                        fillYData.Add(yData[yData.Length - 1]); // Last Y
                        fillYData.Insert(0, yData[0]); // First Y

                        var polygon = plt.Add.Polygon(fillXData.ToArray(), fillYData.ToArray());
                        polygon.FillColor = ScottPlot.Color.FromHex(color).WithAlpha(0.3);
                        polygon.LineWidth = 0;

                        isFirstCurve = false;
                    }

                    var scatter = plt.Add.ScatterLine(xData, yData);
                    scatter.Color = ScottPlot.Color.FromHex(color);
                    scatter.LineWidth = (float)thickness;
                }

                // Add separator line at uniform intervals
                if (trackBand < trackCurves.Count - 1)
                {
                    double separatorX = (trackBand + 1) * trackWidth - 0.5 * trackWidth;
                    var vline = plt.Add.VerticalLine(separatorX);
                    vline.Color = ScottPlot.Colors.Gray;
                    vline.LineWidth = 1;
                    vline.LinePattern = ScottPlot.LinePattern.Dashed;
                }


                // Add markers at specific Y positions for each track
                if (trackBand == 0) // Track 1
                {
                    double markerY = 80;
                    double markerX = trackBaseX + trackWidth * 0.5; // Center of track

                    // Add marker
                    var marker = plt.Add.Marker(markerX, markerY);
                    marker.Color = ScottPlot.Colors.Red;
                    marker.Size = 10;
                    marker.Shape = MarkerShape.FilledDiamond;

                    // Add custom text annotation
                    var text = plt.Add.Text("Marker 1", markerX, markerY);
                    text.LabelFontColor = ScottPlot.Colors.Red;
                    text.LabelFontSize = 12;
                    text.LabelBold = true;
                    text.LabelBackgroundColor = ScottPlot.Colors.White.WithAlpha(0.8);
                    text.LabelBorderColor = ScottPlot.Colors.Red;
                    text.LabelBorderWidth = 2;
                    text.OffsetY = 10; // Offset text above the marker

                    // Add horizontal line segment across the track at Y=150
                    double lineStartX = trackBaseX + 0.1 * trackWidth;
                    double lineEndX = trackBaseX + 0.9 * trackWidth;
                    var markerLine = plt.Add.Line(lineStartX, markerY, lineEndX, markerY);
                    markerLine.Color = ScottPlot.Colors.Red;
                    markerLine.LineWidth = 2;
                    markerLine.LinePattern = ScottPlot.LinePattern.Dotted;
                }
                else if (trackBand == 1) // Track 2
                {
                    double markerY = 90;
                    double markerX = trackBaseX + trackWidth * 0.5; // Center of track

                    var marker = plt.Add.Marker(markerX, markerY);
                    marker.Color = ScottPlot.Colors.Blue;
                    marker.Size = 10;
                    marker.Shape = MarkerShape.FilledCircle;

                    // Add horizontal line segment across the track at Y=90
                    double lineStartX = trackBaseX + 0.1 * trackWidth;
                    double lineEndX = trackBaseX + 0.9 * trackWidth;
                    var markerLine = plt.Add.Line(lineStartX, markerY, lineEndX, markerY);
                    markerLine.Color = ScottPlot.Colors.Blue;
                    markerLine.LineWidth = 2;
                    markerLine.LinePattern = ScottPlot.LinePattern.Dotted;
                }

                // Add separator line at uniform intervals
                if (trackBand < trackCurves.Count - 1)
                {
                    double separatorX = (trackBand + 1) * trackWidth - 0.5 * trackWidth;
                    var vline = plt.Add.VerticalLine(separatorX);
                    vline.Color = ScottPlot.Colors.Gray;
                    vline.LineWidth = 1;
                    vline.LinePattern = ScottPlot.LinePattern.Dashed;
                }
                trackBand++;
            }


            // Add legend entries for all curves
            foreach (var (trackName, curves) in trackCurves)
            {
                foreach (var (curveTitle, color, _, _, thickness, _) in curves)
                {
                    // Legend entries are automatically added when you set the label
                    var dummyScatter = plt.Add.ScatterLine(new double[] { }, new double[] { });
                    dummyScatter.Color = ScottPlot.Color.FromHex(color);
                    dummyScatter.LineWidth = (float)thickness;
                    dummyScatter.LegendText = $"{trackName} - {curveTitle}";
                }
            }

            plt.ShowLegend(Edge.Top);
            // Don't show legend
            plt.HideGrid();
            plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D3D3D3");
            plt.ShowGrid();
        }

        private void DrawScotPlotHorizontalMultiAxis(
            ScottPlot.Plot plt,
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves,
            int pointsPerTrack)
        {
            int nTracks = trackNames.Count;
            const double trackHeight = 1.0; // Uniform track height

            // Build custom tick labels
            var customLabels = new List<string>();
            foreach (var (trackName, curves) in trackCurves)
            {
                customLabels.Add(trackName);
            }

            // Set Y axis to categorical with uniform spacing
            double[] yPositions = Enumerable.Range(0, nTracks).Select(i => i * trackHeight).ToArray();
            plt.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(yPositions, customLabels.ToArray());
            plt.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Colors.Black;

            // Increase left margin to accommodate scale labels
            plt.Layout.Fixed(new ScottPlot.PixelPadding(280, 60, 60, 150));
            plt.Axes.Margins(0, 0);

            int trackBand = 0;
            foreach (var (trackName, curves) in trackCurves)
            {
                double trackBaseY = trackBand * trackHeight;
                bool isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Each curve uses the FULL track height with its own scale
                    var xData = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();
                    var yData = data.Select(d =>
                    {
                        double normalizedValue = (d - min) / (max - min + 1e-9);
                        normalizedValue = Math.Max(0.0, Math.Min(1.0, normalizedValue));
                        // Map to full track height (with 10% padding on top and bottom)
                        return trackBaseY + (0.1 + normalizedValue * 0.8) * trackHeight;
                    }).ToArray();

                    // Fill only the first curve
                    if (isFirstCurve)
                    {
                        var fillYData = new List<double>(yData);
                        fillYData.Add(trackBaseY + 0.1 * trackHeight); // Bottom boundary
                        fillYData.Insert(0, trackBaseY + 0.1 * trackHeight); // Bottom boundary at start

                        var fillXData = new List<double>(xData);
                        fillXData.Add(xData[xData.Length - 1]); // Last X
                        fillXData.Insert(0, xData[0]); // First X

                        var polygon = plt.Add.Polygon(fillXData.ToArray(), fillYData.ToArray());
                        polygon.FillColor = ScottPlot.Color.FromHex(color).WithAlpha(0.3);
                        polygon.LineWidth = 0;

                        isFirstCurve = false;
                    }

                    // Draw curve line
                    var scatter = plt.Add.ScatterLine(xData, yData);
                    scatter.Color = ScottPlot.Color.FromHex(color);
                    scatter.LineWidth = (float)thickness;
                }

                // Add separator line between tracks
                if (trackBand < trackCurves.Count - 1)
                {
                    double separatorY = (trackBand + 1) * trackHeight;
                    var hline = plt.Add.HorizontalLine(separatorY);
                    hline.Color = ScottPlot.Colors.Red;
                    hline.LineWidth = 1;
                    hline.LinePattern = ScottPlot.LinePattern.Dashed;
                }

                trackBand++;
            }

            // Add legend entries
            foreach (var (trackName, curves) in trackCurves)
            {
                foreach (var (curveTitle, color, min, max, thickness, _) in curves)
                {
                    var dummyScatter = plt.Add.ScatterLine(new double[] { }, new double[] { });
                    dummyScatter.Color = ScottPlot.Color.FromHex(color);
                    dummyScatter.LineWidth = (float)thickness;
                    dummyScatter.LegendText = $"{trackName} - {curveTitle} [{min:F1} to {max:F1}]";
                }
            }

            plt.ShowLegend(Edge.Top);
            plt.HideGrid();
            plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D3D3D3");
            plt.ShowGrid();
        }

        private void DrawScotPlotVerticalMultiAxis(
            ScottPlot.Plot plt,
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves,
            int pointsPerTrack)
        {
            int nTracks = trackNames.Count;
            const double trackWidth = 1.0; // Uniform track width

            // Build custom tick labels
            var customLabels = new List<string>();
            foreach (var (trackName, curves) in trackCurves)
            {
                customLabels.Add(trackName);
            }

            // Set X axis to categorical with uniform spacing
            double[] xPositions = Enumerable.Range(0, nTracks).Select(i => i * trackWidth + trackWidth * 0.5).ToArray();
            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(xPositions, customLabels.ToArray());
            plt.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Colors.Black;

            // Increase bottom margin to accommodate scale labels
            plt.Layout.Fixed(new ScottPlot.PixelPadding(100, 60, 60, 250));
            plt.Axes.Margins(0, 0);

            int trackBand = 0;
            foreach (var (trackName, curves) in trackCurves)
            {
                double trackBaseX = trackBand * trackWidth;
                bool isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Each curve uses the FULL track width with its own scale
                    var yData = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();
                    var xData = data.Select(d =>
                    {
                        double normalizedValue = (d - min) / (max - min + 1e-9);
                        normalizedValue = Math.Max(0.0, Math.Min(1.0, normalizedValue));
                        // Map to full track width (with 10% padding on left and right)
                        return trackBaseX + (0.1 + normalizedValue * 0.8) * trackWidth;
                    }).ToArray();

                    // Fill only the first curve
                    if (isFirstCurve)
                    {
                        var fillXData = new List<double>(xData);
                        fillXData.Add(trackBaseX + 0.1 * trackWidth); // Left boundary
                        fillXData.Insert(0, trackBaseX + 0.1 * trackWidth); // Left boundary at start

                        var fillYData = new List<double>(yData);
                        fillYData.Add(yData[yData.Length - 1]); // Last Y
                        fillYData.Insert(0, yData[0]); // First Y

                        var polygon = plt.Add.Polygon(fillXData.ToArray(), fillYData.ToArray());
                        polygon.FillColor = ScottPlot.Color.FromHex(color).WithAlpha(0.3);
                        polygon.LineWidth = 0;

                        isFirstCurve = false;
                    }

                    // Draw curve line
                    var scatter = plt.Add.ScatterLine(xData, yData);
                    scatter.Color = ScottPlot.Color.FromHex(color);
                    scatter.LineWidth = (float)thickness;
                }

                // Add separator line between tracks
                if (trackBand < trackCurves.Count - 1)
                {
                    double separatorX = (trackBand + 1) * trackWidth;
                    var vline = plt.Add.VerticalLine(separatorX);
                    vline.Color = ScottPlot.Colors.DarkRed;
                    vline.LineWidth = 1;
                    vline.LinePattern = ScottPlot.LinePattern.Dashed;
                }

                trackBand++;
            }

            // Add legend entries
            foreach (var (trackName, curves) in trackCurves)
            {
                foreach (var (curveTitle, color, min, max, thickness, _) in curves)
                {
                    var dummyScatter = plt.Add.ScatterLine(new double[] { }, new double[] { });
                    dummyScatter.Color = ScottPlot.Color.FromHex(color);
                    dummyScatter.LineWidth = (float)thickness;
                    dummyScatter.LegendText = $"{trackName} - {curveTitle} [{min:F1} to {max:F1}]";
                }
            }

            plt.ShowLegend(Edge.Top);
            plt.HideGrid();
            plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D3D3D3");
            plt.ShowGrid();
        }

        private (List<string> trackNames, List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)>) ParseTracks(JsonElement tracks, int pointsPerTrack)
        {
            var trackNames = new List<string>();
            var trackCurves = new List<(string, List<(string, string, double, double, double, List<double>)>)>();
            int count = 0;
            foreach (var track in tracks.EnumerateArray())
            {
                var trackType = track.TryGetProperty("trackType", out var tt) ? tt.GetString() : null;
                var checkedTrack = track.TryGetProperty("checked", out var chk) && chk.GetBoolean();
                var trackName = track.TryGetProperty("name", out var tn) ? tn.GetString() : "";

                if (!checkedTrack || trackType != "curve" || trackName?.Contains("Time Summary", StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                trackNames.Add(trackName ?? "");

                var curvesList = new List<(string, string, double, double, double, List<double>)>();
                if (track.TryGetProperty("curves", out var curves))
                {
                    foreach (var curve in curves.EnumerateArray())
                    {
                        var curveChecked = curve.TryGetProperty("checked", out var cc) && cc.GetBoolean();
                        if (!curveChecked) continue;

                        var curveTitle = curve.TryGetProperty("title", out var ct) ? ct.GetString() ?? "Curve" : "Curve";
                        var color = curve.TryGetProperty("colour", out var col) ? col.GetString() ?? "#000000" : "#000000";
                        var min = curve.TryGetProperty("manualScaleMin", out var cmin) ? cmin.GetDouble() : 0;
                        var max = curve.TryGetProperty("manualScaleMax", out var cmax) ? cmax.GetDouble() : 100;
                        var thickness = curve.TryGetProperty("lineThickness", out var th) ? th.GetDouble() : 2;

                        var data = GenerateSampleData(min, max, pointsPerTrack);
                        curvesList.Add((curveTitle, color, min, max, thickness, data));
                    }
                }
                trackCurves.Add((trackName ?? "", curvesList));
            }
            return (trackNames, trackCurves);
        }

        private List<double> GenerateSampleData(double min, double max, int count = 100)
        {
            var data = new List<double>();
            var rand = new Random();
            
            // Generate time series data with various patterns
            double range = max - min;
            double midPoint = min + (range / 2);
            
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / count; // Normalized time [0, 1]
                double value = 0;
                
                // Combine multiple patterns to create realistic time series
                // 1. Trend component (gradual increase/decrease)
                double trend = (rand.NextDouble() - 0.5) * range * 0.3 * t;
                
                // 2. Seasonal/cyclic component (sine wave)
                double seasonal = Math.Sin(2 * Math.PI * t * (2 + rand.Next(0, 3))) * range * 0.25;
                
                // 3. Secondary cycle (different frequency)
                double secondaryCycle = Math.Cos(2 * Math.PI * t * (4 + rand.Next(0, 2))) * range * 0.15;
                
                // 4. Random noise
                double noise = (rand.NextDouble() - 0.5) * range * 0.1;
                
                // Combine all components
                value = midPoint + trend + seasonal + secondaryCycle + noise;
                
                // Clamp to min/max range
                value = Math.Max(min, Math.Min(max, value));
                
                data.Add(value);
            }
            
            return data;
        }
    }
}
