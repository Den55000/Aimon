using System.Collections.ObjectModel;
using Aimon.Application.Abstractions;
using Aimon.Application.Pipeline;
using Aimon.Desktop.Services;
using Aimon.Domain;
using Aimon.Domain.Conversations;
using Aimon.Domain.Models;
using Aimon.Domain.Pipeline;
using Aimon.Domain.Profiles;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// Streaming chat view model. It saves the API key, sends a turn through <see cref="IChatService"/>,
/// and renders streamed text and generated/attached images as a list of message bubbles while
/// reflecting the live <see cref="AgentState"/>. A per-turn <see cref="TurnMode"/> lets the user
/// force chat or image generation, or leave it on Auto (the model decides via its image tool).
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string SecretKeyName = "openai";

    private readonly IChatService? _chatService;
    private readonly ISecretStore? _secretStore;
    private readonly IImagePicker? _imagePicker;
    private CancellationTokenSource? _sendCts;

    // The compact running state the reducer folds each turn into; fed back into the next turn's Router.
    private ConversationState _conversationState = ConversationState.Empty;

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

    // Design-time constructor for the XAML previewer.
    public MainViewModel()
    {
        Avatar = new AvatarViewModel();
        ApiKey = "";
        Model = "gpt-5.4-mini";
        SelectedMode = TurnMode.Auto;
        Input = "";
        StateLabel = AgentState.Idle.ToString();
        Messages.Add(ChatMessageViewModel.FromSystem("Aimon chat (design-time preview)."));
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public MainViewModel(IChatService chatService, ISecretStore secretStore, AvatarViewModel avatar, IImagePicker imagePicker)
    {
        _chatService = chatService;
        _secretStore = secretStore;
        _imagePicker = imagePicker;
        Avatar = avatar;
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
        if (_chatService is null || string.IsNullOrWhiteSpace(Model))
            return;

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
                Profile = BuildTurnProfile(Model.Trim()),
                ConversationState = _conversationState,
                UserMessage = userMessage,
                Mode = SelectedMode,
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
                    case ProviderStreamItem.ProviderEvent { Kind: "blocked" } blocked:
                        Messages.Add(ChatMessageViewModel.FromSystem($"[blocked: {blocked.Payload}]"));
                        break;
                    case ProviderStreamItem.ProviderEvent { Kind: "error" } failure:
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

        var preferred = profile.Structured.ModelPreferences.Preferred;
        if (preferred.Count > 0 && !string.IsNullOrWhiteSpace(preferred[0].Model))
            Model = preferred[0].Model;

        Messages.Add(ChatMessageViewModel.FromSystem($"[Active profile: {profile.Name}]"));
    }

    private bool CanSend() => !IsBusy;

    private static Bitmap ToBitmap(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return new Bitmap(stream);
    }

    // Use the active profile (personality, rules, hard settings) for the turn, but let the Model box
    // pick the model; fall back to a built-in default when no profile is active.
    private Profile BuildTurnProfile(string model)
    {
        var baseProfile = _activeProfile ?? DefaultProfile();

        return baseProfile with
        {
            Structured = baseProfile.Structured with
            {
                ModelPreferences = baseProfile.Structured.ModelPreferences with
                {
                    Preferred = [new ModelRef("openai", model)],
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
}
