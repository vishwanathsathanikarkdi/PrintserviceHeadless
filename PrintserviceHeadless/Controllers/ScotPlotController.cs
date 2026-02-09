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
            int pointsPerTrack = 200;

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

        private void DrawScotPlotHorizontal(
            ScottPlot.Plot plt,
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves,
            int pointsPerTrack)
        {
            int nTracks = trackNames.Count;
            const double trackHeight = 1.0; // Uniform track height

            // Build custom tick labels with curve data stacked vertically
            var customLabels = new List<string>();
            foreach (var (trackName, curves) in trackCurves)
            {
                var labelBuilder = new System.Text.StringBuilder();
                labelBuilder.AppendLine(trackName);

                int maxCurvesToShow = 5; // Limit to avoid overflow
                int curveCount = 0;

                foreach (var (curveTitle, color, _, _, _, _) in curves)
                {
                    if (curveCount >= maxCurvesToShow)
                    {
                        int remaining = curves.Count - maxCurvesToShow;
                        labelBuilder.AppendLine($"  +{remaining} more...");
                        break;
                    }

                    // Truncate long curve titles
                    string displayTitle = curveTitle.Length > 20
                        ? curveTitle.Substring(0, 17) + "..."
                        : curveTitle;
                    labelBuilder.AppendLine($"  • {displayTitle}");
                    curveCount++;
                }

                customLabels.Add(labelBuilder.ToString().TrimEnd());
            }

            // Set Y axis to categorical with uniform spacing
            double[] yPositions = Enumerable.Range(0, nTracks).Select(i => i * trackHeight).ToArray();
            plt.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(yPositions, customLabels.ToArray());

            // Increase left margin to accommodate multi-line labels
            plt.Layout.Fixed(new ScottPlot.PixelPadding(200, 60, 60, 100));

            // Set X axis range with uniform track heights
            plt.Axes.SetLimits(0, pointsPerTrack, -0.5 * trackHeight, (nTracks - 0.5) * trackHeight);

            int trackBand = 0;
            foreach (var (trackName, curves) in trackCurves)
            {
                double trackBaseY = trackBand * trackHeight;
                bool isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Normalize data to fit within uniform track band
                    var xData = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();
                    var yData = data.Select(d =>
                    {
                        double yNorm = (d - min) / (max - min + 1e-9);
                        return trackBaseY + yNorm * trackHeight * 0.8; // 0.8 for padding within track
                    }).ToArray();

                    if (isFirstCurve)
                    {
                        // Fill the first curve
                        var fillYData = new List<double>(yData);
                        fillYData.Add(trackBaseY); // Bottom of band
                        fillYData.Insert(0, trackBaseY); // Bottom of band at start

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

                trackBand++;
            }

            // Don't show legend
            plt.HideGrid();
            plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D3D3D3");
            plt.ShowGrid();
        }

        private void DrawScotPlotVertical(
            ScottPlot.Plot plt,
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves,
            int pointsPerTrack)
        {
            int nTracks = trackNames.Count;
            const double trackWidth = 1.0; // Uniform track width

            // Build custom tick labels with curve data stacked vertically
            var customLabels = new List<string>();
            foreach (var (trackName, curves) in trackCurves)
            {
                var labelBuilder = new System.Text.StringBuilder();
                labelBuilder.AppendLine(trackName);

                int maxCurvesToShow = 3; // Fewer for vertical to avoid width overflow
                int curveCount = 0;

                foreach (var (curveTitle, color, _, _, _, _) in curves)
                {
                    if (curveCount >= maxCurvesToShow)
                    {
                        int remaining = curves.Count - maxCurvesToShow;
                        labelBuilder.AppendLine($"  +{remaining} more");
                        break;
                    }

                    // Truncate long curve titles more aggressively for vertical
                    string displayTitle = curveTitle.Length > 15
                        ? curveTitle.Substring(0, 12) + "..."
                        : curveTitle;
                    labelBuilder.AppendLine($"  • {displayTitle}");
                    curveCount++;
                }

                customLabels.Add(labelBuilder.ToString().TrimEnd());
            }

            // Set X axis to categorical with uniform spacing
            double[] xPositions = Enumerable.Range(0, nTracks).Select(i => i * trackWidth).ToArray();
            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(xPositions, customLabels.ToArray());

            // Increase bottom margin to accommodate multi-line labels
            plt.Layout.Fixed(new ScottPlot.PixelPadding(100, 60, 60, 150));

            // Set axis range with uniform track widths
            plt.Axes.SetLimits(-0.5 * trackWidth, (nTracks - 0.5) * trackWidth, 0, pointsPerTrack);

            int trackBand = 0;
            foreach (var (trackName, curves) in trackCurves)
            {
                double trackBaseX = trackBand * trackWidth;
                bool isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Normalize data to fit within uniform track band
                    var yData = Enumerable.Range(0, data.Count).Select(i => (double)i).ToArray();
                    var xData = data.Select(d =>
                    {
                        double xNorm = (d - min) / (max - min + 1e-9);
                        return trackBaseX + xNorm * trackWidth * 0.8; // 0.8 for padding within track
                    }).ToArray();

                    if (isFirstCurve)
                    {
                        // Fill the first curve
                        var fillXData = new List<double>(xData);
                        fillXData.Add(trackBaseX); // Left edge of band
                        fillXData.Insert(0, trackBaseX); // Left edge of band at start

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

                trackBand++;
            }

            // Don't show legend
            plt.HideGrid();
            plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#D3D3D3");
            plt.ShowGrid();
        }

        private (List<string> trackNames, List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)>) ParseTracks(JsonElement tracks, int pointsPerTrack)
        {
            var trackNames = new List<string>();
            var trackCurves = new List<(string, List<(string, string, double, double, double, List<double>)>)>();
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

        private List<double> GenerateSampleData(double min, double max, int count = 200)
        {
            var data = new List<double>();
            var rand = new Random();
            for (int i = 0; i < count; i++)
                data.Add(rand.NextDouble() * (max - min) + min);
            return data;
        }
    }
}
