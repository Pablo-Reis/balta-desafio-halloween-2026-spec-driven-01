namespace Productivity.Domain;

public enum DemandStatus
{
    Running,
    Paused,
    Finished
}

public enum DemandEventKind
{
    Started,
    Paused,
    Resumed,
    Finished
}

public enum NoteSource
{
    Hotkey,
    Application
}

public enum AppTheme
{
    System,
    Light,
    Dark
}
