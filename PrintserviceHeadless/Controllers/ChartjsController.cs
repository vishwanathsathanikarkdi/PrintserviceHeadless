using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PrintserviceHeadless.Models;
using System.Text;
using System.Text.Json;

namespace PrintserviceHeadless.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChartJsController : ControllerBase
    {
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private static string? _cachedChartJsScript;
        private static string? _cachedAnnotationScript;

        public ChartJsController(
            ICompositeViewEngine viewEngine, 
            ITempDataProvider tempDataProvider,
            IWebHostEnvironment webHostEnvironment)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpPost("plot-chartjs")]
        public async Task<IActionResult> PlotTracksChartJs([FromBody] PlotTrackRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WidgetsAsJsonString))
                return BadRequest("widgetsAsJsonString is required.");

            var widgets = JsonDocument.Parse(request.WidgetsAsJsonString).RootElement;
            var config = widgets[0].GetProperty("configuration").GetProperty("widgetConfiguration");
            var tracks = config.GetProperty("tracks");
            int orientation = config.TryGetProperty("orientation", out var orientElem) ? orientElem.GetInt32() : 1;

            int pointsPerTrack = 100;

            var (trackNames, trackCurves) = ParseTracks(tracks, pointsPerTrack);

            var model = new ChartViewModel
            {
                TrackNames = trackNames,
                TrackCurves = trackCurves,
                Orientation = orientation,
                IsHorizontal = orientation == 1
            };

            // Render view to HTML string
            var viewName = orientation == 1 ? "HorizontalChart" : "VerticalChart";
            string htmlContent = await RenderViewToStringAsync(viewName, model);

            // Embed local JavaScript files
            htmlContent = await EmbedLocalScriptsAsync(htmlContent);

            // Convert to stream and return as downloadable HTML file
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
            stream.Position = 0;

            return File(stream, "text/html", "tracks-chart.html");
        }

        private async Task<string> EmbedLocalScriptsAsync(string htmlContent)
        {
            // Get scripts from local files (cached)
            var chartJsScript = await GetChartJsScriptAsync();
            var annotationScript = await GetAnnotationScriptAsync();

            // Replace CDN script tags with inline scripts
            htmlContent = htmlContent.Replace(
                "<script src=\"https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js\"></script>",
                $"<script>{chartJsScript}</script>"
            );

            htmlContent = htmlContent.Replace(
                "<script src=\"https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation@3.0.1/dist/chartjs-plugin-annotation.min.js\"></script>",
                $"<script>{annotationScript}</script>"
            );

            return htmlContent;
        }

        private async Task<string> GetChartJsScriptAsync()
        {
            if (_cachedChartJsScript != null)
                return _cachedChartJsScript;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedChartJsScript != null)
                    return _cachedChartJsScript;

                // Path to the local Chart.js file in Views/libs folder
                var scriptPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Views", "libs", "chart.umd.min.js");
                
                if (!System.IO.File.Exists(scriptPath))
                {
                    throw new FileNotFoundException($"Chart.js file not found at: {scriptPath}");
                }

                _cachedChartJsScript = await System.IO.File.ReadAllTextAsync(scriptPath);
                return _cachedChartJsScript;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task<string> GetAnnotationScriptAsync()
        {
            if (_cachedAnnotationScript != null)
                return _cachedAnnotationScript;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cachedAnnotationScript != null)
                    return _cachedAnnotationScript;

                // Path to the local annotation plugin file in Views/libs folder
                var scriptPath = Path.Combine(_webHostEnvironment.ContentRootPath, "Views", "libs", "chartjs-plugin-annotation.min.js");
                
                if (!System.IO.File.Exists(scriptPath))
                {
                    throw new FileNotFoundException($"Chart.js annotation plugin file not found at: {scriptPath}");
                }

                _cachedAnnotationScript = await System.IO.File.ReadAllTextAsync(scriptPath);
                return _cachedAnnotationScript;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private async Task<string> RenderViewToStringAsync(string viewName, object model)
        {
            var actionContext = new ActionContext(HttpContext, RouteData, ControllerContext.ActionDescriptor);

            using var sw = new StringWriter();
            var viewResult = _viewEngine.FindView(actionContext, viewName, false);

            if (viewResult.View == null)
            {
                throw new ArgumentNullException($"{viewName} does not match any available view");
            }

            var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };

            var viewContext = new ViewContext(
                actionContext,
                viewResult.View,
                viewDictionary,
                new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                sw,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return sw.ToString();
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