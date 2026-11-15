namespace CutCraft.Core.Models;

public class CaptionItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Index { get; set; } = 1;
    public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan EndTime { get; set; } = TimeSpan.FromSeconds(3);
    public string Text { get; set; } = string.Empty;

    public TimeSpan Duration => EndTime - StartTime;

    public CaptionItem() { }

    public CaptionItem(int index, TimeSpan start, TimeSpan end, string text)
    {
        Index = index;
        StartTime = start;
        EndTime = end;
        Text = text;
    }
}
