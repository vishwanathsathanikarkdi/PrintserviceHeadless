namespace PrintserviceHeadless.Models
{
    public class ChartViewModel
    {
        public List<string> TrackNames { get; set; } = new();
        public List<(string trackName, List<(string curveTitle, string color, double min, double max, double thickness, List<double> data)>)> TrackCurves { get; set; } = new();
        public int Orientation { get; set; }
        public bool IsHorizontal { get; set; }
    }
}