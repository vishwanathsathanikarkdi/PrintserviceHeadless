using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace PrintserviceHeadless.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChartJsController : ControllerBase
    {
        [HttpPost("plot-chartjs")]
        public IActionResult PlotTracksChartJs([FromBody] PlotTrackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WidgetsAsJsonString))
                return BadRequest("widgetsAsJsonString is required.");

            var widgets = JsonDocument.Parse(request.WidgetsAsJsonString).RootElement;
            var config = widgets[0].GetProperty("configuration").GetProperty("widgetConfiguration");
            var tracks = config.GetProperty("tracks");
            int orientation = config.TryGetProperty("orientation", out var orientElem) ? orientElem.GetInt32() : 1; // default to horizontal

            int pointsPerTrack = 100;

            var (trackNames, trackCurves) = ParseTracks(tracks, pointsPerTrack);

            string html = orientation == 1
                ? GenerateHorizontalChartHtml(trackNames, trackCurves)
                : GenerateVerticalChartHtml(trackNames, trackCurves);

            var htmlBytes = Encoding.UTF8.GetBytes(html);
            return File(htmlBytes, "text/html", "tracks-chart.html");
        }

        private string GenerateHorizontalChartHtml(
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves)
        {
            var datasetsJson = new StringBuilder();
            int trackIndex = 0;

            foreach (var (trackName, curves) in trackCurves)
            {
                bool isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Normalize data to fit within track band (each track gets a unit height of 1.0)
                    var normalizedData = data.Select((d, i) =>
                    {
                        double normalized = (d - min) / (max - min + 1e-9);
                        normalized = Math.Max(0.0, Math.Min(1.0, normalized));
                        // Map to track band: trackIndex to trackIndex+1, with 0.1 and 0.9 margins
                        double yValue = trackIndex + 0.1 + (normalized * 0.8);
                        return $"{{x: {i}, y: {yValue:F4}}}";
                    });

                    var dataPoints = string.Join(",", normalizedData);

                    // Add fill for the first curve in each track
                    string fillConfig = isFirstCurve ? "true" : "false";
                    string fillColor = isFirstCurve ? $"'{color}33'" : "false";

                    datasetsJson.AppendLine($@"
        {{
            label: '{trackName} - {curveTitle}',
            data: [{dataPoints}],
            borderColor: '{color}',
            backgroundColor: {fillColor},
            borderWidth: {thickness},
            fill: {{
                target: {trackIndex + 0.1:F1},
                above: '{color}33'
            }},
            tension: 0.1,
            pointRadius: 0,
            yAxisID: 'y'
        }},");

                    isFirstCurve = false;
                }

                trackIndex++;
            }

            // Generate Y-axis tick labels and positions
            var yTickPositions = string.Join(", ", Enumerable.Range(0, trackCurves.Count).Select(i => $"{i + 0.5:F1}"));
            var yTickLabels = string.Join(", ", trackNames.Select(name => $"'{name}'"));

            var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Tracks Chart (Horizontal)</title>
    <script src=""https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation@3.0.1/dist/chartjs-plugin-annotation.min.js""></script>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 20px;
            background-color: #f5f5f5;
        }}
        .chart-container {{
            position: relative;
            width: 1200px;
            height: 800px;
            background-color: white;
            padding: 20px;
            margin: 0 auto;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        h1 {{
            text-align: center;
            color: #333;
        }}
    </style>
</head>
<body>
    <h1>Tracks (Grouped) - Horizontal</h1>
    <div class=""chart-container"">
        <canvas id=""tracksChart""></canvas>
    </div>

    <script>
        const ctx = document.getElementById('tracksChart').getContext('2d');
        
        const chart = new Chart(ctx, {{
            type: 'line',
            data: {{
                datasets: [
                    {datasetsJson}
                ]
            }},
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                scales: {{
                    x: {{
                        type: 'linear',
                        position: 'bottom',
                        title: {{
                            display: true,
                            text: 'Index',
                            font: {{
                                size: 14,
                                weight: 'bold'
                            }}
                        }},
                        grid: {{
                            color: '#D3D3D3'
                        }}
                    }},
                    y: {{
                        type: 'linear',
                        position: 'left',
                        min: -0.5,
                        max: {trackCurves.Count - 0.5},
                        ticks: {{
                            values: [{yTickPositions}],
                            callback: function(value, index, values) {{
                                const labels = [{yTickLabels}];
                                const tickIndex = Math.round(value - 0.5);
                                if (tickIndex >= 0 && tickIndex < labels.length && Math.abs(value - (tickIndex + 0.5)) < 0.01) {{
                                    return labels[tickIndex];
                                }}
                                return '';
                            }},
                            font: {{
                                size: 12,
                                weight: 'bold'
                            }}
                        }},
                        title: {{
                            display: true,
                            text: 'Tracks',
                            font: {{
                                size: 14,
                                weight: 'bold'
                            }}
                        }},
                        grid: {{
                            color: function(context) {{
                                // Draw separator lines between tracks
                                const value = context.tick.value;
                                for (let i = 0; i < {trackCurves.Count - 1}; i++) {{
                                    if (Math.abs(value - (i + 0.5)) < 0.01) {{
                                        return 'rgba(128, 128, 128, 0.5)';
                                    }}
                                }}
                                return '#E5E5E5';
                            }},
                            lineWidth: function(context) {{
                                const value = context.tick.value;
                                for (let i = 0; i < {trackCurves.Count - 1}; i++) {{
                                    if (Math.abs(value - (i + 0.5)) < 0.01) {{
                                        return 1;
                                    }}
                                }}
                                return 1;
                            }}
                        }}
                    }}
                }},
                plugins: {{
                    legend: {{
                        display: true,
                        position: 'top',
                        labels: {{
                            boxWidth: 15,
                            padding: 8,
                            font: {{
                                size: 10
                            }},
                            usePointStyle: false
                        }}
                    }},
                    title: {{
                        display: true,
                        text: 'Tracks (Grouped)',
                        font: {{
                            size: 18,
                            weight: 'bold'
                        }},
                        padding: {{
                            top: 10,
                            bottom: 20
                        }}
                    }},
                    tooltip: {{
                        mode: 'index',
                        intersect: false,
                        callbacks: {{
                            title: function(context) {{
                                return 'Index: ' + context[0].parsed.x;
                            }},
                            label: function(context) {{
                                return context.dataset.label + ': ' + context.parsed.y.toFixed(4);
                            }}
                        }}
                    }},
                    annotation: {{
                        annotations: {GenerateHorizontalSeparators(trackCurves.Count)}
                    }}
                }},
                interaction: {{
                    mode: 'nearest',
                    axis: 'x',
                    intersect: false
                }}
            }}
        }});
    </script>
