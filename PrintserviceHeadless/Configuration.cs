namespace PrintserviceHeadless
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class ActivatedUnitSetDetails
    {
        public string id { get; set; }
        public DateTime updatedDateTime { get; set; }
    }

    public class AdvanceDisplayOptions
    {
        public bool showCloud { get; set; }
        public bool showTrend { get; set; }
    }

    public class AlarmThreshold
    {
        public bool enabledBefore { get; set; }
        public bool enabledAfter { get; set; }
        public double before { get; set; }
        public double after { get; set; }
    }

    public class AnnotationFilterConfiguration
    {
        public AppliedFilters appliedFilters { get; set; }
        public AppliedSettings appliedSettings { get; set; }
    }

    public class AnnotationSettings
    {
        public string messageFillColor { get; set; }
        public string riskFillColor { get; set; }
    }

    public class Appearance
    {
        public Color color { get; set; }
        public Fill fill { get; set; }
        public Border border { get; set; }
        public FillTransparency fillTransparency { get; set; }
    }

    public class AppliedFilter
    {
        public FlagState flagState { get; set; }
        public List<object> type { get; set; }
        public List<object> severity { get; set; }
        public List<object> affectedPersonnel { get; set; }
    }

    public class AppliedFilters
    {
        public Risk risk { get; set; }
        public Message message { get; set; }
        public TimeSummary timeSummary { get; set; }
        public FlagState flagState { get; set; }
    }

    public class AppliedSettings
    {
        public Risk risk { get; set; }
        public Message message { get; set; }
        public TimeSummary timeSummary { get; set; }
    }

    public class Border
    {
        public bool selected { get; set; }
        public Value value { get; set; }
    }

    public class Color
    {
        public string value { get; set; }
    }

    public class ColorMap
    {
        public string name { get; set; }
        public List<string> color { get; set; }
    }

    public class Content
    {
        public bool iconAndSeverity { get; set; }
        public bool name { get; set; }
        public bool description { get; set; }
        public bool summary { get; set; }
        public bool icon { get; set; }
        public bool type { get; set; }
        public bool probability { get; set; }
        public bool severity { get; set; }
        public bool activity { get; set; }
        public bool timeRange { get; set; }
        public bool depthMD { get; set; }
        public bool detailedActivity { get; set; }
        public bool comments { get; set; }
    }

    public class Curf
    {
        public string guid { get; set; }
        public string title { get; set; }
        public string colour { get; set; }
        public string logIndexUnit { get; set; }
        public int lineThickness { get; set; }
        public int decimalPrecision { get; set; }
        public int scaling { get; set; }
        public int manualScaleMin { get; set; }
        public int manualScaleMax { get; set; }
        public bool lineoptionsEnabled { get; set; }
        public string linePattern { get; set; }
        public bool fillEnabled { get; set; }
        public FillOptions fillOptions { get; set; }
        public bool dataTickMarksEnabled { get; set; }
        public int dataTickMarksPosition { get; set; }
        public int dataTickMarksLength { get; set; }
        public int dataValueToFill { get; set; }
        public bool symbolEnabled { get; set; }
        public bool customSymbolEnabled { get; set; }
        public int symbolType { get; set; }
        public int maxSymbolsInView { get; set; }
        public bool wrappingEnabled { get; set; }
        public bool valuesEnabled { get; set; }
        public int valuePositionType { get; set; }
        public int maxValuePositionsInView { get; set; }
        public int interpolationType { get; set; }
        public int unitConversionFactor { get; set; }
        public string wrapColour { get; set; }
        public string baseUnit { get; set; }
        public bool reverseDataRange { get; set; }
        public List<string> dataContextPath { get; set; }
        public string dataType { get; set; }
        public bool showLineType { get; set; }
        public int opacity { get; set; }
        public int fgOpacity { get; set; }
        public string selectedReferenceCurve { get; set; }
        public ValueOptions valueOptions { get; set; }
        public bool @checked { get; set; }
        public List<object> offsetWells { get; set; }
        public int leftPosition { get; set; }
        public int rightPosition { get; set; }
        public string minMaxDisplayUnit { get; set; }
        public int gapValue { get; set; }
        public bool gapEnable { get; set; }
        public ColorMap colorMap { get; set; }
        public bool mapReversed { get; set; }
        public int minAutoScale { get; set; }
        public int maxAutoScale { get; set; }
        public bool showLastValue { get; set; }
        public double minValue { get; set; }
        public double maxValue { get; set; }
        public int manualMinAutoScale { get; set; }
        public int manualMaxAutoScale { get; set; }
        public string aggregationOperator { get; set; }
        public string unit { get; set; }
        public string mnemonicType { get; set; }
        public int indexType { get; set; }
        public string unitType { get; set; }
        public string logType { get; set; }
        public bool pickLatestGMCurveStyle { get; set; }
        public CurveAsAnnotation curveAsAnnotation { get; set; }
        public AlarmThreshold alarmThreshold { get; set; }
        public WarningThreshold warningThreshold { get; set; }
        public AdvanceDisplayOptions advanceDisplayOptions { get; set; }
        public CurveSmoothingOptions curveSmoothingOptions { get; set; }
    }

    public class CurveAsAnnotation
    {
        public bool showAsAnnotation { get; set; }
        public Appearance appearance { get; set; }
    }

    public class CurveSmoothingOptions
    {
        public string linePattern { get; set; }
        public int lineThickness { get; set; }
        public string lineColor { get; set; }
        public bool valueOptionEnabled { get; set; }
        public string valueColor { get; set; }
        public int valuePositionType { get; set; }
        public int valueMaxInView { get; set; }
        public bool symbolOptionEnabled { get; set; }
        public int symbolType { get; set; }
        public int symbolMaxInView { get; set; }
        public bool hasDefaultValues { get; set; }
        public int alterationLevel { get; set; }
    }

    public class EnableDipPoint
    {
    }

    public class EnableTadPoles
    {
    }

    public class Fill
    {
        public bool selected { get; set; }
        public string value { get; set; }
    }

    public class FillOptions
    {
        public int fillType { get; set; }
        public int fillWith { get; set; }
        public string fillColor { get; set; }
        public string fillPosColor { get; set; }
        public string fillNegColor { get; set; }
        public int fillPattern { get; set; }
        public string fillPatternBgColor { get; set; }
        public string fillPatternFgColor { get; set; }
    }

    public class FillTransparency
    {
        public bool selected { get; set; }
        public string value { get; set; }
    }

    public class FlagState
    {
        public bool inTripRisk { get; set; }
        public bool inSAARForPresentation { get; set; }
        public bool inDailySARForPresentation { get; set; }
    }

    public class IndexTrack
    {
        public string guid { get; set; }
        public int trackWidthPercentage { get; set; }
        public bool @checked { get; set; }
        public List<object> mudlogPath { get; set; }
        public bool showLatest { get; set; }
        public string trackType { get; set; }
        public string name { get; set; }
        public bool showAnnotationIcons { get; set; }
        public SurveyMarkerConfig surveyMarkerConfig { get; set; }
        public WellGeometryConfig wellGeometryConfig { get; set; }
        public double indexTrackWidthPercent { get; set; }
        public int position { get; set; }
    }

    public class MarkerEnableSettings
    {
        public bool showMarker { get; set; }
        public bool showMarkerName { get; set; }
    }

    public class Message
    {
        public bool showFilter { get; set; }
        public AppliedFilter appliedFilter { get; set; }
        public Content content { get; set; }
        public Appearance appearance { get; set; }
    }

    public class Risk
    {
        public bool showFilter { get; set; }
        public AppliedFilter appliedFilter { get; set; }
        public Content content { get; set; }
        public Appearance appearance { get; set; }
    }

    public class Configuration
    {
        public string title { get; set; }
        public object widgetConfiguration { get; set; }
    }

    public class Scroll
    {
        public bool autoScrollEnabled { get; set; }
        public int autoScrollMode { get; set; }
    }

    public class SurveyMarkerConfig
    {
        public bool showMarkerIcons { get; set; }
        public string selectedType { get; set; }
    }

    public class TimeSummary
    {
        public bool showFilter { get; set; }
        public AppliedFilter appliedFilter { get; set; }
        public Content content { get; set; }
        public Appearance appearance { get; set; }
    }

    public class Track
    {
        public string guid { get; set; }
        public double trackWidthPercentage { get; set; }
        public bool @checked { get; set; }
        public List<object> mudlogPath { get; set; }
        public bool showLatest { get; set; }
        public AnnotationFilterConfiguration annotationFilterConfiguration { get; set; }
        public string backgroundColor { get; set; }
        public bool enableMessageAnnotation { get; set; }
        public bool enableRiskAnnotation { get; set; }
        public bool enableTimeSummaryAnnotation { get; set; }
        public List<object> dataContextPath { get; set; }
        public string trackType { get; set; }
        public int curveTrackType { get; set; }
        public List<Curf> curves { get; set; }
        public int logStart { get; set; }
        public int logDecades { get; set; }
        public int linearPrimary { get; set; }
        public int linearSecondary { get; set; }
        public bool showGridLines { get; set; }
        public int secondaryLineWidth { get; set; }
        public int primaryLineWidth { get; set; }
        public string secondaryLineColor { get; set; }
        public string primaryLineColor { get; set; }
        public string indexMajorTickColor { get; set; }
        public string indexMinorTickColor { get; set; }
        public string indexEdgeTickColor { get; set; }
        public int indexMajorTickWidth { get; set; }
        public int indexMinorTickWidth { get; set; }
        public int indexEdgeTickWidth { get; set; }
        public string name { get; set; }
        public bool showTrackTitle { get; set; }
        public bool showCurveTitle { get; set; }
        public bool showUOM { get; set; }
        public bool enableCommonRange { get; set; }
        public int commonManualScaleMin { get; set; }
        public int commonManualScaleMax { get; set; }
        public int commonMinAutoScale { get; set; }
        public int commonMaxAutoScale { get; set; }
        public int position { get; set; }
    }

    public class Value
    {
        public string linePattern { get; set; }
        public string width { get; set; }
        public string color { get; set; }
    }

    public class ValueOptions
    {
        public string valueColor { get; set; }
    }

    public class WarningThreshold
    {
        public bool enabledBefore { get; set; }
        public bool enabledAfter { get; set; }
        public double before { get; set; }
        public double after { get; set; }
    }

    public class WellGeometryConfig
    {
        public bool showWellGeometryIcons { get; set; }
        public string selectedType { get; set; }
        public string selectedUid { get; set; }
        public List<object> wellBoreGeometries { get; set; }
    }

    public class WidgetConfiguration
    {
        public List<IndexTrack> indexTracks { get; set; }
        public List<Track> tracks { get; set; }
        public int decimals { get; set; }
        public string indexUnit { get; set; }
        public bool activeWellboreContext { get; set; }
        public int depthMode { get; set; }
        public bool isTimeDisabled { get; set; }
        public bool isFilterApplied { get; set; }
        public bool isViewAnnotationPermission { get; set; }
        public bool autoscroll { get; set; }
        public bool isValid { get; set; }
        public string uid { get; set; }
        public int chartIndexType { get; set; }
        public bool autosizeTracks { get; set; }
        public int orientation { get; set; }
        public int headerPlacement { get; set; }
        public bool cursorVisibility { get; set; }
        public Scroll scroll { get; set; }
        public MarkerEnableSettings markerEnableSettings { get; set; }
        public AnnotationSettings annotationSettings { get; set; }
        public int selectedDepthLimit { get; set; }
        public List<object> wellboreDataContextPath { get; set; }
        public List<object> formationMarkerDataContextPath { get; set; }
        public string formationMarkerUint { get; set; }
        public string formationMarkerTitle { get; set; }
        public ActivatedUnitSetDetails activatedUnitSetDetails { get; set; }
        public int zoomMode { get; set; }
        public int defaultZoomInMeter { get; set; }
        public int defaultZoomInFeet { get; set; }
        public int defaultZoomInTime { get; set; }
        public int defaultScaleInMeter { get; set; }
        public int defaultScaleInFeet { get; set; }
        public string selectedDepthUnitFromUnitSet { get; set; }
        public bool syncMasterTrack { get; set; }
        public int headerheight { get; set; }
        public bool gmHubAdaptationRead { get; set; }
        public EnableDipPoint enableDipPoint { get; set; }
        public EnableTadPoles enableTadPoles { get; set; }
    }


}
