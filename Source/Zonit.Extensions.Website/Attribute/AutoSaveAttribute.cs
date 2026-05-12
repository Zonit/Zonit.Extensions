namespace Zonit.Extensions.Website;

[AttributeUsage(AttributeTargets.Property)]
public class AutoSaveAttribute : Attribute
{
    public int DelayMs { get; set; } = 800;

    public AutoSaveAttribute() { }

    public AutoSaveAttribute(int delayMs)
    {
        DelayMs = delayMs;
    }
}