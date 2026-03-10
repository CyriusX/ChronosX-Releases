namespace TimeTrack.Agent.Domain.Common;

/// <summary>
/// Classe base para entidades do domínio
/// </summary>
public abstract class EntityBase : IEquatable<EntityBase>
{
    public Guid Id { get; protected set; }

    protected EntityBase() { }

    protected EntityBase(Guid id)
    {
        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not EntityBase other)
            return false;

        if (GetType() != other.GetType())
            return false;

        if (Id == Guid.Empty || other.Id == Guid.Empty)
            return false;

        return Id == other.Id;
    }

    public bool Equals(EntityBase? other)
    {
        return other is not null && Equals((object)other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(EntityBase? left, EntityBase? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(EntityBase? left, EntityBase? right)
    {
        return !(left == right);
    }
}
