using Microsoft.AspNetCore.Mvc;
using OxyPlot;
using OxyPlot.ImageSharp;
using OxyPlot.Series;
using OxyPlot.Axes;
using System.Text.Json;
using SkiaSharp;


namespace PrintserviceHeadless.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TrackPlotController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        [HttpPost("plot")]
        public IActionResult PlotTracks([FromBody] PlotTrackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WidgetsAsJsonString))
                return BadRequest("widgetsAsJsonString is required.");

            var widgets = JsonDocument.Parse(request.WidgetsAsJsonString).RootElement;
            var tracks = widgets[0].GetProperty("configuration")
                .GetProperty("widgetConfiguration")
                .GetProperty("tracks");

            int width = 1200, height = 800;
            var plotModel = new PlotModel { Title = "Tracks (Grouped)" };
            plotModel.Background = OxyColors.White;

            // We'll use a categorical Y axis for track bands
            var yAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Track",
                GapWidth = 0.2
            };
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Index",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            plotModel.Axes.Add(yAxis);
            plotModel.Axes.Add(xAxis);

            int pointsPerTrack = 50;
            int trackBand = 0;
            var trackNames = new List<string>();

            foreach (var track in tracks.EnumerateArray())
            {
                var trackType = track.TryGetProperty("trackType", out var tt) ? tt.GetString() : null;
                var checkedTrack = track.TryGetProperty("checked", out var chk) && chk.GetBoolean();
                var trackName = track.TryGetProperty("name", out var tn) ? tn.GetString() : "";

                // Ignore time summary tracks
                if (!checkedTrack || trackType != "curve" || trackName.Contains("Time Summary", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Add track name to Y axis
                yAxis.Labels.Add(trackName);

                if (track.TryGetProperty("curves", out var curves))
                {
                    foreach (var curve in curves.EnumerateArray())
                    {
                        var curveChecked = curve.TryGetProperty("checked", out var cc) && cc.GetBoolean();
                        if (!curveChecked) continue;

                        var curveTitle = curve.TryGetProperty("title", out var ct) ? ct.GetString() : "Curve";
                        var color = curve.TryGetProperty("colour", out var col) ? col.GetString() : "#000000";
                        var min = curve.TryGetProperty("manualScaleMin", out var cmin) ? cmin.GetDouble() : 0;
                        var max = curve.TryGetProperty("manualScaleMax", out var cmax) ? cmax.GetDouble() : 100;
                        var thickness = curve.TryGetProperty("lineThickness", out var th) ? th.GetDouble() : 2;

                        var series = new LineSeries
                        {
                            Title = $"{trackName} - {curveTitle}",
                            Color = OxyColor.Parse(color),
                            StrokeThickness = thickness
                        };

                        // Generate random data in [min, max]
                        var data = GenerateSampleData(min, max, pointsPerTrack);
                        for (int i = 0; i < data.Count; i++)
                        {
                            // Y value is the track band + normalized value (to fit in band)
                            double y = trackBand + (data[i] - min) / (max - min + 1e-9) * 0.8; // 0.8 to keep within band
                            series.Points.Add(new DataPoint(i, y));
                        }

                        plotModel.Series.Add(series);
                    }
                }

                // Draw a horizontal separator after each track except the last
                trackBand++;
                if (trackBand < tracks.GetArrayLength())
                {
                    var sepLine = new LineSeries
                    {
                        Color = OxyColors.Gray,
                        StrokeThickness = 1,
                        LineStyle = LineStyle.Dash
                    };
                    sepLine.Points.Add(new DataPoint(0, trackBand - 0.5));
                    sepLine.Points.Add(new DataPoint(pointsPerTrack, trackBand - 0.5));
                    plotModel.Series.Add(sepLine);
                }
            }

            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            PngExporter.Export(plotModel, tempFile, width, height);
            var imageBytes = System.IO.File.ReadAllBytes(tempFile);
            System.IO.File.Delete(tempFile);
            return File(imageBytes, "image/png");
        }

        [HttpPost("plot-skiasharp")]
        public IActionResult PlotTracksSkiaSharp([FromBody] PlotTrackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WidgetsAsJsonString))
                return BadRequest("widgetsAsJsonString is required.");

            var widgets = JsonDocument.Parse(request.WidgetsAsJsonString).RootElement;
            var config = widgets[0].GetProperty("configuration").GetProperty("widgetConfiguration");
            var tracks = config.GetProperty("tracks");
            int orientation = config.TryGetProperty("orientation", out var orientElem) ? orientElem.GetInt32() : 1; // default to horizontal

            int width = 1200, height = 800;
            int pointsPerTrack = 50;

            var (trackNames, trackCurves) = ParseTracks(tracks, pointsPerTrack);

            using var image = DrawSkiaChart(width, height, pointsPerTrack, trackNames, trackCurves, orientation);
            using var imagedata = image.Encode(SKEncodedImageFormat.Png, 100);
            return File(imagedata.ToArray(), "image/png");
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

                if (!checkedTrack || trackType != "curve" || trackName.Contains("Time Summary", StringComparison.OrdinalIgnoreCase))
                    continue;

                trackNames.Add(trackName);

                var curvesList = new List<(string, string, double, double, double, List<double>)>();
                if (track.TryGetProperty("curves", out var curves))
                {
                    foreach (var curve in curves.EnumerateArray())
                    {
                        var curveChecked = curve.TryGetProperty("checked", out var cc) && cc.GetBoolean();
                        if (!curveChecked) continue;

                        var curveTitle = curve.TryGetProperty("title", out var ct) ? ct.GetString() : "Curve";
                        var color = curve.TryGetProperty("colour", out var col) ? col.GetString() : "#000000";
                        var min = curve.TryGetProperty("manualScaleMin", out var cmin) ? cmin.GetDouble() : 0;
                        var max = curve.TryGetProperty("manualScaleMax", out var cmax) ? cmax.GetDouble() : 100;
                        var thickness = curve.TryGetProperty("lineThickness", out var th) ? th.GetDouble() : 2;

                        var data = GenerateSampleData(min, max, pointsPerTrack);
                        curvesList.Add((curveTitle, color, min, max, thickness, data));
                    }
                }
                trackCurves.Add((trackName, curvesList));
            }
            return (trackNames, trackCurves);
        }

        private SKImage DrawSkiaChart(
            int width, int height, int pointsPerTrack,
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves,
            int orientation // 0 = vertical, 1 = horizontal
        )
        {
            int marginLeft = 120, marginBottom = 60, marginTop = 40, marginRight = 40;
            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsAntialias = true };
            var labelPaint = new SKPaint { Color = SKColors.Black, TextSize = 18, IsAntialias = true };
            var gridPaint = new SKPaint { Color = SKColors.LightGray, StrokeWidth = 1, IsAntialias = true };

            int plotWidth = width - marginLeft - marginRight;
            int plotHeight = height - marginTop - marginBottom;
            int nTracks = trackNames.Count;

            if (orientation == 1) // Horizontal
            {
                float bandHeight = plotHeight / Math.Max(nTracks, 1);

                DrawHorizontalAxesAndGrid(canvas, marginLeft, marginTop, plotWidth, plotHeight, bandHeight, nTracks, pointsPerTrack, trackNames, labelPaint, gridPaint, axisPaint);

                // Draw curves
                for (int trackBand = 0; trackBand < trackCurves.Count; trackBand++)
                {
                    var (_, curves) = trackCurves[trackBand];
                    foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                    {
                        DrawHorizontalCurve(canvas, marginLeft, marginTop, plotWidth, bandHeight, trackBand, pointsPerTrack, min, max, thickness, color, data);
                    }
                    // Separator
                    if (trackBand < trackCurves.Count - 1)
                    {
                        DrawHorizontalSeparator(canvas, marginLeft, plotWidth, marginTop, bandHeight, trackBand);
                    }
                }
            }
            else // Vertical
            {
                float bandWidth = plotWidth / Math.Max(nTracks, 1);

                DrawVerticalAxesAndGrid(canvas, marginLeft, marginTop, plotWidth, plotHeight, bandWidth, nTracks, pointsPerTrack, trackNames, labelPaint, gridPaint, axisPaint, height, marginBottom);

                // Draw curves
                for (int trackBand = 0; trackBand < trackCurves.Count; trackBand++)
                {
                    var (_, curves) = trackCurves[trackBand];
                    foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                    {
                        DrawVerticalCurve(canvas, marginLeft, marginTop, bandWidth, plotHeight, trackBand, pointsPerTrack, min, max, thickness, color, data);
                    }
                    // Separator
                    if (trackBand < trackCurves.Count - 1)
                    {
                        DrawVerticalSeparator(canvas, marginLeft, marginTop, bandWidth, plotHeight, trackBand);
                    }
                }
            }

            return SKImage.FromBitmap(bitmap);
        }

        private void DrawHorizontalAxesAndGrid(SKCanvas canvas, int marginLeft, int marginTop, int plotWidth, int plotHeight, float bandHeight, int nTracks, int pointsPerTrack, List<string> trackNames, SKPaint labelPaint, SKPaint gridPaint, SKPaint axisPaint)
        {
            for (int i = 0; i < nTracks; i++)
            {
                float y = marginTop + bandHeight * (i + 0.5f);
                canvas.DrawText(trackNames[i], 10, y + 6, labelPaint);
                if (i > 0)
                    canvas.DrawLine(marginLeft, marginTop + bandHeight * i, marginLeft + plotWidth, marginTop + bandHeight * i, gridPaint);
            }
            for (int i = 0; i <= pointsPerTrack; i += 10)
            {
                float x = marginLeft + plotWidth * i / pointsPerTrack;
                canvas.DrawLine(x, marginTop, x, marginTop + plotHeight, gridPaint);
                canvas.DrawText(i.ToString(), x - 10, marginTop + plotHeight + 30, labelPaint);
            }
            canvas.DrawLine(marginLeft, marginTop, marginLeft, marginTop + plotHeight, axisPaint);
            canvas.DrawLine(marginLeft, marginTop + plotHeight, marginLeft + plotWidth, marginTop + plotHeight, axisPaint);
        }

        private void DrawVerticalAxesAndGrid(SKCanvas canvas, int marginLeft, int marginTop, int plotWidth, int plotHeight, float bandWidth, int nTracks, int pointsPerTrack, List<string> trackNames, SKPaint labelPaint, SKPaint gridPaint, SKPaint axisPaint, int height, int marginBottom)
        {
            for (int i = 0; i < nTracks; i++)
            {
                float x = marginLeft + bandWidth * (i + 0.5f);
                canvas.DrawText(trackNames[i], x - 30, height - marginBottom + 30, labelPaint);
                if (i > 0)
                    canvas.DrawLine(marginLeft + bandWidth * i, marginTop, marginLeft + bandWidth * i, marginTop + plotHeight, gridPaint);
            }
            for (int i = 0; i <= pointsPerTrack; i += 10)
            {
                float y = marginTop + plotHeight * i / pointsPerTrack;
                canvas.DrawLine(marginLeft, y, marginLeft + plotWidth, y, gridPaint);
                canvas.DrawText(i.ToString(), marginLeft - 40, y + 6, labelPaint);
            }
            canvas.DrawLine(marginLeft, marginTop, marginLeft + plotWidth, marginTop, axisPaint);
            canvas.DrawLine(marginLeft, marginTop, marginLeft, marginTop + plotHeight, axisPaint);
        }

        private void DrawHorizontalCurve(SKCanvas canvas, int marginLeft, int marginTop, int plotWidth, float bandHeight, int trackBand, int pointsPerTrack, double min, double max, double thickness, string color, List<double> data)
        {
            var curvePaint = new SKPaint
            {
                Color = SKColor.Parse(color),
                StrokeWidth = (float)thickness,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            var path = new SKPath();
            for (int i = 0; i < data.Count; i++)
            {
                double yNorm = (data[i] - min) / (max - min + 1e-9) * 0.8;
                float y = marginTop + bandHeight * trackBand + bandHeight * (float)yNorm;
                float x = marginLeft + plotWidth * i / pointsPerTrack;
                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }
            canvas.DrawPath(path, curvePaint);
        }

        private void DrawVerticalCurve(SKCanvas canvas, int marginLeft, int marginTop, float bandWidth, int plotHeight, int trackBand, int pointsPerTrack, double min, double max, double thickness, string color, List<double> data)
        {
            var curvePaint = new SKPaint
            {
                Color = SKColor.Parse(color),
                StrokeWidth = (float)thickness,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            var path = new SKPath();
            for (int i = 0; i < data.Count; i++)
            {
                double xNorm = (data[i] - min) / (max - min + 1e-9) * 0.8;
                float x = marginLeft + bandWidth * trackBand + bandWidth * (float)xNorm;
                float y = marginTop + plotHeight * i / pointsPerTrack;
                if (i == 0)
                    path.MoveTo(x, y);
                else
                    path.LineTo(x, y);
            }
            canvas.DrawPath(path, curvePaint);
        }

        private void DrawHorizontalSeparator(SKCanvas canvas, int marginLeft, int plotWidth, int marginTop, float bandHeight, int trackBand)
        {
            var sepPaint = new SKPaint
            {
                Color = SKColors.Gray,
                StrokeWidth = 1,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 10, 10 }, 0)
            };
            float y = marginTop + bandHeight * (trackBand + 1);
            canvas.DrawLine(marginLeft, y, marginLeft + plotWidth, y, sepPaint);
        }

        private void DrawVerticalSeparator(SKCanvas canvas, int marginLeft, int marginTop, float bandWidth, int plotHeight, int trackBand)
        {
            var sepPaint = new SKPaint
            {
                Color = SKColors.Gray,
                StrokeWidth = 1,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 10, 10 }, 0)
            };
            float x = marginLeft + bandWidth * (trackBand + 1);
            canvas.DrawLine(x, marginTop, x, marginTop + plotHeight, sepPaint);
        }

        private List<double> GenerateSampleData(double min, double max, int count = 50)
        {
            var data = new List<double>();
            var rand = new Random();
            for (int i = 0; i < count; i++)
                data.Add(rand.NextDouble() * (max - min) + min);
            return data;
        }
    }

    public class PlotTrackRequest
    {
        public string WidgetsAsJsonString { get; set; }
    }
}