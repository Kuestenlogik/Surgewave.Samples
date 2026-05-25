using Kuestenlogik.Surgewave.Samples.EventSourcing.Events;

namespace Kuestenlogik.Surgewave.Samples.EventSourcing.Projections;

/// <summary>
/// Current state projection for an account.
/// Rebuilt by replaying all events for the account.
/// </summary>
public sealed class AccountState
{
    public string AccountId { get; private set; } = "";
    public string HolderName { get; private set; } = "";
    public string AccountType { get; private set; } = "";
    public long BalanceCents { get; private set; }
    public bool IsOpen { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public long LastSequenceNumber { get; private set; }

    public decimal Balance => BalanceCents / 100m;

    /// <summary>
    /// Apply an event to update the state.
    /// </summary>
    public void Apply(AccountEvent @event)
    {
        LastSequenceNumber = @event.SequenceNumber;

        switch (@event)
        {
            case AccountOpened opened:
                AccountId = opened.AccountId;
                HolderName = opened.HolderName;
                AccountType = opened.AccountType;
                BalanceCents = opened.InitialDepositCents;
                IsOpen = true;
                OpenedAt = opened.Timestamp;
                break;

            case MoneyDeposited deposited:
                BalanceCents += deposited.AmountCents;
                break;

            case MoneyWithdrawn withdrawn:
                BalanceCents -= withdrawn.AmountCents;
                break;

            case AccountClosed closed:
                IsOpen = false;
                ClosedAt = closed.Timestamp;
                break;
        }
    }

    /// <summary>
    /// Rebuild state from a sequence of events.
    /// </summary>
    public static AccountState FromEvents(IEnumerable<AccountEvent> events)
    {
        var state = new AccountState();
        foreach (var @event in events)
        {
            state.Apply(@event);
        }
        return state;
    }
}
