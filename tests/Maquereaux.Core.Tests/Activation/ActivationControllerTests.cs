using Maquereaux.Core.Activation;
using Maquereaux.Core.Engine;
using Maquereaux.Core.Models;
using Maquereaux.Core.Tests.Fakes;

namespace Maquereaux.Core.Tests.Activation;

public class ActivationControllerTests
{
    private readonly FakeRunner _runner = new();
    private readonly Dictionary<Guid, Profile> _profiles = new();
    private readonly List<string> _errors = new();
    private readonly List<Guid> _activated = new();
    private readonly ActivationController _ctrl;
    private readonly Profile _p1;
    private readonly Profile _p2;

    public ActivationControllerTests()
    {
        _p1 = TestProfiles.Aze(); _p1.Name = "P1";
        _p2 = TestProfiles.Aze(); _p2.Name = "P2";
        _profiles[_p1.Id] = _p1;
        _profiles[_p2.Id] = _p2;
        _ctrl = new ActivationController(_runner, id => _profiles.GetValueOrDefault(id));
        _ctrl.Error += _errors.Add;
        _ctrl.ProfileActivated += _activated.Add;
    }

    [Fact]
    public void Toggle_starts_then_stops_same_profile()
    {
        _ctrl.Toggle(_p1.Id);
        _ctrl.Toggle(_p1.Id);
        Assert.Equal(new[] { "start:P1", "stop:P1:Manual" }, _runner.Log);
        Assert.Equal(new[] { _p1.Id }, _activated);
    }

    [Fact]
    public void Toggle_other_profile_switches_only_one_active_at_a_time()
    {
        _ctrl.Toggle(_p1.Id);
        _ctrl.Toggle(_p2.Id);
        Assert.Equal(new[] { "start:P1", "start:P2" }, _runner.Log); // Start remplace (le moteur arrête l'ancien)
        Assert.Equal("P2", _runner.RunningProfile!.Name);
    }

    [Fact]
    public void Emergency_stop_always_stops()
    {
        _ctrl.Toggle(_p1.Id);
        _ctrl.EmergencyStop();
        Assert.Equal("stop:P1:Emergency", _runner.Log.Last());
        Assert.False(_runner.IsRunning);
    }

    [Fact]
    public void Stop_button_stops_with_manual_reason()
    {
        _ctrl.Toggle(_p1.Id);
        _ctrl.Stop();
        Assert.Equal("stop:P1:Manual", _runner.Log.Last());
    }

    [Fact]
    public void Hold_down_starts_and_up_stops()
    {
        _ctrl.HoldDown(_p1.Id);
        Assert.True(_runner.IsRunning);
        _ctrl.HoldUp(_p1.Id);
        Assert.False(_runner.IsRunning);
        Assert.Equal(new[] { "start:P1", "stop:P1:HoldReleased" }, _runner.Log);
    }

    [Fact]
    public void Hold_autorepeat_downs_are_ignored()
    {
        _ctrl.HoldDown(_p1.Id);
        _ctrl.HoldDown(_p1.Id);
        _ctrl.HoldDown(_p1.Id);
        Assert.Single(_runner.Log);
    }

    [Fact]
    public void Hold_up_of_another_profile_does_not_stop_running_one()
    {
        _ctrl.HoldDown(_p1.Id);
        _ctrl.HoldUp(_p2.Id);
        Assert.True(_runner.IsRunning);
    }

    [Fact]
    public void Hold_up_after_toggle_switch_does_not_stop_the_toggled_profile()
    {
        _ctrl.HoldDown(_p1.Id);
        _ctrl.Toggle(_p2.Id); // P2 remplace P1 pendant le maintien
        _ctrl.HoldUp(_p1.Id);
        Assert.Equal("P2", _runner.RunningProfile!.Name);
    }

    [Fact]
    public void Empty_sequence_reports_error_and_does_not_start()
    {
        _p1.Steps.Clear();
        _ctrl.Toggle(_p1.Id);
        Assert.False(_runner.IsRunning);
        Assert.Single(_errors);
        Assert.Contains("vide", _errors[0]);
    }

    [Fact]
    public void Unknown_profile_reports_error()
    {
        _ctrl.Toggle(Guid.NewGuid());
        Assert.Single(_errors);
    }

    [Fact]
    public void Start_exception_is_reported_not_thrown()
    {
        _runner.ThrowOnStart = true;
        _ctrl.HoldDown(_p1.Id);
        Assert.Single(_errors);
        Assert.Contains("boom", _errors[0]);
    }

    [Fact]
    public void Gate_factory_is_used_on_start()
    {
        var gate = new FakeGate();
        var ctrl = new ActivationController(_runner, id => _profiles.GetValueOrDefault(id), _ => gate);
        ctrl.Toggle(_p1.Id);
        Assert.Same(gate, _runner.LastGate);
    }
}

public class SerialExecutorTests
{
    [Fact]
    public void Executes_actions_in_order_on_a_single_thread()
    {
        using var exec = new SerialExecutor();
        var order = new List<int>();
        var threads = new HashSet<int>();
        for (int i = 0; i < 100; i++)
        {
            var n = i;
            exec.Post(() => { order.Add(n); threads.Add(Environment.CurrentManagedThreadId); });
        }
        exec.Invoke(() => { });
        Assert.Equal(Enumerable.Range(0, 100), order);
        Assert.Single(threads);
    }

    [Fact]
    public void Failing_action_is_reported_and_executor_keeps_running()
    {
        using var exec = new SerialExecutor();
        Exception? reported = null;
        exec.ActionFailed += e => reported = e;
        exec.Post(() => throw new InvalidOperationException("x"));
        var ran = false;
        exec.Invoke(() => ran = true);
        Assert.True(ran);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public void Invoke_propagates_exceptions()
    {
        using var exec = new SerialExecutor();
        Assert.Throws<AggregateException>(() => exec.Invoke(() => throw new Exception("y")));
    }

    [Fact]
    public void Post_after_dispose_is_ignored()
    {
        var exec = new SerialExecutor();
        exec.Dispose();
        exec.Post(() => { });
    }
}
