namespace AetherBoy.Desktop;

// Logical coordinates shared by drawing and hit areas. Larger text trades density for readable rows.
internal readonly record struct LinuxLibraryLayout(float Width, float Top, float RowHeight, int Columns,
    int Rows, float ColumnWidth, float SummaryY, float FooterY)
{
    internal int PageSize => Rows * Columns;

    internal static LinuxLibraryLayout Create(int logicalWidth, int logicalHeight, int minimumTextSize, bool grid)
    {
        float width = Math.Max(810, logicalWidth - 324);
        float footer = logicalHeight - 168;
        float summary = footer - Math.Max(28, minimumTextSize + 10);
        float rowHeight = grid ? Math.Max(108, minimumTextSize + 90) : Math.Max(62, minimumTextSize + 44);
        int columns = grid ? Math.Max(2, (int)((width + 12) / 400)) : 1;
        int rows = Math.Max(1, (int)((summary - 12 - 304) / rowHeight));
        return new(width, 304, rowHeight, columns, rows, width / columns, summary, footer);
    }
}
