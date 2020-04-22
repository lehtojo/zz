using System;
using System.Collections.Generic;

public enum HandleType
{
    MEMORY,
    CONSTANT,
    REGISTER,
    NONE
}

public class Handle
{
    public HandleType Type { get; private set; }
    public Size Size { get; set; } = Size.NONE;

    public Handle()
    {
        Type = HandleType.NONE;
    }

    public Handle(HandleType type)
    {
        Type = type;
    }

    public virtual void Use(int position) { }

    public override string ToString()
    {
        throw new NotImplementedException("Missing text conversion from handle");
    }
}

public class DataSectionHandle : Handle
{
    public string Identifier { get; private set; }

    public DataSectionHandle(string identifier) : base(HandleType.MEMORY)
    {
        Identifier = identifier;
    }

    public override string ToString()
    {
        if (Size.IsVisible())
        {
            return $"{Size} [{Identifier}]";
        }
        else
        {
            return $"[{Identifier}]";
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is DataSectionHandle handle &&
               Type == handle.Type &&
               Identifier == handle.Identifier;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Identifier);
    }
}

public class ConstantHandle : Handle
{
    public object Value { get; private set; }

    public ConstantHandle(object value) : base(HandleType.CONSTANT)
    {
        Value = value;
    }

    public override string ToString()
    {
        return Value?.ToString() ?? throw new NullReferenceException("Constant value was missing");
    }

    public override bool Equals(object? obj)
    {
        return obj is ConstantHandle handle &&
               EqualityComparer<object>.Default.Equals(Value, handle.Value);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value);
    }
}

public class VariableMemoryHandle : MemoryHandle
{
    public Variable Variable { get; private set; }

    public VariableMemoryHandle(Unit unit, Variable variable) : base(unit, new Result(new RegisterHandle(unit.GetStackPointer())), variable.Alignment)
    {
        Variable = variable;
    }

    public override string ToString() 
    {
        if (Variable.Alignment < 0)
        {
            return $"[?]";
        }

        Offset = Variable.Alignment;

        return base.ToString();
    }
}

public class MemoryHandle : Handle
{
    public Unit Unit { get; private set; }
    public Result Start { get; private set; }
    public int Offset { get; protected set; }
    
    private bool IsStackMemoryPointer => Start.Value is RegisterHandle handle && handle.Register == Unit.GetStackPointer();
    private int CorrectedOffset => (IsStackMemoryPointer ? Unit.StackOffset : 0) + Offset;
    
    public static MemoryHandle FromStack(Unit unit, int offset)
    {
        return new MemoryHandle(unit, new Result(new RegisterHandle(unit.GetStackPointer())), offset);
    }

    public MemoryHandle(Unit unit, Result start, int offset) : base(HandleType.MEMORY)
    {
        Unit = unit;
        Start = start;
        Offset = offset;
    }

    public override void Use(int position)
    {
        Start.Use(position);
    }

    public override string ToString()
    {
        var offset = string.Empty;

        if (Offset > 0)
        {
            offset = $"+{CorrectedOffset}";
        }
        else if (Offset < 0)
        {
            offset = CorrectedOffset.ToString();
        }

        if (Start.Value.Type == HandleType.REGISTER ||
            Start.Value.Type == HandleType.CONSTANT)
        {
            var address = $"[{Start.Value}{offset}]";

            if (Size.IsVisible())
            {
                return $"{Size} {address}";
            }
            else
            {
                return $"{address}";
            }
        }

        throw new ApplicationException("Base of the memory handle was no longer in register");
    }

    public override bool Equals(object? obj)
    {
        return obj is MemoryHandle handle &&
               EqualityComparer<Result>.Default.Equals(Start, handle.Start) &&
               Offset == handle.Offset;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, Offset);
    }
}

public class ComplexMemoryHandle : Handle
{
    public Result Start { get; private set; }
    public Result Offset { get; private set; }
    public int Stride { get; private set; }

    public ComplexMemoryHandle(Result start, Result offset, int stride) : base(HandleType.MEMORY)
    {
        Start = start;
        Offset = offset;
        Stride = stride;
    }

    public override void Use(int position)
    {
        Start.Use(position);
        Offset.Use(position);
    }

    public override string ToString()
    {
        var offset = string.Empty;

        if (Offset.Value.Type == HandleType.REGISTER)
        {
            offset = "+" + Offset.ToString() + (Stride == 1 ? string.Empty : $"*{Stride}");
        }
        else if (Offset.Value is ConstantHandle constant)
        {
            var index = (Int64)constant.Value;
            var value = index * Stride;

            if (value > 0)
            {
                offset = $"+{value}";
            }
            else if (value < 0)
            {
                offset = value.ToString();
            }
        }
        else
        {
            throw new ApplicationException("Complex memory address's offset wasn't a constant or in a register");
        }

        if (Start.Value.Type == HandleType.REGISTER ||
            Start.Value.Type == HandleType.CONSTANT)
        {
            var address = $"[{Start.Value}{offset}]";

            if (Size.IsVisible())
            {
                return $"{Size} {address}";
            }
            else
            {
                return $"{address}";
            }
        }

        throw new ApplicationException("Base of the memory handle was no longer in register");
    }

    public override bool Equals(object? obj)
    {
        return obj is ComplexMemoryHandle handle &&
               EqualityComparer<Result>.Default.Equals(Start, handle.Start) &&
               EqualityComparer<Result>.Default.Equals(Offset, handle.Offset) &&
               Stride == handle.Stride;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Start, Offset, Stride);
    }
}

public class RegisterHandle : Handle
{
    public Register Register { get; private set; }

    public RegisterHandle(Register register) : base(HandleType.REGISTER)
    {
        Register = register;
    }

    public override string ToString()
    {
        if (Size == Size.NONE)
        {
            return Register[Assembler.Size];
        }

        return Register[Size];
    }

    public override bool Equals(object? obj)
    {
        return obj is RegisterHandle handle &&
               EqualityComparer<Register>.Default.Equals(Register, handle.Register);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Register);
    }
}