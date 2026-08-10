using System.Collections.ObjectModel;
using Aimon.Application.Abstractions;
using Aimon.Application.Agents;
using Aimon.Application.Pipeline;
using Aimon.Desktop.Services;
using Aimon.Domain;
using Aimon.Domain.Agents;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Domain.Profiles;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// Streaming chat view model. It saves the API key, sends a turn through <see cref="IChatService"/> on
/// the selected backend, and renders streamed text, images, and agent activity as message bubbles while
/// reflecting the live <see cref="AgentState"/>. A per-turn <see cref="TurnMode"/> lets the user force
/// chat or image generation (model provider); selecting a CLI-agent backend reveals a working folder
/// and resumes the agent's own session across turns (Phase 9).
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string SecretKeyName = "openai";

    private readonly IChatService? _chatService;
    private readonly ISecretStore? _secretStore;
    private readonly IImagePicker? _imagePicker;
    private readonly IChatBackendRegistry? _registry;
    private readonly IWorkspacePicker? _workspacePicker;
    private CancellationTokenSource? _sendCts;

    // The compact running state the reducer folds each turn into; fed back into the next turn's Router.
    private ConversationState _conversationState = ConversationState.Empty;

    // The CLI agent's own session, carried across turns so it resumes (skips Aimon's reducer).
    private AgentSessionId? _agentSessionId;

    // The profile activated in the Profiles tab; its personality + hard settings drive each turn.
    private Profile? _activeProfile;

    /// <summary>The avatar shown beside the chat; it reacts to this turn's <see cref="AgentState"/> changes.</summary>
    public AvatarViewModel Avatar { get; }

    /// <summary>The rendered transcript: text and image bubbles.</summary>
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    /// <summary>Pending image inputs shown as thumbnails until the turn is sent.</summary>
    public ObservableCollection<AttachmentViewModel> Attachments { get; } = [];

    /// <summary>The selectable turn modes for the mode dropdown.</summary>
    public IReadOnlyList<TurnMode> Modes { get; } = Enum.GetValues<TurnMode>();

    /// <summary>The backends available in the picker: the model provider plus any CLI agents.</summary>
    public IReadOnlyList<BackendDescriptor> Backends { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAgentBackend))]
    [NotifyPropertyChangedFor(nameof(IsModelBackend))]
    public partial BackendDescriptor SelectedBackend { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WorkspaceDisplay))]
    public partial string? WorkspacePath { get; set; }

    [ObservableProperty]
    public partial string BackendStatus { get; set; }

    [ObservableProperty]
    public partial string ApiKey { get; set; }

    [ObservableProperty]
    public partial string Model { get; set; }

    [ObservableProperty]
    public partial TurnMode SelectedMode { get; set; }

    [ObservableProperty]
    public partial string Input { get; set; }

    [ObservableProperty]
    public partial string StateLabel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>True when a CLI-agent backend is selected (reveals the workspace controls).</summary>
    public bool IsAgentBackend => SelectedBackend is { Kind: BackendKind.CliAgent };

    /// <summary>True when the built-in model provider is selected (reveals the key/model controls).</summary>
    public bool IsModelBackend => !IsAgentBackend;

    /// <summary>The chosen working folder, or a placeholder when none is selected yet.</summary>
    public string WorkspaceDisplay => string.IsNullOrEmpty(WorkspacePath) ? "No folder selected" : WorkspacePath;

    // Design-time constructor for the XAML previewer.
    public MainViewModel()
    {
        Avatar = new AvatarViewModel();
        Backends = [DesignBackend()];
        SelectedBackend = Backends[0];
        BackendStatus = "";
        ApiKey = "";
        Model = "gpt-5.4-mini";
        SelectedMode = TurnMode.Auto;
        Input = "";
        StateLabel = AgentState.Idle.ToString();
        Messages.Add(ChatMessageViewModel.FromSystem("Aimon chat (design-time preview)."));
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public MainViewModel(
        IChatService chatService,
        ISecretStore secretStore,
        AvatarViewModel avatar,
        IImagePicker imagePicker,
        IChatBackendRegistry registry,
        IWorkspacePicker workspacePicker)
    {
        _chatService = chatService;
        _secretStore = secretStore;
        _imagePicker = imagePicker;
        _registry = registry;
        _workspacePicker = workspacePicker;
        Avatar = avatar;
        Backends = registry.Descriptors;
        SelectedBackend = Backends[0];
        BackendStatus = "";
        ApiKey = "";
        Model = "gpt-5.4-mini";
        SelectedMode = TurnMode.Auto;
        Input = "";
        StateLabel = AgentState.Idle.ToString();
    }

    [RelayCommand]
    private async Task SaveKeyAsync()
    {
        if (_secretStore is null || string.IsNullOrWhiteSpace(ApiKey))
            return;

        await _secretStore.SetAsync(SecretKeyName, ApiKey.Trim(), CancellationToken.None);
        ApiKey = "";
        Messages.Add(ChatMessageViewModel.FromSystem("[API key saved to the OS secret store.]"));
    }

    [RelayCommand]
    private async Task PickWorkspaceAsync()
    {
        if (_workspacePicker is null)
            return;

        var path = await _workspacePicker.PickWorkspaceAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            WorkspacePath = path;
            _agentSessionId = null; // a new folder starts a fresh agent session
        }
    }

    [RelayCommand]
    private async Task AttachImagesAsync()
    {
        if (_imagePicker is null)
            return;

        var picked = await _imagePicker.PickImagesAsync();
        foreach (var image in picked)
            Attachments.Add(new AttachmentViewModel(ToBitmap(image.Data), image.Data, image.MediaType, image.FileName, RemoveAttachmentCommand));
    }

    [RelayCommand]
    private void RemoveAttachment(AttachmentViewModel attachment)
    {
        if (attachment is not null)
            Attachments.Remove(attachment);
    }

    [RelayCommand]
    private async Task SaveImageAsync(byte[]? data)
    {
        if (_imagePicker is null || data is null || data.Length == 0)
            return;

        await _imagePicker.SaveImageAsync(data, $"aimon-{DateTime.Now:yyyyMMdd-HHmmss}.png");
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_chatService is null)
            return;
        if (IsModelBackend && string.IsNullOrWhiteSpace(Model))
            return;
        if (IsAgentBackend && string.IsNullOrWhiteSpace(WorkspacePath))
        {
            Messages.Add(ChatMessageViewModel.FromSystem("[Choose a working folder for this backend first.]"));
            return;
        }

        var userText = Input.Trim();
        var attachments = Attachments.Select(a => new ImageAttachment(a.Data, a.MediaType)).ToList();
        if (userText.Length == 0 && attachments.Count == 0)
            return;

        Input = "";
        Messages.Add(ChatMessageViewModel.FromUser(userText.Length > 0 ? userText : "(image)"));
        foreach (var attachment in Attachments)
            Messages.Add(ChatMessageViewModel.UserImage(attachment.Thumbnail, attachment.Data));
        Attachments.Clear();

        IsBusy = true;
        _sendCts = new CancellationTokenSource();
        Avatar.OnAgentState(AgentState.Listening);

        try
        {
            var userMessage = attachments.Count > 0
                ? Message.User(userText, attachments)
                : Message.User(userText);

            var request = new SendMessageRequest
            {
                Profile = ResolveTurnProfile(),
                ConversationState = _conversationState,
                UserMessage = userMessage,
                Mode = SelectedMode,
                Backend = SelectedBackend.Id,
                Workspace = IsAgentBackend ? new WorkspaceContext(WorkspacePath!) : null,
                SessionId = _agentSessionId,
                PermissionMode = AgentPermissionMode.Full,
            };

            ChatMessageViewModel? streamingAssistant = null;

            await foreach (var item in _chatService.SendAsync(request, _sendCts.Token))
            {
                switch (item)
                {
                    case ProviderStreamItem.TextDelta delta:
                        if (streamingAssistant is null)
                        {
                            streamingAssistant = ChatMessageViewModel.AssistantText("");
                            Messages.Add(streamingAssistant);
                        }

                        streamingAssistant.Text += delta.Text;
                        break;
                    case ProviderStreamItem.ImageArtifact image:
                        streamingAssistant = null;
                        Messages.Add(ChatMessageViewModel.AssistantImage(ToBitmap(image.Data), image.Data, SaveImageCommand));
                        break;
                    case ProviderStreamItem.StateChange stateChange:
                        StateLabel = stateChange.State.ToString();
                        Avatar.OnAgentState(stateChange.State);
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "conversation", Payload: ConversationState next }:
                        _conversationState = next;
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "session", Payload: string sessionId }:
                        _agentSessionId = AgentSessionId.From(sessionId);
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "tool", Payload: string tool }:
                        streamingAssistant = null;
                        Messages.Add(ChatMessageViewModel.FromSystem($"[tool: {tool}]"));
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "file-edit", Payload: string path }:
                        streamingAssistant = null;
                        Messages.Add(ChatMessageViewModel.FromSystem($"[edited: {path}]"));
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "usage", Payload: string usage }:
                        Messages.Add(ChatMessageViewModel.FromSystem($"[{usage}]"));
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "blocked" } blocked:
                        Messages.Add(ChatMessageViewModel.FromSystem($"[blocked: {blocked.Payload}]"));
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "error" or "agent-error" } failure:
                        Messages.Add(ChatMessageViewModel.FromSystem($"[error: {failure.Payload}]"));
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Messages.Add(ChatMessageViewModel.FromSystem("[cancelled]"));
        }
        catch (HttpRequestException ex)
        {
            StateLabel = AgentState.Error.ToString();
            Avatar.OnAgentState(AgentState.Error);
            Messages.Add(ChatMessageViewModel.FromSystem($"[error: {ex.Message}]"));
        }
        finally
        {
            IsBusy = false;
            _sendCts?.Dispose();
            _sendCts = null;
        }
    }

    /// <summary>Adopts the profile activated in the Profiles tab and starts a fresh conversation with it.</summary>
    public void SetActiveProfile(Profile profile)
    {
        _activeProfile = profile;
        _conversationState = ConversationState.Empty;
        _agentSessionId = null;

        var preferred = profile.Structured.ModelPreferences.Preferred;
        if (preferred.Count > 0 && !string.IsNullOrWhiteSpace(preferred[0].Model))
            Model = preferred[0].Model;

        Messages.Add(ChatMessageViewModel.FromSystem($"[Active profile: {profile.Name}]"));
    }

    // Re-check availability and reset the session whenever the selected backend changes.
    partial void OnSelectedBackendChanged(BackendDescriptor value)
    {
        _agentSessionId = null;
        BackendStatus = string.Empty;

        if (value is { Kind: BackendKind.CliAgent })
            _ = ProbeAvailabilityAsync(value);
    }

    private async Task ProbeAvailabilityAsync(BackendDescriptor backend)
    {
        if (_registry is null || !_registry.TryGetAgent(backend.Id, out var agent))
            return;

        BackendStatus = "Checking…";
        try
        {
            var availability = await agent.CheckAvailabilityAsync(CancellationToken.None);
            BackendStatus = FormatAvailability(backend, availability);
        }
        catch (OperationCanceledException)
        {
            BackendStatus = string.Empty;
        }
    }

    private static string FormatAvailability(BackendDescriptor backend, BackendAvailability availability)
    {
        if (!availability.IsAvailable)
            return availability.Reason ?? "Unavailable.";

        return availability.Version is { Length: > 0 } version
            ? $"{backend.DisplayName} {version} detected"
            : $"{backend.DisplayName} detected";
    }

    private bool CanSend() => !IsBusy;

    private static Bitmap ToBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return new Bitmap(stream);
    }

    // Use the active profile (personality, rules, hard settings) for the turn. For the model provider,
    // let the Model box pick the model; the agent path ignores it (the CLI uses its own model).
    private Profile ResolveTurnProfile()
    {
        var baseProfile = _activeProfile ?? DefaultProfile();
        if (IsAgentBackend)
            return baseProfile;

        return baseProfile with
        {
            Structured = baseProfile.Structured with
            {
                ModelPreferences = baseProfile.Structured.ModelPreferences with
                {
                    Preferred = [new ModelRef("openai", Model.Trim())],
                },
            },
        };
    }

    private static Profile DefaultProfile() => new()
    {
        Id = ProfileId.New(),
        Name = "Default",
        Structured = new StructuredProfile
        {
            Personality = new Personality { Persona = "Aimon, a friendly and concise assistant" },
        },
    };

    private static BackendDescriptor DesignBackend() => new()
    {
        Id = BackendId.From("openai"),
        DisplayName = "OpenAI",
        Kind = BackendKind.ModelProvider,
        SupportsImages = true,
        AuthKind = BackendAuthKind.ApiKey,
    };
}
