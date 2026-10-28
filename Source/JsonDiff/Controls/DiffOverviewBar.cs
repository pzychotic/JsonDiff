using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using JsonDiff.Models;
using JsonDiff.Services;

namespace JsonDiff.Controls;

/// <summary>
/// A vertical map of both panes' rows: a column per side with a mark for each difference, and a frame around the rows
/// in view. Clicking centers the view on that spot, dragging moves it like a scrollbar thumb, and the wheel scrolls.
/// </summary>
public sealed class DiffOverviewBar : FrameworkElement
{
    public static readonly DependencyProperty LeftLinesProperty =
        DependencyProperty.Register(
            nameof(LeftLines),
            typeof(IReadOnlyList<object>),
            typeof(DiffOverviewBar),
            new FrameworkPropertyMetadata(Array.Empty<object>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RightLinesProperty =
        DependencyProperty.Register(
            nameof(RightLines),
            typeof(IReadOnlyList<object>),
            typeof(DiffOverviewBar),
            new FrameworkPropertyMetadata(Array.Empty<object>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TopRowProperty =
        DependencyProperty.Register(
            nameof(TopRow),
            typeof(int),
            typeof(DiffOverviewBar),
            new FrameworkPropertyMetadata(0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ViewportRowsProperty =
        DependencyProperty.Register(
            nameof(ViewportRows),
            typeof(int),
            typeof(DiffOverviewBar),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LeftSearchRowsProperty =
        DependencyProperty.Register(
            nameof(LeftSearchRows),
            typeof(IReadOnlyList<int>),
            typeof(DiffOverviewBar),
            new FrameworkPropertyMetadata(Array.Empty<int>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RightSearchRowsProperty =
        DependencyProperty.Register(
            nameof(RightSearchRows),
            typeof(IReadOnlyList<int>),
            typeof(DiffOverviewBar),
            new FrameworkPropertyMetadata(Array.Empty<int>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty = RegisterBrush(nameof(Background), Brushes.White);
    public static readonly DependencyProperty SeparatorBrushProperty = RegisterBrush(nameof(SeparatorBrush), Brushes.LightGray);
    public static readonly DependencyProperty UnmatchedBrushProperty = RegisterBrush(nameof(UnmatchedBrush), Brushes.IndianRed);
    public static readonly DependencyProperty ModifiedBrushProperty = RegisterBrush(nameof(ModifiedBrush), Brushes.Goldenrod);
    public static readonly DependencyProperty TypeChangedBrushProperty = RegisterBrush(nameof(TypeChangedBrush), Brushes.MediumPurple);
    public static readonly DependencyProperty IgnoredBrushProperty = RegisterBrush(nameof(IgnoredBrush), Brushes.DarkGray);
    public static readonly DependencyProperty SearchMatchBrushProperty = RegisterBrush(nameof(SearchMatchBrush), Brushes.DarkOrange);
    public static readonly DependencyProperty ViewportBrushProperty = RegisterBrush(nameof(ViewportBrush), Brushes.Transparent);
    public static readonly DependencyProperty ViewportBorderBrushProperty = RegisterBrush(nameof(ViewportBorderBrush), Brushes.SteelBlue);

    private static DependencyProperty RegisterBrush(string name, Brush fallback) =>
        DependencyProperty.Register(name, typeof(Brush), typeof(DiffOverviewBar),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The left pane's rows, drawn in the left column.</summary>
    public IReadOnlyList<object> LeftLines
    {
        get => (IReadOnlyList<object>)GetValue(LeftLinesProperty);
        set => SetValue(LeftLinesProperty, value);
    }

    /// <summary>The right pane's rows, drawn in the right column.</summary>
    public IReadOnlyList<object> RightLines
    {
        get => (IReadOnlyList<object>)GetValue(RightLinesProperty);
        set => SetValue(RightLinesProperty, value);
    }

    /// <summary>The left pane's rows holding a search hit, drawn over its differences.</summary>
    public IReadOnlyList<int> LeftSearchRows
    {
        get => (IReadOnlyList<int>)GetValue(LeftSearchRowsProperty);
        set => SetValue(LeftSearchRowsProperty, value);
    }

    /// <summary>The right pane's rows holding a search hit, drawn over its differences.</summary>
    public IReadOnlyList<int> RightSearchRows
    {
        get => (IReadOnlyList<int>)GetValue(RightSearchRowsProperty);
        set => SetValue(RightSearchRowsProperty, value);
    }

    /// <summary>The row shown at the top of the panes; the bar sets it when clicked, dragged or scrolled.</summary>
    public int TopRow
    {
        get => (int)GetValue(TopRowProperty);
        set => SetValue(TopRowProperty, value);
    }

    /// <summary>How many rows fit into the panes; the height of the frame.</summary>
    public int ViewportRows
    {
        get => (int)GetValue(ViewportRowsProperty);
        set => SetValue(ViewportRowsProperty, value);
    }

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Brush SeparatorBrush
    {
        get => (Brush)GetValue(SeparatorBrushProperty);
        set => SetValue(SeparatorBrushProperty, value);
    }

    public Brush UnmatchedBrush
    {
        get => (Brush)GetValue(UnmatchedBrushProperty);
        set => SetValue(UnmatchedBrushProperty, value);
    }

    public Brush ModifiedBrush
    {
        get => (Brush)GetValue(ModifiedBrushProperty);
        set => SetValue(ModifiedBrushProperty, value);
    }

    public Brush TypeChangedBrush
    {
        get => (Brush)GetValue(TypeChangedBrushProperty);
        set => SetValue(TypeChangedBrushProperty, value);
    }

    public Brush IgnoredBrush
    {
        get => (Brush)GetValue(IgnoredBrushProperty);
        set => SetValue(IgnoredBrushProperty, value);
    }

    public Brush SearchMatchBrush
    {
        get => (Brush)GetValue(SearchMatchBrushProperty);
        set => SetValue(SearchMatchBrushProperty, value);
    }

    public Brush ViewportBrush
    {
        get => (Brush)GetValue(ViewportBrushProperty);
        set => SetValue(ViewportBrushProperty, value);
    }

    public Brush ViewportBorderBrush
    {
        get => (Brush)GetValue(ViewportBorderBrushProperty);
        set => SetValue(ViewportBorderBrushProperty, value);
    }

    private const double MinFrameHeight = 6;
    private const double ColumnPadding = 2;

    /// <summary>Where the pointer grabbed the frame, measured from its top; set while dragging.</summary>
    private double? _grabOffset;

    private readonly MarksCache _leftMarks = new();
    private readonly MarksCache _rightMarks = new();
    private readonly SpansCache _leftSearchSpans = new();
    private readonly SpansCache _rightSearchSpans = new();

    private int RowCount => Math.Max(LeftLines.Count, RightLines.Count);

    protected override void OnRender(DrawingContext dc)
    {
        var (width, height) = (ActualWidth, ActualHeight);
        dc.DrawRectangle(Background, null, new Rect(0, 0, width, height));
        if (width <= 0 || height <= 0)
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var middle = Snap(width / 2, dpi.DpiScaleX);
        var separator = 1 / dpi.DpiScaleX;
        dc.DrawRectangle(SeparatorBrush, null, new Rect(middle, 0, separator, height));

        if (RowCount == 0)
            return;

        // A side's marks are scaled to its own row count, which both sides share whenever a diff is shown.
        var leftHeight = height * LeftLines.Count / RowCount;
        var rightHeight = height * RightLines.Count / RowCount;
        DrawMarks(_leftMarks.Get(LeftLines, leftHeight), ColumnPadding, middle - ColumnPadding);
        DrawMarks(_rightMarks.Get(RightLines, rightHeight), middle + separator + ColumnPadding, width - ColumnPadding);

        // The search hits cover the middle half of their column, so the differences below stay visible at its edges.
        var inset = (middle - 2 * ColumnPadding) / 4;
        DrawSpans(_leftSearchSpans.Get(LeftSearchRows, LeftLines.Count, leftHeight),
            ColumnPadding + inset, middle - ColumnPadding - inset);
        DrawSpans(_rightSearchSpans.Get(RightSearchRows, RightLines.Count, rightHeight),
            middle + separator + ColumnPadding + inset, width - ColumnPadding - inset);

        var frame = Frame();
        var thickness = 1 / dpi.DpiScaleY;
        var top = Snap(frame.Top, dpi.DpiScaleY);
        var bottom = Math.Max(Snap(frame.Bottom, dpi.DpiScaleY), top + 2 * thickness);
        dc.DrawRectangle(ViewportBrush, new Pen(ViewportBorderBrush, thickness),
            new Rect(thickness / 2, top + thickness / 2, width - thickness, bottom - top - thickness));

        void DrawMarks(IReadOnlyList<OverviewMark> marks, double left, double right)
        {
            if (right <= left)
                return;

            foreach (var mark in marks)
                DrawStretch(BrushFor(mark.Kind), mark.Top, mark.Height, left, right);
        }

        void DrawSpans(IReadOnlyList<OverviewSpan> spans, double left, double right)
        {
            if (right <= left)
                return;

            foreach (var span in spans)
                DrawStretch(SearchMatchBrush, span.Top, span.Height, left, right);
        }

        void DrawStretch(Brush brush, double top, double stretchHeight, double left, double right)
        {
            var snappedTop = Snap(top, dpi.DpiScaleY);
            // At least one device pixel, so a thin run never rounds away.
            var bottom = Math.Max(Snap(top + stretchHeight, dpi.DpiScaleY), snappedTop + 1 / dpi.DpiScaleY);
            dc.DrawRectangle(brush, null, new Rect(left, snappedTop, right - left, bottom - snappedTop));
        }
    }

    private Brush BrushFor(DiffLineKind kind) => kind switch
    {
        DiffLineKind.TypeChanged => TypeChangedBrush,
        DiffLineKind.Modified => ModifiedBrush,
        DiffLineKind.Unmatched => UnmatchedBrush,
        _ => IgnoredBrush,
    };

    private static double Snap(double value, double scale) => Math.Round(value * scale) / scale;

    /// <summary>The rows in view, in bar units; never thinner than <see cref="MinFrameHeight"/>.</summary>
    private Rect Frame()
    {
        var rowHeight = ActualHeight / RowCount;
        var height = Math.Min(ActualHeight, Math.Max(Math.Min(ViewportRows, RowCount) * rowHeight, MinFrameHeight));
        var top = Math.Min(TopRow * rowHeight, ActualHeight - height);
        return new Rect(0, Math.Max(0, top), ActualWidth, height);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (RowCount == 0 || !CaptureMouse())
            return;

        // Grabbing the frame keeps it under the pointer; a press elsewhere centers the frame there.
        var y = e.GetPosition(this).Y;
        var frame = Frame();
        _grabOffset = y >= frame.Top && y <= frame.Bottom ? y - frame.Top : frame.Height / 2;
        MoveFrameTo(y);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_grabOffset is not null && IsMouseCaptured)
            MoveFrameTo(e.GetPosition(this).Y);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _grabOffset = null;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollTo(TopRow - Math.Sign(e.Delta) * SystemParameters.WheelScrollLines);
        e.Handled = true;
    }

    private void MoveFrameTo(double y)
    {
        if (_grabOffset is not { } offset || ActualHeight <= 0)
            return;

        ScrollTo((int)Math.Round((y - offset) * RowCount / ActualHeight));
    }

    private void ScrollTo(int row)
    {
        var last = Math.Max(0, RowCount - ViewportRows);
        // SetCurrentValue keeps the binding to the pane's top row.
        SetCurrentValue(TopRowProperty, Math.Clamp(row, 0, last));
    }

    /// <summary>The marks of the last rows and height drawn; rows are replaced, never changed in place.</summary>
    private sealed class MarksCache
    {
        private IReadOnlyList<object>? _lines;
        private double _height;
        private IReadOnlyList<OverviewMark> _marks = [];

        public IReadOnlyList<OverviewMark> Get(IReadOnlyList<object> lines, double height)
        {
            if (!ReferenceEquals(lines, _lines) || height != _height)
                (_lines, _height, _marks) = (lines, height, OverviewMarks.Build(lines, height));
            return _marks;
        }
    }

    /// <summary>The spans of the last search rows and height drawn; rows are replaced, never changed in place.</summary>
    private sealed class SpansCache
    {
        private IReadOnlyList<int>? _rows;
        private int _rowCount;
        private double _height;
        private IReadOnlyList<OverviewSpan> _spans = [];

        public IReadOnlyList<OverviewSpan> Get(IReadOnlyList<int> rows, int rowCount, double height)
        {
            if (!ReferenceEquals(rows, _rows) || rowCount != _rowCount || height != _height)
                (_rows, _rowCount, _height, _spans) = (rows, rowCount, height, OverviewMarks.BuildRows(rows, rowCount, height));
            return _spans;
        }
    }
}
