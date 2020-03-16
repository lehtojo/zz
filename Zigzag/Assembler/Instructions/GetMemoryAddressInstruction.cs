public class GetMemoryAddressInstruction : Instruction
{
    public Result Base { get; private set; }
    public Result Offset { get; private set; }
    public int Stride { get; private set; }

    public GetMemoryAddressInstruction(Unit unit, Result @base, Result offset, int stride) : base(unit)
    {
        Base = @base;
        Offset = offset;
        Stride = stride;
    }

    public override void Build()
    {
        Memory.MoveToRegister(Unit, Base);
        Memory.MoveToRegister(Unit, Offset);
        Result.Set(new ComplexMemoryHandle(Base, Offset, Stride));
    }

    public override Result[] GetHandles()
    {
        return new Result[] { Result, Base, Offset };
    }

    public override InstructionType GetInstructionType()
    {
        return InstructionType.GET_MEMORY_ADDRESS;
    }

    public override Result? GetDestination()
    {
        return null;   
    }
}