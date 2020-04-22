using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;

public class Lifetime
{
    public int Start { get; set; } = -1;
    public int End { get; set; } = -1;

    public void Reset()
    {
        Start = -1;
        End = -1;
    }

    public bool IsActive(int position)
    {
        return position >= Start && (End == -1 || position <= End);
    }

    public bool IsIntersecting(int start, int end)
    {
        var s1 = Start == -1 ? int.MinValue : Start;
        var e1 = End == -1 ? int.MaxValue : End;

        var s2 = start == -1 ? int.MinValue : start;
        var e2 = end == -1 ? int.MaxValue : end;

        return s1 < e2 && e1 > s2;
    }

    public Lifetime Clone()
    {
        return new Lifetime()
        {
            Start = Start,
            End = End
        };
    }
}

public enum UnitPhase
{
    READ_ONLY_MODE,
    APPEND_MODE,
    BUILD_MODE
}

public class VariableState
{
    public Variable Variable { get; private set; }
    public Register Register { get; private set; }

    public VariableState(Variable variable, Register register)
    {
        Variable = variable;
        Register = register;
    }

    public void Restore(Unit unit)
    {
        if (Register.Handle != null)
        {
            throw new ApplicationException("During state restoration one of the registers was conflicted");
        }

        var current_handle = unit.GetCurrentVariableHandle(Variable);

        if (current_handle != null)
        {
            Register.Handle = current_handle;
            current_handle.Value = new RegisterHandle(Register);
        }
    }
}

public class Unit
{
    public bool Optimize = true;

    public FunctionImplementation Function { get; private set; }

    public List<Register> Registers { get; private set; } = new List<Register>();
    public List<Register> NonVolatileRegisters { get; private set; } = new List<Register>();
    public List<Register> VolatileRegisters { get; private set; } = new List<Register>();
    public List<Register> NonReservedRegisters { get; private set; } = new List<Register>();
    public List<Register> MediaRegisters { get; private set; } = new List<Register>();

    public List<Instruction> Instructions { get; private set; } = new List<Instruction>();

    private StringBuilder Builder { get; set; } = new StringBuilder();

    private int LabelIndex { get; set; } = 0;
    private int StringIndex { get; set; } = 0;

    private bool IsReindexingNeeded { get; set; } = false;

    public Variable? Self { get; set; }

    private Instruction? Anchor { get; set; }
    public int Position { get; private set; } = -1;
    public int StackOffset { get; set; } = 0;

    public Scope? Scope { get; set; }
    public UnitPhase Phase { get; private set; } = UnitPhase.READ_ONLY_MODE;

