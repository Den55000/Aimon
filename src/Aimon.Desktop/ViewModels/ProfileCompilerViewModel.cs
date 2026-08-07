using System.Text;
using Aimon.Application.Abstractions;
using Aimon.Application.Authoring;
using Aimon.Domain.Profiles;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aimon.Desktop.ViewModels;

/// <summary>
/// Drives the Profile Compiler authoring loop from the UI (implementation_plan.md, Phase 5): describe
/// a persona → Compile a draft → review it → Revise (recompile) as needed → Confirm to persist the
/// natural-language source and structured draft together. The API key is shared with the chat tab via
/// the OS secret store.
/// </summary>
public partial class ProfileCompilerViewModel : ViewModelBase
{
    private readonly IProfileDraftSessionFactory? _sessionFactory;
    private ProfileDraftSession? _session;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompileCommand))]
    public partial string Name { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompileCommand))]
    public partial string Description { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReviseCommand))]
    public partial string RevisionInstruction { get; set; }

    [ObservableProperty]
    public partial string DraftPreview { get; set; }

    [ObservableProperty]
    public partial string ValidationSummary { get; set; }

    [ObservableProperty]
    public partial string StatusLabel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReviseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReviseCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool HasDraft { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial bool IsDraftValid { get; set; }

    // Design-time constructor for the XAML previewer.
    public ProfileCompilerViewModel()
    {
        Name = "";
        Description = "";
        RevisionInstruction = "";
        DraftPreview = "The compiled profile will appear here.";
        ValidationSummary = "";
        StatusLabel = "Describe a persona, then Compile.";
    }

    // Runtime constructor resolved through the DI container in App.axaml.cs.
    public ProfileCompilerViewModel(IProfileDraftSessionFactory sessionFactory)
        : this()
    {
        _sessionFactory = sessionFactory;
    }

    [RelayCommand(CanExecute = nameof(CanCompile))]
    private async Task CompileAsync()
    {
        if (_sessionFactory is null || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Description))
            return;

        IsBusy = true;
        StatusLabel = "Compiling…";
        try
        {
            _session = await _sessionFactory.BeginAsync(Name.Trim(), Description.Trim(), CancellationToken.None);
            OnDraftUpdated("Draft ready — review, revise, or confirm.");
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRevise))]
    private async Task ReviseAsync()
    {
        if (_session is null || string.IsNullOrWhiteSpace(RevisionInstruction))
            return;

        IsBusy = true;
        StatusLabel = "Revising…";
        try
        {
            await _session.ReviseAsync(RevisionInstruction.Trim(), CancellationToken.None);
            RevisionInstruction = "";
            OnDraftUpdated($"Revised (revision {_session.RevisionCount}).");
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        if (_session is null || !_session.IsValid)
            return;

        IsBusy = true;
        StatusLabel = "Saving…";
        try
        {
            var profile = await _session.ConfirmAsync(CancellationToken.None);
            _session = null;
            HasDraft = false;
            IsDraftValid = false;
            StatusLabel = $"Saved profile “{profile.Name}” ({profile.Id}). Compile again to author another.";
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCompile() => !IsBusy && !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Description);

    private bool CanRevise() => !IsBusy && HasDraft && !string.IsNullOrWhiteSpace(RevisionInstruction);

    private bool CanConfirm() => !IsBusy && HasDraft && IsDraftValid;

    private void OnDraftUpdated(string status)
    {
        var draft = _session!.CurrentDraft;
        DraftPreview = Render(draft);
        HasDraft = true;
        IsDraftValid = _session.IsValid;
        ValidationSummary = _session.ValidationIssues.Count == 0
            ? "No issues — ready to confirm."
            : "Resolve before confirming:\n • " + string.Join("\n • ", _session.ValidationIssues);
        StatusLabel = status;
    }

    private void OnError(Exception ex)
    {
        var message = ex switch
        {
            HttpRequestException => $"Network/API error: {ex.Message}",
            InvalidDataException => $"The compiler returned unusable output: {ex.Message}",
            _ => ex.Message, // e.g. no API key configured, or an invalid confirm.
        };

        StatusLabel = $"Error: {message}";
    }

    private static string Render(StructuredProfile profile)
    {
        var builder = new StringBuilder();

        var personality = profile.Personality;
        builder.AppendLine("PERSONALITY");
        builder.AppendLine($"  Persona: {OrDash(personality.Persona)}");
        builder.AppendLine($"  Tone:    {OrDash(personality.Tone)}");
        builder.AppendLine($"  Traits:  {OrDash(string.Join(", ", personality.Traits))}");
        builder.AppendLine();

        var rules = profile.InteractionRules;
        builder.AppendLine("INTERACTION RULES");
        builder.AppendLine($"  Do:      {OrDash(string.Join("; ", rules.Dos))}");
        builder.AppendLine($"  Don't:   {OrDash(string.Join("; ", rules.Donts))}");
        builder.AppendLine($"  Verbosity: {rules.Verbosity}");
        if (!string.IsNullOrWhiteSpace(rules.FormattingPreference))
            builder.AppendLine($"  Formatting: {rules.FormattingPreference}");

        if (profile.TaskStyles.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("TASK STYLES");
            foreach (var taskStyle in profile.TaskStyles)
            {
                builder.AppendLine($"  • {taskStyle.TaskName}");
                if (!string.IsNullOrWhiteSpace(taskStyle.SystemPromptAddition))
                    builder.AppendLine($"      {taskStyle.SystemPromptAddition}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string OrDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
