namespace Win11Seconds;

static class ClockLayout
{
    public const int PopupOffset = 30;
    public const int FontPadding = 4;
    public const int CloseButtonHoverSize = 30;
    public const int CloseButtonMargin = 7;
    public const float MinFontSize = 1f;
    public const float MaxFontSize = 512f;

    public static bool IsStoredLocationVisible(
        Point location,
        Size windowSize,
        IEnumerable<Rectangle> workingAreas)
    {
        var windowBounds = new Rectangle(location, windowSize);
        return workingAreas.Any(area => area.Contains(windowBounds));
    }

    public static Point CalculatePopupLocation(
        Point cursorPosition,
        Rectangle workingArea,
        Size windowSize,
        int offset = PopupOffset)
    {
        int x = cursorPosition.X - windowSize.Width / 2;
        int y = cursorPosition.Y - windowSize.Height - offset;

        x = Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - windowSize.Width));
        if (y < workingArea.Top)
        {
            y = cursorPosition.Y + offset;
        }

        return new Point(x, y);
    }

    public static Point CalculateCloseButtonLocation(Size clientSize, Size closeButtonSize, int rightPadding = 4)
    {
        return new Point(Math.Max(0, clientSize.Width - closeButtonSize.Width - rightPadding), 0);
    }

    public static bool ShouldShowCloseButton(
        Point cursorPosition,
        Rectangle clientRectangle,
        int hoverSize = CloseButtonHoverSize,
        int margin = CloseButtonMargin)
    {
        int region = hoverSize + margin;
        return cursorPosition.X >= clientRectangle.Width - region
            && cursorPosition.Y <= region;
    }

    public static int GetFirstTickIntervalMilliseconds(int currentMillisecond)
    {
        if (currentMillisecond <= 0)
        {
            return 1000;
        }

        return Math.Clamp(1000 - currentMillisecond, 1, 1000);
    }

    public static float? CalculateFontSize(
        Size clientSize,
        SizeF measurement,
        float measurementFontSize,
        int padding = FontPadding,
        float minFontSize = MinFontSize,
        float maxFontSize = MaxFontSize)
    {
        int availableWidth = clientSize.Width - padding * 2;
        int availableHeight = clientSize.Height - padding * 2;
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return null;
        }

        if (!float.IsFinite(measurement.Width)
            || !float.IsFinite(measurement.Height)
            || !float.IsFinite(measurementFontSize)
            || measurement.Width <= 0
            || measurement.Height <= 0
            || measurementFontSize <= 0)
        {
            return null;
        }

        float scaleWidth = availableWidth / measurement.Width;
        float scaleHeight = availableHeight / measurement.Height;
        float fontSize = measurementFontSize * Math.Min(scaleWidth, scaleHeight);
        if (!float.IsFinite(fontSize))
        {
            return null;
        }

        return Math.Clamp(fontSize, minFontSize, maxFontSize);
    }
}