    public Unit(FunctionImplementation function)
    {
        Function = function;

        if (function.Metadata?.IsMember ?? false)
        {
            Self = function.GetVariable(global::Function.THIS_POINTER_IDENTIFIER);
        }

        Registers = new List<Register>()
        {
            new Register(Size.QWORD, new string[] { "rax", "eax", "ax", "al" }, RegisterFlag.VOLATILE | RegisterFlag.RETURN | RegisterFlag.DENOMINATOR),
            new Register(Size.QWORD, new string[] { "rbx", "ebx", "bx", "bl" }),
            new Register(Size.QWORD, new string[] { "rcx", "ecx", "cx", "cl" }, RegisterFlag.VOLATILE),
            new Register(Size.QWORD, new string[] { "rdx", "edx", "dx", "dl" }, RegisterFlag.VOLATILE | RegisterFlag.REMAINDER),
            new Register(Size.QWORD, new string[] { "rsi", "esi", "si", "sil" }),
            new Register(Size.QWORD, new string[] { "rdi", "edi", "di", "dil" }),
            new Register(Size.QWORD, new string[] { "rbp", "ebp", "bp", "bpl" }),
            new Register(Size.QWORD, new string[] { "rsp", "esp", "sp", "spl" }, RegisterFlag.RESERVED | RegisterFlag.STACK_POINTER),

            new Register(Size.YWORD, new string[] { "ymm0", "xmm0" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm1", "xmm1" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm2", "xmm2" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm3", "xmm3" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm4", "xmm4" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm5", "xmm5" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm6", "xmm6" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm7", "xmm7" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm8", "xmm8" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm9", "xmm9" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm10", "xmm10" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm11", "xmm11" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm12", "xmm12" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm13", "xmm13" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm14", "xmm14" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED),
            new Register(Size.YWORD, new string[] { "ymm15", "xmm15" }, RegisterFlag.MEDIA | RegisterFlag.VOLATILE | RegisterFlag.RESERVED)
        };

        if (Assembler.Size == Size.QWORD)
        {
            Registers.AddRange(new List<Register>()
            {
                new Register(Size.QWORD, new string[] { "r8", "r8d", "r8w", "r8b" }, RegisterFlag.VOLATILE),
                new Register(Size.QWORD, new string[] { "r9", "r9d", "r9w", "r9b" }, RegisterFlag.VOLATILE),
                new Register(Size.QWORD, new string[] { "r10", "r10d", "r10w", "r10b" }, RegisterFlag.VOLATILE),
                new Register(Size.QWORD, new string[] { "r11", "r11d", "r11w", "r11b" }, RegisterFlag.VOLATILE),
                new Register(Size.QWORD, new string[] { "r12", "r12d", "r12w", "r12b" }),
                new Register(Size.QWORD, new string[] { "r13", "r13d", "r13w", "r13b" }),
                new Register(Size.QWORD, new string[] { "r14", "r14d", "r14w", "r14b" }),
                new Register(Size.QWORD, new string[] { "r15", "r15d", "r15w", "r15b" })
            });
        }

        NonVolatileRegisters = Registers.FindAll(r => !r.IsVolatile && !r.IsReserved);
        VolatileRegisters = Registers.FindAll(r => r.IsVolatile);

        NonReservedRegisters = VolatileRegisters.FindAll(r => !r.IsReserved);
        NonReservedRegisters.AddRange(NonVolatileRegisters.FindAll(r => !r.IsReserved));

        MediaRegisters = Registers.FindAll(r => Flag.Has(r.Flags, RegisterFlag.MEDIA));
    }

    public void ExpectMode(UnitPhase expected)
    {
        if (Phase != expected)
        {
            throw new InvalidOperationException("Unit mode didn't match the expected");
        }
    }

    public int GetCurrentVariableVersion(Variable variable)
    {
        return Math.Max(GetVariableHandles(variable).FindLastIndex(h => h.IsValid(Position)), 0);
    }

    /// <summary>
    /// Returns all variables currently inside registers that are needed at the given instruction position or later
    /// </summary>
    public List<VariableState> GetState(int at)
    {
        return Registers
            .FindAll(register => !register.IsAvailable(at) && (register.Handle?.Metadata.IsVariable ?? false))
            .Select(register => new VariableState(register.Handle!.Metadata.Variables.First().Variable, register)).ToList();
    }

    public void Set(List<VariableState> state)
    {
        // Reset all registers
        Registers.ForEach(r => r.Reset());

        // Restore all the variables to their own registers
        state.ForEach(s => s.Restore(this));
    }

    public void Append(Instruction instruction, bool after = false)
    {
        if (Position < 0)
        {
            Instructions.Add(instruction);
            instruction.Position = Instructions.Count - 1;
        }
        else
        {
            // Find the last instruction with the current position since there can be many
            var position = Instructions.IndexOf(Anchor!);

            if (position == -1)
            {
                position = Position;
            }

            if (after)
            {
                position++;
            }

            Instructions.Insert(position, instruction);
            instruction.Position = Position;

            IsReindexingNeeded = true;
        }

        instruction.Scope = Scope;
        instruction.Result.Lifetime.Start = instruction.Position;

        if (Phase == UnitPhase.BUILD_MODE)
        {
            var previous = Anchor;

            Anchor = instruction;
            instruction.TryBuild();
            Anchor = previous;
        }
    }

    public void Write(string instruction)
    {
        ExpectMode(UnitPhase.BUILD_MODE);

        Builder.Append(instruction);
        Builder.AppendLine();
    }

    public RegisterHandle? TryGetCached(Result handle, bool write)
    {
        var register = Registers.Find(r => (r.Handle != null) ? r.Handle.Value == handle.Value : false);

        if (register != null)
        {
            return new RegisterHandle(register);
        }

        return null;
    }

    public void Release(Register register)
    {
        var value = register.Handle;

        if (value == null || !value.IsReleasable())
        {
            return;
        }

        // Get all the variables that this value represents
        var attributes = value.Metadata.Variables;

        foreach (var attribute in attributes)
        {
            var destination = new Result(References.CreateVariableHandle(this, null, attribute.Variable));

            var move = new MoveInstruction(this, destination, value);
            //move.Mode = attribute.Variable.GetInstructionParameterSize();
            move.Type = MoveType.RELOCATE;

            Append(move);
        }

        // Now the register is ready for use
        register.Reset();
    }

    public Label GetNextLabel()
    {
        return new Label(Function.Metadata!.GetFullname() + $"_L{LabelIndex++}");
    }

    public string GetNextString()
    {
        return Function.Metadata!.GetFullname() + $"_S{StringIndex++}";
    }

    #region Registers

    public Register? GetNextNonVolatileRegister(int start, int end)
    {
        var register = NonVolatileRegisters
            .Find(r => (r.Handle == null || !r.Handle.Lifetime.IsIntersecting(start, end)) && !r.IsReserved);

        return register;
    }

    public Register? GetNextNonVolatileRegister(bool release = true)
    {
        var register = NonVolatileRegisters
            .Find(r => r.IsAvailable(Position) && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);

        if (register != null || !release)
        {
            return register;
        }

        register = NonVolatileRegisters.Find(r => r.IsReleasable && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);

        if (register != null)
        {
            Release(register);
            return register;
        }

        return null;
    }

    public Register? GetNextRegisterWithoutReleasing()
    {
        var register = VolatileRegisters.Find(r => r.IsAvailable(Position) && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);

        if (register != null)
        {
            return register;
        }

        return NonVolatileRegisters.Find(r => r.IsAvailable(Position) && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);
    }

    public Register GetNextRegister()
    {
        var register = VolatileRegisters.Find(r => r.IsAvailable(Position) && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);

        if (register != null)
        {
            return register;
        }

        register = NonVolatileRegisters.Find(r => r.IsAvailable(Position) && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);

        if (register != null)
        {
            return register;
        }

        register = VolatileRegisters.Find(r => r.IsReleasable && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);

        if (register != null)
        {
            Release(register);
            return register;
        }

        register = NonVolatileRegisters.Find(r => r.IsReleasable && !(Function.Returns && r.IsReturnRegister) && !r.IsReserved);

        if (register != null)
        {
            Release(register);
            return register;
        }

        throw new NotImplementedException("Couldn't find available register");
    }

    public Register GetBasePointer()
    {
        return Registers.Find(r => Flag.Has(r.Flags, RegisterFlag.BASE_POINTER)) ?? throw new Exception("Architecture didn't have base pointer register?");
    }

    public Register GetStackPointer()
    {
        return Registers.Find(r => Flag.Has(r.Flags, RegisterFlag.STACK_POINTER)) ?? throw new Exception("Architecture didn't have stack pointer register?");
    }

    public Register GetStandardReturnRegister()
    {
        return Registers.Find(r => Flag.Has(r.Flags, RegisterFlag.RETURN)) ?? throw new Exception("Architecture didn't have return register?");
    }

    public void Reset()
    {
        Registers.ForEach(r => r.Reset(true));
    }

    #endregion

    #region Interaction

    public void Execute(UnitPhase mode, Action action)
    {
        Phase = mode;
        Position = -1;

        try
        {
            action();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERROR: Unit simulation failed: {e}");
        }

        if (IsReindexingNeeded)
        {
            Reindex();
        }

        Phase = UnitPhase.READ_ONLY_MODE;
    }

    public void Simulate(UnitPhase mode, Action<Instruction> action)
    {
        Phase = mode;
        Position = 0;
        StackOffset = 0;
        Scope = null;

        Reset();

        var instructions = new List<Instruction>(Instructions);

        foreach (var instruction in instructions)
        {
            try
            {
                if (instruction.Scope == null)
                {
                    throw new ApplicationException("Instruction was missing its scope");
                }

                Anchor = instruction;

                if (Scope != instruction.Scope)
                {
                    var back = false;

                    // Detect if the program is exiting the current scope
                    if (Scope?.Outer == instruction.Scope)
                    {
                        Scope?.Exit();
                        back = true;
                    }

                    // Scope enter function is designed for entering not for falling back
                    if (back)
                    {
                        Scope = instruction.Scope;
                    }
                    else
                    {
                        instruction.Scope.Enter(this);
                    }
                }

                action(instruction);

                // Simulate the stack size change
                StackOffset += instruction.GetStackOffsetChange();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"ERROR: Unit simulation failed: {e}");
                break;
            }

            Position++;
        }

        if (IsReindexingNeeded)
        {
            Reindex();
        }

        // Reset the state after this simulation
        Phase = UnitPhase.READ_ONLY_MODE;
    }

