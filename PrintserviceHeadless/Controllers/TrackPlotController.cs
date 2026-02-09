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
            int pointsPerTrack = 200;

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

            // Reserve space for track headers based on orientation
            int headerReservedWidth = orientation == 1 ? 100 : 0;
            int headerReservedHeight = orientation == 0 ? 80 : 0;

            marginLeft += headerReservedWidth;
            marginTop += headerReservedHeight;

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

                for (int trackBand = 0; trackBand < trackCurves.Count; trackBand++)
                {
                    var (_, curves) = trackCurves[trackBand];

                    // Draw track header FIRST in reserved space
                    DrawTrackCurveHeader(canvas, marginLeft, marginTop, plotWidth, bandHeight, trackBand, curves, trackNames[trackBand], true, headerReservedWidth);

                    // Then draw curves in remaining space
                    for (int curveIdx = 0; curveIdx < curves.Count; curveIdx++)
                    {
                        var (curveTitle, color, min, max, thickness, data) = curves[curveIdx];
                        if (curveIdx == 0)
                        {
                            var fillColor = GetRandomSKColor();
                            DrawHorizontalCurve(canvas, marginLeft, marginTop, plotWidth, bandHeight, trackBand, pointsPerTrack, min, max, thickness, color, data, true, fillColor);
                        }
                        else
                        {
                            DrawHorizontalCurve(canvas, marginLeft, marginTop, plotWidth, bandHeight, trackBand, pointsPerTrack, min, max, thickness, color, data);
                        }
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

                for (int trackBand = 0; trackBand < trackCurves.Count; trackBand++)
                {
                    var (_, curves) = trackCurves[trackBand];

                    // Draw track header FIRST in reserved space
                    DrawTrackCurveHeader(canvas, marginLeft, marginTop, bandWidth, plotHeight, trackBand, curves, trackNames[trackBand], false, headerReservedHeight);

                    // Then draw curves in remaining space
                    for (int curveIdx = 0; curveIdx < curves.Count; curveIdx++)
                    {
                        var (curveTitle, color, min, max, thickness, data) = curves[curveIdx];
                        if (curveIdx == 0)
                        {
                            var fillColor = GetRandomSKColor();
                            DrawVerticalCurve(canvas, marginLeft, marginTop, bandWidth, plotHeight, trackBand, pointsPerTrack, min, max, thickness, color, data, true, fillColor);
                        }
                        else
                        {
                            DrawVerticalCurve(canvas, marginLeft, marginTop, bandWidth, plotHeight, trackBand, pointsPerTrack, min, max, thickness, color, data);
                        }
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

        private void DrawTrackCurveHeader(
            SKCanvas canvas,
            int marginLeft,
            int marginTop,
            float bandSize,
            float totalSize,
            int trackBand,
            List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)> curves,
            string trackName,
            bool isHorizontal,
            int headerSpace)
        {
            if (isHorizontal)
            {
                // Draw horizontal text on the left side in reserved header space
                float trackStartY = marginTop + totalSize * trackBand;
                float xPosition = marginLeft - headerSpace + 5;

                canvas.Save();

                // Translate to the specific position for THIS track band
                canvas.Translate(xPosition, trackStartY);

                float availableHeight = totalSize - 10; // Leave some padding
                float availableWidth = headerSpace - 10;
                float itemHeight = 12;

                var headerPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 8,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
                };

                int curveCount = curves.Count;

                // Adaptive rendering strategy for horizontal mode
                if (curveCount <= 5)
                {
                    // Single column - draw all curves
                    float currentY = 5;

                    // Draw track header
                    using var headerBoldPaint = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 9,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                    };

                    canvas.DrawRect(0, currentY - 7, 6, 6, headerBoldPaint);
                    currentY += itemHeight;

                    // Draw all curves
                    foreach (var (curveTitle, color, _, _, _, _) in curves)
                    {
                        using var boxPaint = new SKPaint
                        {
                            Color = SKColor.Parse(color),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawRect(0, currentY - 7, 6, 6, boxPaint);

                        string displayText = TruncateLabel(curveTitle, availableWidth - 10, headerPaint);
                        canvas.DrawText(displayText, 10, currentY, headerPaint);
                        currentY += itemHeight;
                    }
                }
                else if (curveCount <= 10)
                {
                    // Two columns layout
                    int maxRowsPerColumn = Math.Max(1, (int)(availableHeight / itemHeight));
                    int numberOfColumns = (int)Math.Ceiling((double)(curveCount + 1) / maxRowsPerColumn); // +1 for header
                    float columnWidth = availableWidth / Math.Max(numberOfColumns, 1);

                    float currentY = 5;
                    float currentX = 0;
                    int itemsInCurrentColumn = 0;

                    // Draw track header
                    using var headerBoldPaint = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 9,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                    };

                    canvas.DrawRect(currentX, currentY - 7, 6, 6, headerBoldPaint);
                    string trackHeader = TruncateLabel($"Track {trackBand + 1}", columnWidth - 12, headerBoldPaint);
                    canvas.DrawText(trackHeader, currentX + 10, currentY, headerBoldPaint);
                    currentY += itemHeight;
                    itemsInCurrentColumn++;

                    // Draw curves in columns
                    foreach (var (curveTitle, color, _, _, _, _) in curves)
                    {
                        if (itemsInCurrentColumn >= maxRowsPerColumn && currentX + columnWidth < availableWidth)
                        {
                            currentX += columnWidth;
                            currentY = 5;
                            itemsInCurrentColumn = 0;
                        }

                        using var boxPaint = new SKPaint
                        {
                            Color = SKColor.Parse(color),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawRect(currentX, currentY - 6, 5, 5, boxPaint);

                        string displayText = TruncateLabel(curveTitle, columnWidth - 12, headerPaint);
                        canvas.DrawText(displayText, currentX + 8, currentY, headerPaint);
                        currentY += itemHeight;
                        itemsInCurrentColumn++;
                    }
                }
                else
                {
                    // Summary mode - too many curves
                    float currentY = 5;

                    using var summaryPaint = new SKPaint
                    {
                        Color = SKColors.Black,
                        TextSize = 9,
                        IsAntialias = true,
                        Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                    };

                    string summary = TruncateLabel($"Track {trackBand + 1}: {curves.Count} curves", availableWidth - 5, summaryPaint);
                    canvas.DrawText(summary, 0, currentY, summaryPaint);
                    currentY += itemHeight;

                    // Show first 3 curves
                    int maxToShow = Math.Min(3, curves.Count);
                    for (int i = 0; i < maxToShow; i++)
                    {
                        var (curveTitle, color, _, _, _, _) = curves[i];
                        using var boxPaint = new SKPaint
                        {
                            Color = SKColor.Parse(color),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawRect(0, currentY - 6, 5, 5, boxPaint);

                        string displayText = TruncateLabel(curveTitle, availableWidth - 10, headerPaint);
                        canvas.DrawText(displayText, 8, currentY, headerPaint);
                        currentY += itemHeight;
                    }

                    if (curves.Count > maxToShow)
                    {
                        string moreText = TruncateLabel($"... +{curves.Count - maxToShow} more", availableWidth - 5, headerPaint);
                        canvas.DrawText(moreText, 0, currentY, headerPaint);
                    }
                }

                headerPaint.Dispose();
                canvas.Restore();
            }
            else // Vertical orientation
            {
                float currentX = marginLeft + bandSize * trackBand + 5;
                float currentY = marginTop - headerSpace + 10;
                float availableWidth = bandSize - 10;
                float availableHeight = headerSpace - 20;

                var headerPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    TextSize = 9,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal)
                };

                int curveCount = curves.Count;
                float itemHeight = 12;

                // Adaptive rendering strategy
                if (curveCount <= 5)
                {
                    // Single column - draw all curves
                    float textY = currentY;
                    foreach (var (curveTitle, color, _, _, _, _) in curves)
                    {
                        using var boxPaint = new SKPaint
                        {
                            Color = SKColor.Parse(color),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawRect(currentX, textY - 7, 6, 6, boxPaint);

                        string displayText = TruncateLabel(curveTitle, availableWidth - 15, headerPaint);
                        canvas.DrawText(displayText, currentX + 10, textY, headerPaint);
                        textY += itemHeight;
                    }
                }
                else if (curveCount <= 10)
                {
                    // Two columns layout
                    int maxRowsPerColumn = Math.Max(1, (int)(availableHeight / itemHeight));
                    int numberOfColumns = (int)Math.Ceiling((double)curveCount / maxRowsPerColumn);
                    float columnWidth = availableWidth / numberOfColumns;

                    int curveIndex = 0;
                    for (int col = 0; col < numberOfColumns && curveIndex < curves.Count; col++)
                    {
                        float colX = currentX + (col * columnWidth);
                        float textY = currentY;
                        int itemsInColumn = Math.Min(maxRowsPerColumn, curves.Count - curveIndex);

                        for (int row = 0; row < itemsInColumn; row++)
                        {
                            var (curveTitle, color, _, _, _, _) = curves[curveIndex++];
                            using var boxPaint = new SKPaint
                            {
                                Color = SKColor.Parse(color),
                                Style = SKPaintStyle.Fill,
                                IsAntialias = true
                            };
                            canvas.DrawRect(colX, textY - 6, 5, 5, boxPaint);

                            string displayText = TruncateLabel(curveTitle, columnWidth - 12, headerPaint);
                            canvas.DrawText(displayText, colX + 8, textY, headerPaint);
                            textY += itemHeight;
                        }
                    }
                }
                else
                {
                    // Summary mode - too many curves
                    string summary = $"Track {trackBand + 1}: {curves.Count} curves";
                    canvas.DrawText(summary, currentX, currentY, headerPaint);

                    float textY = currentY + itemHeight;
                    int maxToShow = Math.Min(3, curves.Count);

                    for (int i = 0; i < maxToShow; i++)
                    {
                        var (curveTitle, color, _, _, _, _) = curves[i];
                        using var boxPaint = new SKPaint
                        {
                            Color = SKColor.Parse(color),
                            Style = SKPaintStyle.Fill,
                            IsAntialias = true
                        };
                        canvas.DrawRect(currentX, textY - 6, 5, 5, boxPaint);

                        string displayText = TruncateLabel(curveTitle, availableWidth - 12, headerPaint);
                        canvas.DrawText(displayText, currentX + 8, textY, headerPaint);
                        textY += itemHeight;
                    }

                    if (curves.Count > maxToShow)
                    {
                        canvas.DrawText($"... +{curves.Count - maxToShow} more", currentX, textY, headerPaint);
                    }
                }

                headerPaint.Dispose();
            }
        }

        private void DrawHorizontalAxesAndGrid(SKCanvas canvas, int marginLeft, int marginTop, int plotWidth, int plotHeight, float bandHeight, int nTracks, int pointsPerTrack, List<string> trackNames, SKPaint labelPaint, SKPaint gridPaint, SKPaint axisPaint)
        {
            float minFontSize = 8;
            float maxFontSize = 18;
            float fontSize = Math.Max(minFontSize, Math.Min(maxFontSize, bandHeight * 0.6f));
            labelPaint.TextSize = fontSize;

            float minSpacing = fontSize * 1.5f;
            int maxLabels = 20; // You can adjust this for your chart

            var labelIndices = GetLabelIndices(nTracks, bandHeight, 0, minSpacing, maxLabels);

            float maxLabelWidth = marginLeft - 10;

            foreach (int i in labelIndices)
            {
                float y = marginTop + bandHeight * (i + 0.5f);
                string displayLabel = TruncateLabel(trackNames[i], maxLabelWidth, labelPaint);
                canvas.DrawText(displayLabel, 10, y + fontSize / 2, labelPaint);
            }

            // Draw grid lines for all bands
            for (int i = 0; i < nTracks; i++)
            {
                if (i > 0)
                    canvas.DrawLine(marginLeft, marginTop + bandHeight * i, marginLeft + plotWidth, marginTop + bandHeight * i, gridPaint);
            }

            // Draw X axis labels and grid lines as before
            for (int i = 0; i <= pointsPerTrack; i += 10)
            {
                float x = marginLeft + plotWidth * i / pointsPerTrack;
                canvas.DrawLine(x, marginTop, x, marginTop + plotHeight, gridPaint);
                canvas.DrawText(i.ToString(), x - 10, marginTop + plotHeight + 30, labelPaint);
            }
            canvas.DrawLine(marginLeft, marginTop, marginLeft, marginTop + plotHeight, axisPaint);
            canvas.DrawLine(marginLeft, marginTop + plotHeight, marginLeft + plotWidth, marginTop + plotHeight, axisPaint);
        }

        private void DrawVerticalAxesAndGrid(SKCanvas canvas, int marginLeft, int marginTop, int plotWidth, int plotHeight, float bandWidth,
            int nTracks, int pointsPerTrack, List<string> trackNames, SKPaint labelPaint, SKPaint gridPaint, SKPaint axisPaint, int height, int marginBottom)
        {
            float minFontSize = 8;
            float maxFontSize = 18;
            float fontSize = Math.Max(minFontSize, Math.Min(maxFontSize, bandWidth * 0.6f));
            labelPaint.TextSize = fontSize;

            // Calculate which Y-axis labels to show based on available space
            float minSpacing = labelPaint.TextSize * 2f;
            int maxLabels = 20;

            var labelIndices = GetLabelIndices(nTracks, bandWidth, 0, minSpacing, maxLabels);

            float maxLabelWidth = bandWidth - 4;

            foreach (int i in labelIndices)
            {
                float x = marginLeft + bandWidth * (i + 0.5f);
                string displayLabel = TruncateLabel(trackNames[i], maxLabelWidth, labelPaint);
                canvas.DrawText(displayLabel, x - labelPaint.MeasureText(displayLabel) / 2, height - marginBottom + fontSize + 2, labelPaint);
            }

            // Draw grid lines for all bands
            for (int i = 0; i < nTracks; i++)
            {
                if (i > 0)
                    canvas.DrawLine(marginLeft + bandWidth * i, marginTop, marginLeft + bandWidth * i, marginTop + plotHeight, gridPaint);
            }

            // Calculate optimal increment for Y-axis labels
            int increment = CalculateAxisIncrement(pointsPerTrack, plotHeight, labelPaint.TextSize);

            for (int i = 0; i <= pointsPerTrack; i += increment)
            {
                float y = marginTop + plotHeight * i / pointsPerTrack;
                canvas.DrawLine(marginLeft, y, marginLeft + plotWidth, y, gridPaint);
                canvas.DrawText(i.ToString(), marginLeft - 40, y + 6, labelPaint);
            }
            canvas.DrawLine(marginLeft, marginTop, marginLeft + plotWidth, marginTop, axisPaint);
            canvas.DrawLine(marginLeft, marginTop, marginLeft, marginTop + plotHeight, axisPaint);
        }

        private void DrawHorizontalCurve(
            SKCanvas canvas, int marginLeft, int marginTop, int plotWidth, float bandHeight, int trackBand,
            int pointsPerTrack, double min, double max, double thickness, string color, List<double> data,
            bool fill = false, SKColor? fillColor = null)
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

            if (fill && fillColor.HasValue)
            {
                // Close the path to the bottom of the band for filling
                float lastX = marginLeft + plotWidth * (data.Count - 1) / pointsPerTrack;
                float baseY = marginTop + bandHeight * (trackBand + 1);
                path.LineTo(lastX, baseY);
                path.LineTo(marginLeft, baseY);
                path.Close();

                using var fillPaint = new SKPaint
                {
                    Color = fillColor.Value,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                    ColorF = new SKColor(fillColor.Value.Red, fillColor.Value.Green, fillColor.Value.Blue, 128) // semi-transparent
                };
                canvas.DrawPath(path, fillPaint);
            }

            canvas.DrawPath(path, curvePaint);
        }

        private void DrawVerticalCurve(
            SKCanvas canvas, int marginLeft, int marginTop, float bandWidth, int plotHeight, int trackBand,
            int pointsPerTrack, double min, double max, double thickness, string color, List<double> data,
            bool fill = false, SKColor? fillColor = null)
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

            if (fill && fillColor.HasValue)
            {
                // Close the path to the right edge of the band for filling
                float baseX = marginLeft + bandWidth * (trackBand + 1);
                float lastY = marginTop + plotHeight * (data.Count - 1) / pointsPerTrack;
                path.LineTo(baseX, lastY);
                path.LineTo(baseX, marginTop);
                path.LineTo(marginLeft + bandWidth * trackBand, marginTop);
                path.Close();

                using var fillPaint = new SKPaint
                {
                    Color = fillColor.Value,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                canvas.DrawPath(path, fillPaint);
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

        // Add this helper method to your controller:
        private SKColor GetRandomSKColor()
        {
            var rand = new Random(Guid.NewGuid().GetHashCode());
            return new SKColor(
                (byte)rand.Next(64, 256),
                (byte)rand.Next(64, 256),
                (byte)rand.Next(64, 256),
                60 // semi-transparent
            );
        }

        private List<int> GetLabelIndices(int nBands, float bandSize, float margin, float minSpacing, int maxLabels)
        {
            // Estimate how many labels can fit
            int possibleLabels = (int)((nBands * bandSize - 2 * margin) / minSpacing);
            int labelCount = Math.Min(possibleLabels, maxLabels);
            labelCount = Math.Max(labelCount, 1);

            var indices = new List<int>();
            if (labelCount == 1)
            {
                indices.Add(nBands / 2);
            }
            else
            {
                for (int i = 0; i < labelCount; i++)
                {
                    int idx = (int)Math.Round((double)(i * (nBands - 1) / (labelCount - 1)));
                    indices.Add(idx);
                }
            }
            return indices;
        }

        private string TruncateLabel(string label, float maxWidth, SKPaint paint)
        {
            if (paint.MeasureText(label) <= maxWidth) return label;
            while (label.Length > 0 && paint.MeasureText(label + "...") > maxWidth)
                label = label.Substring(0, label.Length - 1);
            return label + "...";
        }

        private int CalculateAxisIncrement(int maxValue, int availableSpace, float fontSize)
        {
            // Minimum spacing between labels (1.5x font size for readability)
            float minSpacing = fontSize * 1.5f;

            // Maximum number of labels that can fit
            int maxLabels = (int)(availableSpace / minSpacing);
            maxLabels = Math.Max(maxLabels, 2); // At least 2 labels (start and end)

            // Calculate raw increment
            int rawIncrement = maxValue / (maxLabels - 1);

            // Round to a "nice" number (10, 20, 50, 100, 200, 500, 1000, etc.)
            int[] niceNumbers = { 1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000 };

            int increment = niceNumbers[0];
            foreach (int nice in niceNumbers)
            {
                if (nice >= rawIncrement)
                {
                    increment = nice;
                    break;
                }
                increment = nice;
            }

            // Ensure at least one increment
            return Math.Max(increment, 1);
        }
    }

    public class PlotTrackRequest
    {
        public string WidgetsAsJsonString { get; set; }
    }
}