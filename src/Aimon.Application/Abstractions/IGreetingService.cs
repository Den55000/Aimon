namespace Aimon.Application.Abstractions;

/// <summary>
/// Placeholder Application port that proves the Clean Architecture wiring end-to-end
/// (Domain &lt;- Application &lt;- Infrastructure &lt;- Desktop). The real pipeline ports now live
/// alongside it in <c>Abstractions</c>; this shim stays only to keep the window showing something
/// until the chat use case (<see cref="IChatService"/>) is wired into the UI in Phase 3.
/// </summary>
public interface IGreetingService
{
    string GetGreeting();
}
