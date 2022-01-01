namespace Njsast.Coverage;

public enum InstrumentedInfoType
{
    Statement,
    Condition, // Uses 2 Indexes: [+0] False [+1] True
    Function,
    SwitchBranch
}