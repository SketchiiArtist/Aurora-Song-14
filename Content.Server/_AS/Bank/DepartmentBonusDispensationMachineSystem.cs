// Aurora Song - Department Bonus Dispensation Machine System
// Handles periodic bonus allocations and currency dispensing

using Content.Server._NF.Bank;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._AS.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AS.Bank;

public sealed class DepartmentBonusDispensationMachineSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DepartmentBonusDispensationMachineComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DepartmentBonusDispensationMachineComponent, DepartmentBonusDispensationMachineEjectMessage>(OnEjectMessage);
    }

    private void OnStartup(EntityUid uid, DepartmentBonusDispensationMachineComponent component, ComponentStartup args)
    {
        // Set initial bonus allocation time
        if (component.NextWithdrawal == TimeSpan.Zero)
            component.NextWithdrawal = _timing.CurTime + TimeSpan.FromSeconds(component.WithdrawalInterval);

        UpdateUI(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DepartmentBonusDispensationMachineComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.Enabled)
                continue;

            // Update UI periodically for open windows (every second)
            component.UiUpdateAccumulator += frameTime;
            if (component.UiUpdateAccumulator >= 1.0f)
            {
                component.UiUpdateAccumulator = 0f;
                UpdateUI(uid, component);
            }

            if (_timing.CurTime < component.NextWithdrawal)
                continue;

            // Attempt bonus allocation
            AttemptWithdrawal(uid, component);

            // Schedule next bonus allocation
            component.NextWithdrawal = _timing.CurTime + TimeSpan.FromSeconds(component.WithdrawalInterval);
            UpdateUI(uid, component);
        }
    }

    private void AttemptWithdrawal(EntityUid uid, DepartmentBonusDispensationMachineComponent component)
    {
        // Check if storage is full
        if (component.StoredAmount >= component.MaxStoredAmount)
            return;

        // Get current department balance
        if (!_bank.TryGetBalance(component.TargetDepartment, out var currentBalance))
            return;

        var balance = currentBalance;
        if (balance <= 0)
            return;

        // Calculate bonus allocation amount (percentage of current balance)
        var withdrawAmount = (int)(balance * component.TaxRate);

        if (withdrawAmount <= 0)
            return;

        // Ensure we don't exceed storage capacity
        var spaceLeft = component.MaxStoredAmount - component.StoredAmount;
        withdrawAmount = Math.Min(withdrawAmount, spaceLeft);

        // Attempt to allocate from department budget
        if (_bank.TrySectorWithdraw(component.TargetDepartment, withdrawAmount, LedgerEntryType.DepartmentTax))
        {
            component.StoredAmount += withdrawAmount;

            // Play sound
            if (component.WithdrawSound != null)
                _audio.PlayPvs(component.WithdrawSound, uid);

            UpdateUI(uid, component);
        }
    }

    private void OnEjectMessage(EntityUid uid, DepartmentBonusDispensationMachineComponent component, DepartmentBonusDispensationMachineEjectMessage args)
    {
        if (component.StoredAmount <= 0)
        {
            _popup.PopupEntity("The machine is empty!", uid, args.Actor);
            return;
        }

        // Spawn currency
        var amountToEject = component.StoredAmount;
        SpawnCurrency(uid, component, amountToEject);

        // Clear storage
        component.StoredAmount = 0;

        // Play sound
        if (component.EjectSound != null)
            _audio.PlayPvs(component.EjectSound, uid);

        _popup.PopupEntity($"Dispensed {amountToEject} spacebucks in staff bonuses!", uid, args.Actor);

        UpdateUI(uid, component);
    }

    private void SpawnCurrency(EntityUid uid, DepartmentBonusDispensationMachineComponent component, int amount)
    {
        var xform = Transform(uid);

        // Spawn in appropriate denominations
        while (amount > 0)
        {
            // Determine denomination to spawn
            string protoId;
            int denom;

            if (amount >= 5000)
            {
                protoId = "SpaceCash5000";
                denom = 5000;
            }
            else if (amount >= 1000)
            {
                protoId = "SpaceCash1000";
                denom = 1000;
            }
            else if (amount >= 500)
            {
                protoId = "SpaceCash500";
                denom = 500;
            }
            else if (amount >= 100)
            {
                protoId = "SpaceCash100";
                denom = 100;
            }
            else // amount >= 10, minimum denomination
            {
                protoId = "SpaceCash10";
                denom = 10;
            }

            // Calculate how many of this denomination to spawn
            var count = amount / denom;
            if (count > 0)
            {
                // Spawn as a stack if more than 1
                var cashEnt = Spawn(protoId, xform.Coordinates);
                if (count > 1 && TryComp<StackComponent>(cashEnt, out var stack))
                {
                    var maxCount = _stack.GetMaxCount(stack);
                    _stack.SetCount(cashEnt, Math.Min(count, maxCount), stack);
                    amount -= denom * Math.Min(count, maxCount);
                }
                else
                {
                    amount -= denom;
                }
            }
        }
    }

    private void UpdateUI(EntityUid uid, DepartmentBonusDispensationMachineComponent component)
    {
        var state = new DepartmentBonusDispensationMachineBoundUserInterfaceState(
            component.TargetDepartment.ToString(),
            component.TaxRate,
            component.StoredAmount,
            component.MaxStoredAmount,
            component.Enabled,
            component.NextWithdrawal
        );

        _ui.SetUiState(uid, DepartmentBonusDispensationMachineUiKey.Key, state);
    }
}
