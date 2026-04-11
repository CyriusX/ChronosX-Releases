namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Where a Project originated and who "owns" its task list.
/// Local projects are fully editable inside TimeTrack. Linear projects are
/// mirrored from Linear and treated as read-only except for status moves.
/// </summary>
public enum ProjectSyncSource
{
    Local = 1,
    Linear = 2
}
