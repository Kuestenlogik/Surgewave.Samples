using Kuestenlogik.Surgewave.Samples.EventSourcing.Events;
using Kuestenlogik.Surgewave.Samples.EventSourcing.Projections;

namespace Kuestenlogik.Surgewave.Samples.EventSourcing;

/// <summary>
/// Bank account aggregate that encapsulates business logic and event generation.
/// </summary>
public sealed class BankAccount
{
    private readonly EventStore _eventStore;
    private readonly string _accountId;
    private long _sequenceNumber;

    public AccountState State { get; private set; } = new();
    public TransactionHistory History { get; private set; } = new();

    public string AccountId => _accountId;

    private BankAccount(EventStore eventStore, string accountId)
    {
        _eventStore = eventStore;
        _accountId = accountId;
    }

    /// <summary>
    /// Create a new bank account.
    /// </summary>
    public static async Task<BankAccount> OpenAsync(
        EventStore eventStore,
        string holderName,
        string accountType,
        decimal initialDeposit)
    {
        var accountId = $"ACC-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var account = new BankAccount(eventStore, accountId);

        var @event = new AccountOpened
        {
            EventId = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            Timestamp = DateTimeOffset.UtcNow,
            SequenceNumber = ++account._sequenceNumber,
            HolderName = holderName,
            AccountType = accountType,
            InitialDepositCents = (long)(initialDeposit * 100)
        };

        account.ApplyEvent(@event);
        await eventStore.AppendAsync(@event);

        return account;
    }

    /// <summary>
    /// Load an existing account from the event store.
    /// </summary>
    public static async Task<BankAccount?> LoadAsync(EventStore eventStore, string accountId)
    {
        var events = await eventStore.LoadEventsAsync(accountId);
        if (events.Count == 0)
            return null;

        var account = new BankAccount(eventStore, accountId);
        foreach (var @event in events)
        {
            account.ApplyEvent(@event);
            account._sequenceNumber = @event.SequenceNumber;
        }

        return account;
    }

    /// <summary>
    /// Deposit money into the account.
    /// </summary>
    public async Task DepositAsync(decimal amount, string description)
    {
        if (!State.IsOpen)
            throw new InvalidOperationException("Cannot deposit to a closed account");

        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive", nameof(amount));

        var @event = new MoneyDeposited
        {
            EventId = Guid.NewGuid().ToString("N"),
            AccountId = _accountId,
            Timestamp = DateTimeOffset.UtcNow,
            SequenceNumber = ++_sequenceNumber,
            AmountCents = (long)(amount * 100),
            Description = description
        };

        ApplyEvent(@event);
        await _eventStore.AppendAsync(@event);
    }

    /// <summary>
    /// Withdraw money from the account.
    /// </summary>
    public async Task WithdrawAsync(decimal amount, string description)
    {
        if (!State.IsOpen)
            throw new InvalidOperationException("Cannot withdraw from a closed account");

        if (amount <= 0)
            throw new ArgumentException("Withdrawal amount must be positive", nameof(amount));

        var amountCents = (long)(amount * 100);
        if (amountCents > State.BalanceCents)
            throw new InvalidOperationException($"Insufficient funds. Balance: {State.Balance:C}, Requested: {amount:C}");

        var @event = new MoneyWithdrawn
        {
            EventId = Guid.NewGuid().ToString("N"),
            AccountId = _accountId,
            Timestamp = DateTimeOffset.UtcNow,
            SequenceNumber = ++_sequenceNumber,
            AmountCents = amountCents,
            Description = description
        };

        ApplyEvent(@event);
        await _eventStore.AppendAsync(@event);
    }

    /// <summary>
    /// Close the account.
    /// </summary>
    public async Task CloseAsync(string reason)
    {
        if (!State.IsOpen)
            throw new InvalidOperationException("Account is already closed");

        var @event = new AccountClosed
        {
            EventId = Guid.NewGuid().ToString("N"),
            AccountId = _accountId,
            Timestamp = DateTimeOffset.UtcNow,
            SequenceNumber = ++_sequenceNumber,
            Reason = reason,
            FinalBalanceCents = State.BalanceCents
        };

        ApplyEvent(@event);
        await _eventStore.AppendAsync(@event);
    }

    private void ApplyEvent(AccountEvent @event)
    {
        State.Apply(@event);
        History.Apply(@event);
    }
}
