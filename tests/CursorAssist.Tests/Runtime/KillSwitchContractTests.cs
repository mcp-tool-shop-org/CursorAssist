using CursorAssist.Engine.Core;
using CursorAssist.Runtime.Core;
using Xunit;

namespace CursorAssist.Tests.Runtime;

/// <summary>
/// Tests for the kill switch contract: EmergencyStop behavior and
/// IKillSwitch → EngineThread wiring.
/// All tests use EngineThread directly — no Windows dependency.
/// </summary>
public class KillSwitchContractTests
{
    [Fact]
    public void EmergencyStop_StopsEngine()
    {
        var pipeline = new TransformPipeline();
        var engine = new EngineThread(pipeline);

        engine.Enable(100f, 200f);
        Assert.True(engine.IsRunning);

        engine.EmergencyStop();
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void EmergencyStop_DrainsQueues()
    {
        var pipeline = new TransformPipeline();
        var engine = new EngineThread(pipeline);

        // Enqueue items before enabling (queues are public)
        engine.InputQueue.Enqueue(new RawInputEvent(1f, 2f, false, false, 0));
        engine.InputQueue.Enqueue(new RawInputEvent(3f, 4f, false, false, 1));
        engine.InjectionQueue.Enqueue(new AssistedDelta(1f, 1f, 0));

        engine.EmergencyStop();

        Assert.True(engine.InputQueue.IsEmpty);
        Assert.True(engine.InjectionQueue.IsEmpty);
    }

    [Fact]
    public void EmergencyStop_ResetsPipelineState()
    {
        var pipeline = new TransformPipeline();
        var engine = new EngineThread(pipeline);

        // Enable at a non-origin position, feed some input, let engine run briefly
        engine.Enable(500f, 300f);
        engine.InputQueue.Enqueue(new RawInputEvent(10f, 5f, false, false, 0));
        Thread.Sleep(50); // Let the engine thread process at least one tick

        // Cursor should be near the enable position (500 + deltas)
        Assert.True(engine.IsRunning);

        engine.EmergencyStop();

        // After emergency stop, cursor is reset to origin
        Assert.Equal(0f, engine.Cursor.X);
        Assert.Equal(0f, engine.Cursor.Y);
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void MockKillSwitch_TriggeredEvent_StopsEngine()
    {
        var pipeline = new TransformPipeline();
        var engine = new EngineThread(pipeline);
        var killSwitch = new MockKillSwitch();

        // Wire kill switch to engine
        killSwitch.Triggered += engine.EmergencyStop;

        engine.Enable(100f, 200f);
        Assert.True(engine.IsRunning);

        // Simulate hotkey press
        killSwitch.SimulateTrigger();

        Assert.False(engine.IsRunning);
    }

    /// <summary>
    /// In-memory IKillSwitch for testing event wiring without Windows dependency.
    /// </summary>
    private sealed class MockKillSwitch : IKillSwitch
    {
        public bool IsArmed { get; private set; }
        public event Action? Triggered;

        public void Arm() => IsArmed = true;
        public void Disarm() => IsArmed = false;

        public void SimulateTrigger() => Triggered?.Invoke();

        public void Dispose() => Disarm();
    }
}