    #endregion

    public void Cache(Variable variable, Result result, bool invalidate)
    {
        // Get all cached versions of this variable
        var handles = GetVariableHandles(variable);
        var position = handles.FindIndex(0, handles.Count, h => h.Lifetime.Start >= Position);

        result.Lifetime.Start = Position;

        if (position == -1)
        {
            // Crop the lifetime of the previous handle
            var index = handles.Count - 1;

            if (index != -1)
            {
                handles[index].Lifetime.End = Position;
            }

            handles.Add(result);
        }
        else
        {
            // Crop the lifetime of the previous handle
            var index = position - 1;

            if (index != -1)
            {
                handles[index].Lifetime.End = Position;
            }

            result.Lifetime.End = handles[position].Lifetime.Start;

            handles.Insert(position, result);
        }
    }

    public void Cache(object constant, Result value)
    {
        var handles = GetConstantHandles(constant);
        value.Lifetime.Start = Position;

        handles.Add(value);
    }

    public List<Result> GetVariableHandles(Variable variable)
    {
        return Scope?.GetVariableHandles(this, variable) ?? throw new ApplicationException("Couldn't get variable reference list");
    }

    public List<Result> GetConstantHandles(object constant)
    {
        return Scope?.GetConstantHandles(constant) ?? throw new ApplicationException("Couldn't get constant reference list");
    }

    public Result? GetCurrentVariableHandle(Variable variable)
    {
        var handles = GetVariableHandles(variable).FindAll(h => h.IsValid(Position));
        return handles.Count == 0 ? null : handles.Last();
    }

    public Result? GetCurrentConstantHandle(object constant)
    {
        var handles = GetConstantHandles(constant).FindAll(h => h.IsValid(Position));
        return handles.Count == 0 ? null : handles.Last();
    }

    public string Export()
    {
        return Builder.ToString();
    }

    private void Reindex()
    {
        Position = 0;

        foreach (var instruction in Instructions)
        {
            instruction.Position = Position++;
        }

        IsReindexingNeeded = false;
    }
}