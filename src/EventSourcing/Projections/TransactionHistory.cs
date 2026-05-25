using Kuestenlogik.Surgewave.Samples.EventSourcing.Events;

namespace Kuestenlogik.Surgewave.Samples.EventSourcing.Projections;

/// <summary>
/// Transaction history projection showing all account movements.
/// </summary>
public sealed class TransactionHistory
{
    private readonly List<TransactionRecord> _transactions = [];

    public IReadOnlyList<TransactionRecord> Transactions => _transactions;

    public string AccountId { get; private set; } = "";

    /// <summary>
    /// Apply an event to update the transaction history.
    /// </summary>
    public void Apply(AccountEvent @event)
    {
        switch (@event)
        {
            case AccountOpened opened:
                AccountId = opened.AccountId;
                if (opened.InitialDepositCents > 0)
                {
                    _transactions.Add(new TransactionRecord
                    {
                        TransactionId = opened.EventId,
                        Timestamp = opened.Timestamp,
                        Type = TransactionType.Deposit,
                        AmountCents = opened.InitialDepositCents,
                        Description = "Initial deposit",
                        RunningBalanceCents = opened.InitialDepositCents
                    });
                }
                break;

            case MoneyDeposited deposited:
                var balanceAfterDeposit = _transactions.Count > 0
                    ? _transactions[^1].RunningBalanceCents + deposited.AmountCents
                    : deposited.AmountCents;

                _transactions.Add(new TransactionRecord
                {
                    TransactionId = deposited.EventId,
                    Timestamp = deposited.Timestamp,
                    Type = TransactionType.Deposit,
                    AmountCents = deposited.AmountCents,
                    Description = deposited.Description,
                    RunningBalanceCents = balanceAfterDeposit
                });
                break;

            case MoneyWithdrawn withdrawn:
                var balanceAfterWithdraw = _transactions.Count > 0
                    ? _transactions[^1].RunningBalanceCents - withdrawn.AmountCents
                    : -withdrawn.AmountCents;

                _transactions.Add(new TransactionRecord
                {
                    TransactionId = withdrawn.EventId,
                    Timestamp = withdrawn.Timestamp,
                    Type = TransactionType.Withdrawal,
                    AmountCents = withdrawn.AmountCents,
                    Description = withdrawn.Description,
                    RunningBalanceCents = balanceAfterWithdraw
                });
                break;

            case AccountClosed closed:
                _transactions.Add(new TransactionRecord
                {
                    TransactionId = closed.EventId,
                    Timestamp = closed.Timestamp,
                    Type = TransactionType.Closure,
                    AmountCents = 0,
                    Description = $"Account closed: {closed.Reason}",
                    RunningBalanceCents = closed.FinalBalanceCents
                });
                break;
        }
    }

    /// <summary>
    /// Rebuild transaction history from a sequence of events.
    /// </summary>
    public static TransactionHistory FromEvents(IEnumerable<AccountEvent> events)
    {
        var history = new TransactionHistory();
        foreach (var @event in events)
        {
            history.Apply(@event);
        }
        return history;
    }
}

/// <summary>
/// A single transaction record.
/// </summary>
public sealed record TransactionRecord
{
    public required string TransactionId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required TransactionType Type { get; init; }
    public required long AmountCents { get; init; }
    public required string Description { get; init; }
    public required long RunningBalanceCents { get; init; }

    public decimal Amount => AmountCents / 100m;
    public decimal RunningBalance => RunningBalanceCents / 100m;
}

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Closure
}
