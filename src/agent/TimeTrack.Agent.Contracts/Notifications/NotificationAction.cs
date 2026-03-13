namespace TimeTrack.Agent.Contracts.Notifications;

/// <summary>
/// Representa uma ação que pode ser executada a partir de um botão na notificação
///
/// SRP: Apenas encapsula dados de uma ação de botão
/// </summary>
public sealed record NotificationAction
{
    /// <summary>
    /// Label exibido no botão
    /// </summary>
    public string Label { get; init; }

    /// <summary>
    /// Comando IPC que será enviado quando o botão for clicado
    /// Ex: "StartBreak", "SkipBreak", "ResumeTracking"
    /// </summary>
    public string IpcCommand { get; init; }

    /// <summary>
    /// Argumentos opcionais para o comando IPC
    /// </summary>
    public string? IpcArguments { get; init; }

    public NotificationAction(string label, string ipcCommand, string? ipcArguments = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be empty", nameof(label));

        if (string.IsNullOrWhiteSpace(ipcCommand))
            throw new ArgumentException("IPC command cannot be empty", nameof(ipcCommand));

        Label = label;
        IpcCommand = ipcCommand;
        IpcArguments = ipcArguments;
    }
}
