using System.Text.Json.Serialization;

namespace Kuestenlogik.Surgewave.Samples.EventSourcing.Events;

/// <summary>
/// Base class for all account events.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "eventType")]
[JsonDerivedType(typeof(AccountOpened), "AccountOpened")]
[JsonDerivedType(typeof(MoneyDeposited), "MoneyDeposited")]
[JsonDerivedType(typeof(MoneyWithdrawn), "MoneyWithdrawn")]
[JsonDerivedType(typeof(AccountClosed), "AccountClosed")]
public abstract record AccountEvent
{
    /// <summary>
    /// Unique event identifier.
    /// </summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Account this event belongs to.
    /// </summary>
    public required string AccountId { get; init; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Sequence number within the account's event stream.
    /// </summary>
    public required long SequenceNumber { get; init; }
}

/// <summary>
/// Event raised when a new account is opened.
/// </summary>
public sealed record AccountOpened : AccountEvent
{
    /// <summary>
    /// Name of the account holder.
    /// </summary>
    public required string HolderName { get; init; }

    /// <summary>
    /// Initial deposit amount in cents.
    /// </summary>
    public required long InitialDepositCents { get; init; }

    /// <summary>
    /// Type of account (Checking, Savings).
    /// </summary>
    public required string AccountType { get; init; }
}

/// <summary>
/// Event raised when money is deposited into an account.
/// </summary>
public sealed record MoneyDeposited : AccountEvent
{
    /// <summary>
    /// Amount deposited in cents.
    /// </summary>
    public required long AmountCents { get; init; }

    /// <summary>
    /// Description of the deposit.
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// Event raised when money is withdrawn from an account.
/// </summary>
public sealed record MoneyWithdrawn : AccountEvent
{
    /// <summary>
    /// Amount withdrawn in cents.
    /// </summary>
    public required long AmountCents { get; init; }

    /// <summary>
    /// Description of the withdrawal.
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// Event raised when an account is closed.
/// </summary>
public sealed record AccountClosed : AccountEvent
{
    /// <summary>
    /// Reason for closing the account.
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Final balance at closure in cents.
    /// </summary>
    public required long FinalBalanceCents { get; init; }
}
