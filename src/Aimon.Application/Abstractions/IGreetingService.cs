namespace Aimon.Application.Abstractions;

/// <summary>
/// Placeholder Application port used to prove the Clean Architecture wiring end-to-end in
/// Phase 0 (Domain &lt;- Application &lt;- Infrastructure &lt;- Desktop). It is replaced by the real
/// ports (IRouter, IInterpreter, IAiProvider, …) in Phase 1.
/// </summary>
public interface IGreetingService
{
    string GetGreeting();
}
