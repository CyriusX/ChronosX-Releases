using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.Enums;

namespace TimeTrack.Agent.Domain.ValueObjects;

/// <summary>
/// Configuração de Pomodoro
///
/// SRP: Apenas encapsula configuração do Pomodoro
/// </summary>
public sealed class PomodoroConfig : ValueObject
{
    /// <summary>
    /// Duração do foco em minutos (10-180)
    /// </summary>
    public int FocusMinutes { get; }

    /// <summary>
    /// Duração da pausa curta em minutos (5-60)
    /// </summary>
    public int ShortBreakMinutes { get; }

    /// <summary>
    /// Duração da pausa longa em minutos (5-60)
    /// </summary>
    public int LongBreakMinutes { get; }

    /// <summary>
    /// Número de ciclos antes da pausa longa (2-8)
    /// </summary>
    public int CyclesBeforeLongBreak { get; }

    public PomodoroConfig(
        int focusMinutes,
        int shortBreakMinutes,
        int longBreakMinutes,
        int cyclesBeforeLongBreak)
    {
        if (focusMinutes < 10 || focusMinutes > 180)
            throw new ArgumentOutOfRangeException(nameof(focusMinutes), "Focus minutes must be between 10 and 180");

        if (shortBreakMinutes < 5 || shortBreakMinutes > 60)
            throw new ArgumentOutOfRangeException(nameof(shortBreakMinutes), "Short break minutes must be between 5 and 60");

        if (longBreakMinutes < 5 || longBreakMinutes > 60)
            throw new ArgumentOutOfRangeException(nameof(longBreakMinutes), "Long break minutes must be between 5 and 60");

        if (cyclesBeforeLongBreak < 2 || cyclesBeforeLongBreak > 8)
            throw new ArgumentOutOfRangeException(nameof(cyclesBeforeLongBreak), "Cycles before long break must be between 2 and 8");

        FocusMinutes = focusMinutes;
        ShortBreakMinutes = shortBreakMinutes;
        LongBreakMinutes = longBreakMinutes;
        CyclesBeforeLongBreak = cyclesBeforeLongBreak;
    }

    /// <summary>
    /// Configuração padrão do Pomodoro
    /// </summary>
    public static PomodoroConfig Default => new(25, 5, 15, 4);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return FocusMinutes;
        yield return ShortBreakMinutes;
        yield return LongBreakMinutes;
        yield return CyclesBeforeLongBreak;
    }
}

/// <summary>
/// Configuração do Ciclo Ultradian
///
/// SRP: Apenas encapsula configuração do Ultradian
/// </summary>
public sealed class UltradianConfig : ValueObject
{
    /// <summary>
    /// Duração do foco em minutos (10-180)
    /// </summary>
    public int FocusMinutes { get; }

    /// <summary>
    /// Duração da pausa em minutos (5-60)
    /// </summary>
    public int BreakMinutes { get; }

    public UltradianConfig(int focusMinutes, int breakMinutes)
    {
        if (focusMinutes < 10 || focusMinutes > 180)
            throw new ArgumentOutOfRangeException(nameof(focusMinutes), "Focus minutes must be between 10 and 180");

        if (breakMinutes < 5 || breakMinutes > 60)
            throw new ArgumentOutOfRangeException(nameof(breakMinutes), "Break minutes must be between 5 and 60");

        FocusMinutes = focusMinutes;
        BreakMinutes = breakMinutes;
    }

    /// <summary>
    /// Configuração padrão do Ultradian
    /// </summary>
    public static UltradianConfig Default => new(90, 20);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return FocusMinutes;
        yield return BreakMinutes;
    }
}

/// <summary>
/// Política de modo de foco da organização
///
/// SRP: Apenas encapsula a configuração do modo de foco
/// DIP: Usado como contrato de configuração pelo engine
/// </summary>
public sealed class FocusModePolicy : ValueObject
{
    /// <summary>
    /// Se o modo de foco está habilitado
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Tipo de modo de foco
    /// </summary>
    public FocusModeType Mode { get; }

    /// <summary>
    /// Se o usuário pode iniciar/parar ciclos manualmente
    /// </summary>
    public bool AllowUserOverride { get; }

    /// <summary>
    /// Configuração do Pomodoro (se Mode = Pomodoro)
    /// </summary>
    public PomodoroConfig? Pomodoro { get; }

    /// <summary>
    /// Configuração do Ultradian (se Mode = Ultradian)
    /// </summary>
    public UltradianConfig? Ultradian { get; }

    public FocusModePolicy(
        bool enabled,
        FocusModeType mode,
        bool allowUserOverride,
        PomodoroConfig? pomodoro = null,
        UltradianConfig? ultradian = null)
    {
        Enabled = enabled;
        Mode = mode;
        AllowUserOverride = allowUserOverride;
        Pomodoro = pomodoro;
        Ultradian = ultradian;
    }

    /// <summary>
    /// Política desativada padrão
    /// </summary>
    public static FocusModePolicy Disabled => new(false, FocusModeType.None, true);

    /// <summary>
    /// Política Pomodoro padrão
    /// </summary>
    public static FocusModePolicy DefaultPomodoro => new(
        true,
        FocusModeType.Pomodoro,
        true,
        PomodoroConfig.Default,
        null);

    /// <summary>
    /// Política Ultradian padrão
    /// </summary>
    public static FocusModePolicy DefaultUltradian => new(
        true,
        FocusModeType.Ultradian,
        true,
        null,
        UltradianConfig.Default);

    /// <summary>
    /// Obtém a duração do foco em minutos baseado no modo
    /// </summary>
    public int GetFocusMinutes() => Mode switch
    {
        FocusModeType.Pomodoro => Pomodoro?.FocusMinutes ?? 25,
        FocusModeType.Ultradian => Ultradian?.FocusMinutes ?? 90,
        _ => 0
    };

    /// <summary>
    /// Obtém a duração da pausa em minutos baseado no modo e tipo
    /// </summary>
    public int GetBreakMinutes(BreakType breakType = BreakType.Short) => Mode switch
    {
        FocusModeType.Pomodoro => breakType == BreakType.Long
            ? Pomodoro?.LongBreakMinutes ?? 15
            : Pomodoro?.ShortBreakMinutes ?? 5,
        FocusModeType.Ultradian => Ultradian?.BreakMinutes ?? 20,
        _ => 0
    };

    /// <summary>
    /// Obtém o número de ciclos antes da pausa longa (apenas Pomodoro)
    /// </summary>
    public int GetCyclesBeforeLongBreak() => Pomodoro?.CyclesBeforeLongBreak ?? 4;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Enabled;
        yield return Mode;
        yield return AllowUserOverride;
        yield return Pomodoro ?? PomodoroConfig.Default;
        yield return Ultradian ?? UltradianConfig.Default;
    }
}
