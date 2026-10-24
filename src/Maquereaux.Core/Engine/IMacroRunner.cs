using Maquereaux.Core.Input;
using Maquereaux.Core.Models;

namespace Maquereaux.Core.Engine;

/// <summary>Ce dont l'<see cref="Activation.ActivationController"/> a besoin du moteur (permet de le simuler en test).</summary>
public interface IMacroRunner
{
    bool IsRunning { get; }
    Profile? RunningProfile { get; }
    void Start(Profile profile, ISendGate? gate = null);
    void Stop(StopReason reason = StopReason.Manual);
}
