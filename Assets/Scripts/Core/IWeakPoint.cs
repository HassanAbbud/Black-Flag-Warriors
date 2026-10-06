namespace Ahoy.Core
{
    // The diamond. Owned by the WP & UI track.
    public interface IWeakPoint
    {
        WeakPointState State { get; }
        float Progress { get; }                  // 0..1 of the gauge remaining
        ToolType? CounterTool { get; }           // what opens this species, null if none
        bool TryExpose(ExposeSource source);
        void Drain(float amount);
        float WindowRemaining { get; }           // R23: 8 s, then recovery
    }
}
