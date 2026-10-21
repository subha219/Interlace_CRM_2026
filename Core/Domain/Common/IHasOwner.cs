namespace Domain.Common;

/// <summary>A record that belongs to one user (the person responsible for it).</summary>
public interface IHasOwner
{
    public string? OwnerUserId { get; set; }
}
