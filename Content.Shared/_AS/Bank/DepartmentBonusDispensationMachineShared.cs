// Aurora Song - Department Bonus Dispensation Machine Shared
// UI key and messages for the department bonus machine

using Robust.Shared.Serialization;

namespace Content.Shared._AS.Bank;

[Serializable, NetSerializable]
public enum DepartmentBonusDispensationMachineUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DepartmentBonusDispensationMachineBoundUserInterfaceState : BoundUserInterfaceState
{
    public string DepartmentName { get; }
    public float TaxRate { get; }
    public int StoredAmount { get; }
    public int MaxStoredAmount { get; }
    public bool Enabled { get; }
    public TimeSpan NextWithdrawal { get; }

    public DepartmentBonusDispensationMachineBoundUserInterfaceState(
        string departmentName,
        float taxRate,
        int storedAmount,
        int maxStoredAmount,
        bool enabled,
        TimeSpan nextWithdrawal)
    {
        DepartmentName = departmentName;
        TaxRate = taxRate;
        StoredAmount = storedAmount;
        MaxStoredAmount = maxStoredAmount;
        Enabled = enabled;
        NextWithdrawal = nextWithdrawal;
    }
}

[Serializable, NetSerializable]
public sealed class DepartmentBonusDispensationMachineEjectMessage : BoundUserInterfaceMessage
{
}
