using Ardalis.SmartEnum;

namespace AnalisisSentimiento.Domain.Enums;

public sealed class PriorityLevel : SmartEnum<PriorityLevel>
{
    public static readonly PriorityLevel None = new(nameof(None), 0);
    public static readonly PriorityLevel Low = new(nameof(Low), 1);
    public static readonly PriorityLevel Medium = new(nameof(Medium), 2);
    public static readonly PriorityLevel High = new(nameof(High), 3);

    private PriorityLevel(string name, int value)
        : base(name, value) { }
}
