namespace Win11Seconds;

enum ResizeDirection
{
    Left = 1,
    Right = 2,
    Top = 3,
    TopLeft = 4,
    TopRight = 5,
    Bottom = 6,
    BottomLeft = 7,
    BottomRight = 8
}

static class ClockLayout
{
    public const int PopupOffset = 30;
    public const int FontPadding = 4;
    public const int CloseButtonHoverSize = 30;
    public const int CloseButtonMargin = 7;
    public const float MinFontSize = 1f;
    public const float MaxFontSize = 512f;
    public const float DefaultAspectRatio = 200f / 80f;

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

    public static Rectangle CalculateConstrainedBounds(
        Rectangle proposedBounds,
        ResizeDirection direction,
        float aspectRatio,
        Size minimumSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aspectRatio);

        int proposedWidth = Math.Max(1, proposedBounds.Width);
        int proposedHeight = Math.Max(1, proposedBounds.Height);

        Size widthDrivenSize = CreateSizeFromWidth(proposedWidth, aspectRatio, minimumSize);
        Size heightDrivenSize = CreateSizeFromHeight(proposedHeight, aspectRatio, minimumSize);

        return direction switch
        {
            ResizeDirection.Left => CreateHorizontalEdgeBounds(
                proposedBounds.Right,
                GetVerticalCenter(proposedBounds),
                widthDrivenSize,
                anchorLeft: false),
            ResizeDirection.Right => CreateHorizontalEdgeBounds(
                proposedBounds.Left,
                GetVerticalCenter(proposedBounds),
                widthDrivenSize,
                anchorLeft: true),
            ResizeDirection.Top => CreateVerticalEdgeBounds(
                proposedBounds.Bottom,
                GetHorizontalCenter(proposedBounds),
                heightDrivenSize,
                anchorTop: false),
            ResizeDirection.Bottom => CreateVerticalEdgeBounds(
                proposedBounds.Top,
                GetHorizontalCenter(proposedBounds),
                heightDrivenSize,
                anchorTop: true),
            ResizeDirection.TopLeft => CreateCornerBounds(
                proposedBounds.Right,
                proposedBounds.Bottom,
                ChooseCloserSize(proposedWidth, proposedHeight, widthDrivenSize, heightDrivenSize),
                anchorLeft: false,
                anchorTop: false),
            ResizeDirection.TopRight => CreateCornerBounds(
                proposedBounds.Left,
                proposedBounds.Bottom,
                ChooseCloserSize(proposedWidth, proposedHeight, widthDrivenSize, heightDrivenSize),
                anchorLeft: true,
                anchorTop: false),
            ResizeDirection.BottomLeft => CreateCornerBounds(
                proposedBounds.Right,
                proposedBounds.Top,
                ChooseCloserSize(proposedWidth, proposedHeight, widthDrivenSize, heightDrivenSize),
                anchorLeft: false,
                anchorTop: true),
            ResizeDirection.BottomRight => CreateCornerBounds(
                proposedBounds.Left,
                proposedBounds.Top,
                ChooseCloserSize(proposedWidth, proposedHeight, widthDrivenSize, heightDrivenSize),
                anchorLeft: true,
                anchorTop: true),
            _ => proposedBounds
        };
    }

    private static Size CreateSizeFromWidth(int width, float aspectRatio, Size minimumSize)
    {
        width = Math.Max(width, minimumSize.Width);
        int height = Math.Max((int)Math.Round(width / aspectRatio), minimumSize.Height);
        width = Math.Max(width, (int)Math.Round(height * aspectRatio));
        return new Size(width, height);
    }

    private static Size CreateSizeFromHeight(int height, float aspectRatio, Size minimumSize)
    {
        height = Math.Max(height, minimumSize.Height);
        int width = Math.Max((int)Math.Round(height * aspectRatio), minimumSize.Width);
        height = Math.Max(height, (int)Math.Round(width / aspectRatio));
        return new Size(width, height);
    }

    private static Rectangle CreateHorizontalEdgeBounds(
        int horizontalAnchor,
        int verticalCenter,
        Size size,
        bool anchorLeft)
    {
        int left = anchorLeft ? horizontalAnchor : horizontalAnchor - size.Width;
        int top = verticalCenter - size.Height / 2;
        return new Rectangle(left, top, size.Width, size.Height);
    }

    private static Rectangle CreateVerticalEdgeBounds(
        int verticalAnchor,
        int horizontalCenter,
        Size size,
        bool anchorTop)
    {
        int left = horizontalCenter - size.Width / 2;
        int top = anchorTop ? verticalAnchor : verticalAnchor - size.Height;
        return new Rectangle(left, top, size.Width, size.Height);
    }

    private static Rectangle CreateCornerBounds(
        int horizontalAnchor,
        int verticalAnchor,
        Size size,
        bool anchorLeft,
        bool anchorTop)
    {
        int left = anchorLeft ? horizontalAnchor : horizontalAnchor - size.Width;
        int top = anchorTop ? verticalAnchor : verticalAnchor - size.Height;
        return new Rectangle(left, top, size.Width, size.Height);
    }

    private static Size ChooseCloserSize(
        int proposedWidth,
        int proposedHeight,
        Size widthDrivenSize,
        Size heightDrivenSize)
    {
        int widthDrivenDelta = Math.Abs(widthDrivenSize.Width - proposedWidth)
            + Math.Abs(widthDrivenSize.Height - proposedHeight);
        int heightDrivenDelta = Math.Abs(heightDrivenSize.Width - proposedWidth)
            + Math.Abs(heightDrivenSize.Height - proposedHeight);

        return widthDrivenDelta <= heightDrivenDelta ? widthDrivenSize : heightDrivenSize;
    }

    private static int GetHorizontalCenter(Rectangle bounds)
    {
        return bounds.Left + bounds.Width / 2;
    }

    private static int GetVerticalCenter(Rectangle bounds)
    {
        return bounds.Top + bounds.Height / 2;
    }
}
