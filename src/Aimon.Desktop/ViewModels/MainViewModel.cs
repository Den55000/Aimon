using Aimon.Application.Abstractions;
using Aimon.Application.Pipeline;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Domain.Profiles;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// Minimal streaming chat view model for the Phase 3 walking skeleton. It saves the API key to the
/// OS secret store, sends a turn through <see cref="IChatService"/>, and appends streamed tokens
/// while reflecting the live <see cref="AgentState"/> in the UI.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string SecretKeyName = "openai";

    private readonly IChatService? _chatService;
    private readonly ISecretStore? _secretStore;
    private CancellationTokenSource? _sendCts;

    [ObservableProperty]
    public partial string ApiKey { get; set; }

    [ObservableProperty]
    public partial string Model { get; set; }

    [ObservableProperty]
    public partial string Input { get; set; }

    [ObservableProperty]
    public partial string Transcript { get; set; }

    [ObservableProperty]
    public partial string StateLabel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

    // Design-time constructor for the XAML previewer.
    public MainViewModel()
    {
        ApiKey = "";
        Model = "gpt-5.4-mini";
        Input = "";
        Transcript = "Aimon chat (design-time preview).";
        StateLabel = AgentState.Idle.ToString();
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public MainViewModel(IChatService chatService, ISecretStore secretStore)
    {
        _chatService = chatService;
        _secretStore = secretStore;
        ApiKey = "";
        Model = "gpt-5.4-mini";
        Input = "";
        Transcript = "";
        StateLabel = AgentState.Idle.ToString();
    }

    [RelayCommand]
    private async Task SaveKeyAsync()
    {
        if (_secretStore is null || string.IsNullOrWhiteSpace(ApiKey))
            return;

        await _secretStore.SetAsync(SecretKeyName, ApiKey.Trim(), CancellationToken.None);
        ApiKey = "";
        AppendLine("[API key saved to the OS secret store.]");
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_chatService is null || string.IsNullOrWhiteSpace(Input) || string.IsNullOrWhiteSpace(Model))
            return;

        var userText = Input.Trim();
        Input = "";
        AppendLine($"You: {userText}");
        Append("Aimon: ");

        IsBusy = true;
        _sendCts = new CancellationTokenSource();

        try
        {
            var request = new SendMessageRequest
            {
                Profile = BuildProfile(Model.Trim()),
                ConversationState = ConversationState.Empty,
                UserMessage = Message.User(userText),
            };

            await foreach (var item in _chatService.SendAsync(request, _sendCts.Token))
            {
                switch (item)
                {
                    case ProviderStreamItem.TextDelta delta:
                        Transcript += delta.Text;
                        break;
                    case ProviderStreamItem.StateChange stateChange:
                        StateLabel = stateChange.State.ToString();
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "blocked" } blocked:
                        AppendLine($"[blocked: {blocked.Payload}]");
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "error" } failure:
                        AppendLine($"[error: {failure.Payload}]");
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            AppendLine("[cancelled]");
        }
        catch (HttpRequestException ex)
        {
            StateLabel = AgentState.Error.ToString();
            AppendLine($"[error: {ex.Message}]");
        }
        finally
        {
            AppendLine(string.Empty);
            IsBusy = false;
            _sendCts?.Dispose();
            _sendCts = null;
        }
    }

    private bool CanSend() => !IsBusy;

    private void Append(string text) => Transcript += text;

    private void AppendLine(string line)
    {
        if (Transcript.Length > 0 && !Transcript.EndsWith('\n'))
            Transcript += Environment.NewLine;

        Transcript += line;
    }

    private static Profile BuildProfile(string model) => new()
    {
        Id = ProfileId.New(),
        Name = "Default",
        Structured = new StructuredProfile
        {
            Personality = new Personality { Persona = "Aimon, a friendly and concise assistant" },
            ModelPreferences = new ModelPreferences { Preferred = [new ModelRef("openai", model)] },
        },
    };
}
