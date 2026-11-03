public readonly struct StatusApplyResult
{
    public bool Accepted { get; }
    public bool Created { get; }
    public int PreviousStacks { get; }
    public int CurrentStacks { get; }

    public StatusApplyResult(bool accepted, bool created, int previousStacks, int currentStacks)
    {
        Accepted = accepted;
        Created = created;
        PreviousStacks = previousStacks;
        CurrentStacks = currentStacks;
    }

    public static StatusApplyResult Rejected => new StatusApplyResult(false, false, 0, 0);
}