</body>
</html>";

            return html;
        }

        private string GenerateHorizontalSeparators(int trackCount)
        {
            var separators = new StringBuilder();
            separators.AppendLine("{");

            for (int i = 0; i < trackCount - 1; i++)
            {
                double separatorY = i + 0.5;
                separators.AppendLine($@"
                    separator{i}: {{
                        type: 'line',
                        yMin: {separatorY:F1},
                        yMax: {separatorY:F1},
                        borderColor: 'rgba(128, 128, 128, 0.5)',
                        borderWidth: 1,
                        borderDash: [5, 5]
                    }}{(i < trackCount - 2 ? "," : "")}");
            }

            separators.AppendLine("}");
            return separators.ToString();
        }

        private string GenerateVerticalSeparators(int trackCount)
        {
            var separators = new StringBuilder();
            separators.AppendLine("{");

            for (int i = 0; i < trackCount - 1; i++)
            {
                double separatorX = i + 0.5;
                separators.AppendLine($@"
                    separator{i}: {{
                        type: 'line',
                        xMin: {separatorX:F1},
                        xMax: {separatorX:F1},
                        borderColor: 'rgba(128, 128, 128, 0.5)',
                        borderWidth: 1,
                        borderDash: [5, 5]
                    }}{(i < trackCount - 2 ? "," : "")}");
            }

            // Add markers
            separators.AppendLine(@",
                    line1: {
                        type: 'line',
                        yMin: 80,
                        yMax: 80,
                        borderColor: 'rgb(255, 0, 0)',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        label: {
                            display: true,
                            content: 'Marker 1',
                            position: 'start',
                            backgroundColor: 'rgba(255, 0, 0, 0.8)',
                            color: 'white',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        }
                    },
                    line2: {
                        type: 'line',
                        yMin: 90,
                        yMax: 90,
                        borderColor: 'rgb(0, 0, 255)',
                        borderWidth: 2,
                        borderDash: [5, 5],
                        label: {
                            display: true,
                            content: 'Marker 2',
                            position: 'start',
                            backgroundColor: 'rgba(0, 0, 255, 0.8)',
                            color: 'white',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        }
                    }");

            separators.AppendLine("}");
            return separators.ToString();
        }

        private string GenerateVerticalChartHtml(
            List<string> trackNames,
            List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> trackCurves)
        {
            var datasetsJson = new StringBuilder();
            int trackIndex = 0;

            foreach (var (trackName, curves) in trackCurves)
            {
                bool isFirstCurve = true;

                foreach (var (curveTitle, color, min, max, thickness, data) in curves)
                {
                    // Normalize data to fit within track band
                    var normalizedData = data.Select((d, i) =>
                    {
                        double normalized = (d - min) / (max - min + 1e-9);
                        normalized = Math.Max(0.0, Math.Min(1.0, normalized));
                        double xValue = trackIndex + 0.1 + (normalized * 0.8);
                        return $"{{x: {xValue:F4}, y: {i}}}";
                    });

                    var dataPoints = string.Join(",", normalizedData);

                    // Add fill for the first curve in each track
                    string fillColor = isFirstCurve ? $"'{color}33'" : "false";

                    datasetsJson.AppendLine($@"
        {{
            label: '{trackName} - {curveTitle}',
            data: [{dataPoints}],
            borderColor: '{color}',
            backgroundColor: {fillColor},
            borderWidth: {thickness},
            fill: {{
                target: {trackIndex + 0.1:F1},
                below: '{color}33'
            }},
            tension: 0.1,
            pointRadius: 0,
            xAxisID: 'x'
        }},");

                    isFirstCurve = false;
                }

                trackIndex++;
            }

            // Generate X-axis tick labels and positions
            var xTickPositions = string.Join(", ", Enumerable.Range(0, trackCurves.Count).Select(i => $"{i + 0.5:F1}"));
            var xTickLabels = string.Join(", ", trackNames.Select(name => $"'{name}'"));

            var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Tracks Chart (Vertical)</title>
    <script src=""https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation@3.0.1/dist/chartjs-plugin-annotation.min.js""></script>
    <style>
        body {{
            font-family: Arial, sans-serif;
            margin: 20px;
            background-color: #f5f5f5;
        }}
        .chart-container {{
            position: relative;
            width: 1200px;
            height: 800px;
            background-color: white;
            padding: 20px;
            margin: 0 auto;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        h1 {{
            text-align: center;
            color: #333;
        }}
    </style>
</head>
<body>
    <h1>Tracks (Grouped) - Vertical</h1>
    <div class=""chart-container"">
        <canvas id=""tracksChart""></canvas>
    </div>

    <script>
        const ctx = document.getElementById('tracksChart').getContext('2d');
        
        const chart = new Chart(ctx, {{
            type: 'line',
            data: {{
                datasets: [
                    {datasetsJson}
                ]
            }},
            options: {{
                responsive: true,
                maintainAspectRatio: false,
                scales: {{
                    x: {{
                        type: 'linear',
                        position: 'bottom',
                        min: -0.5,
                        max: {trackCurves.Count - 0.5},
                        ticks: {{
                            values: [{xTickPositions}],
                            callback: function(value, index, values) {{
                                const labels = [{xTickLabels}];
                                const tickIndex = Math.round(value - 0.5);
                                if (tickIndex >= 0 && tickIndex < labels.length && Math.abs(value - (tickIndex + 0.5)) < 0.01) {{
                                    return labels[tickIndex];
                                }}
                                return '';
                            }},
                            font: {{
                                size: 12,
                                weight: 'bold'
                            }}
                        }},
                        title: {{
                            display: true,
                            text: 'Tracks',
                            font: {{
                                size: 14,
                                weight: 'bold'
                            }}
                        }},
                        grid: {{
                            color: function(context) {{
                                const value = context.tick.value;
                                for (let i = 0; i < {trackCurves.Count - 1}; i++) {{
                                    if (Math.abs(value - (i + 0.5)) < 0.01) {{
                                        return 'rgba(128, 128, 128, 0.5)';
                                    }}
                                }}
                                return '#E5E5E5';
                            }}
                        }}
                    }},
                    y: {{
                        type: 'linear',
                        reverse: false,
                        title: {{
                            display: true,
                            text: 'Index',
                            font: {{
                                size: 14,
                                weight: 'bold'
                            }}
                        }},
                        grid: {{
                            color: '#D3D3D3'
                        }}
                    }}
                }},
                plugins: {{
                    legend: {{
                        display: true,
                        position: 'top',
                        labels: {{
                            boxWidth: 15,
                            padding: 8,
                            font: {{
                                size: 10
                            }}
                        }}
                    }},
                    title: {{
                        display: true,
                        text: 'Tracks (Grouped)',
                        font: {{
                            size: 18,
                            weight: 'bold'
                        }}
                    }},
                    tooltip: {{
                        mode: 'index',
                        intersect: false,
                        callbacks: {{
                            title: function(context) {{
                                return 'Index: ' + context[0].parsed.y;
                            }},
                            label: function(context) {{
                                return context.dataset.label + ': ' + context.parsed.x.toFixed(4);
                            }}
                        }}
                    }},
                    annotation: {{
                        annotations: {GenerateVerticalSeparators(trackCurves.Count)}
                    }}
                }},
                interaction: {{
                    mode: 'nearest',
                    axis: 'y',
                    intersect: false
                }}
            }}
        }});
    </script>
</body>
</html>";

            return html;
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

        private List<double> GenerateSampleData(double min, double max, int count = 100)
        {
            var data = new List<double>();
            var rand = new Random();

            double range = max - min;
            double midPoint = min + (range / 2);

            for (int i = 0; i < count; i++)
            {
                double t = (double)i / count;
                double value = 0;

                double trend = (rand.NextDouble() - 0.5) * range * 0.3 * t;
                double seasonal = Math.Sin(2 * Math.PI * t * (2 + rand.Next(0, 3))) * range * 0.25;
                double secondaryCycle = Math.Cos(2 * Math.PI * t * (4 + rand.Next(0, 2))) * range * 0.15;
                double noise = (rand.NextDouble() - 0.5) * range * 0.1;

                value = midPoint + trend + seasonal + secondaryCycle + noise;
                value = Math.Max(min, Math.Min(max, value));

                data.Add(value);
            }

            return data;
        }
    }
}