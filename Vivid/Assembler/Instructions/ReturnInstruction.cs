using System.Collections.Generic;
using System.Text;
using System.Linq;

/// <summary>
/// Returns the specified value to the caller by exiting the current function properly
/// This instruction works on all architectures
/// </summary>
public class ReturnInstruction : Instruction
{
	private const string SHARED_RETURN_INSTRUCTION = "ret";

	public const string X64_LOAD_REGISTER_INSTRUCTION = "pop";

	public const string ARM64_LOAD_REGISTER_PAIR_INSTRUCTION = "ldp";
	public const string ARM64_LOAD_REGISTER_INSTRUCTION = "ldr";


	public Register ReturnRegister => ReturnType == Types.DECIMAL ? Unit.GetDecimalReturnRegister() : Unit.GetStandardReturnRegister();
	private Handle ReturnRegisterHandle => new RegisterHandle(ReturnRegister);

	public Result? Object { get; private set; }
	public Type? ReturnType { get; private set; }

	public int StackMemoryChange { get; private set; }

	public ReturnInstruction(Unit unit, Result? value, Type? return_type) : base(unit)
	{
		Object = value;
		ReturnType = return_type;
		Result.Format = ReturnType?.GetRegisterFormat() ?? Assembler.Format;
	}

	/// <summary>
	/// Returns whether the return value is in the wanted return register
	/// </summary>
	private bool IsValueInReturnRegister()
	{
		return Object!.Value.Is(HandleType.REGISTER) && Object!.Value.To<RegisterHandle>().Register == ReturnRegister;
	}

	public override void OnBuild()
	{
		// Ensure that if there's a value to return it's in a return register
		if (Object == null || IsValueInReturnRegister())
		{
			return;
		}

		Unit.Append(new MoveInstruction(Unit, new Result(ReturnRegisterHandle, ReturnType!.GetRegisterFormat()), Object)
		{
			Type = MoveType.RELOCATE
		});
	}

	private void RestoreRegistersArm64(StringBuilder builder, List<Register> registers)
	{
		// Example:
		// stp x0, x1, [sp, #-64]!
		// stp x2, x3, [sp, #16]
		// stp x4, x5, [sp, #32]
		// str x6, [sp, #48]

		// ldr x6, [sp, #48]
		// ldp x4, x5, [sp, #32]
		// ldp x2, x3, [sp, #16]
		// ldp x0, x1, [sp], #64

		if (!registers.Any())
		{
			return;
		}

		var bytes = (registers.Count + 1) / 2 * 2 * Assembler.Size.Bytes;
		var stack_pointer = Unit.GetStackPointer();
		
		Unit.StackOffset -= bytes;

		if (registers.Count == 1)
		{
			builder.AppendLine($"{ARM64_LOAD_REGISTER_INSTRUCTION} {registers.First()}, [{stack_pointer}], #{bytes}");
			return;
		}

		var standard_registers = registers.Where(i => !i.IsMediaRegister).ToList();
		var media_registers = registers.Where(i => i.IsMediaRegister).ToList();

		var position = registers.Count;

		if (media_registers.Count % 2 != 0 && media_registers.Any())
		{
			position--;

			if (!standard_registers.Any() && media_registers.Count == 1)
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_INSTRUCTION} {media_registers.First()}, [{stack_pointer}], #{bytes}");
			}
			else
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_INSTRUCTION} {media_registers.First()}, [{stack_pointer}, #{position * Assembler.Size.Bytes}]");
			}

			media_registers.Pop();
		}

		for (var i = 0; i < media_registers.Count;)
		{
			var batch = media_registers.Skip(i).Take(2).ToArray();

			position -= 2;

			if (!standard_registers.Any() && i + 2 == media_registers.Count)
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_PAIR_INSTRUCTION} {batch[0]}, {batch[1]}, [{stack_pointer}], #{bytes}");
			}
			else
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_PAIR_INSTRUCTION} {batch[0]}, {batch[1]}, [{stack_pointer}, #{position * Assembler.Size.Bytes}]");
			}

			i += 2;
		}

		if (standard_registers.Count % 2 != 0 && standard_registers.Any())
		{
			position--;

			if (standard_registers.Count == 1)
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_INSTRUCTION} {standard_registers.First()}, [{stack_pointer}], #{bytes}");
			}
			else
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_INSTRUCTION} {standard_registers.First()}, [{stack_pointer}, #{position * Assembler.Size.Bytes}]");
			}

			standard_registers.Pop();
		}

		for (var i = 0; i < standard_registers.Count;)
		{
			var batch = standard_registers.Skip(i).Take(2).ToArray();

			position -= 2;

			if (i + 2 == standard_registers.Count)
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_PAIR_INSTRUCTION} {batch[1]}, {batch[0]}, [{stack_pointer}], #{bytes}");
			}
			else
			{
				builder.AppendLine($"{ARM64_LOAD_REGISTER_PAIR_INSTRUCTION} {batch[1]}, {batch[0]}, [{stack_pointer}, #{position * Assembler.Size.Bytes}]");
			}

			i += 2;
		}
	}

	private void RestoreRegistersX64(StringBuilder builder, List<Register> registers)
	{
		// Save all used non-volatile rgisters
		foreach (var register in registers)
		{
			builder.AppendLine($"{X64_LOAD_REGISTER_INSTRUCTION} {register}");
			Unit.StackOffset += Assembler.Size.Bytes;
		}
	}

	public void Build(List<Register> recover_registers, int local_variables_top)
	{
		var builder = new StringBuilder();
		var start = Unit.StackOffset;
		var allocated_local_memory = start - local_variables_top;

		if (allocated_local_memory > 0)
		{
			var stack_pointer = Unit.GetStackPointer();

			if (Assembler.IsX64)
			{
				builder.AppendLine($"{AdditionInstruction.SHARED_STANDARD_ADDITION_INSTRUCTION} {stack_pointer}, {allocated_local_memory}");
			}
			else
			{
				builder.AppendLine($"{AdditionInstruction.SHARED_STANDARD_ADDITION_INSTRUCTION} {stack_pointer}, {stack_pointer}, #{allocated_local_memory}");
			}

			Unit.StackOffset -= allocated_local_memory;
		}

		if (Assembler.IsDebuggingEnabled)
		{
			builder.AppendLine(".cfi_def_cfa 7, 8");
		}

		// Restore all used non-volatile rgisters
		if (Assembler.IsX64)
		{
			RestoreRegistersX64(builder, recover_registers);
		}
		else
		{
			RestoreRegistersArm64(builder, recover_registers);
		}

		builder.Append(SHARED_RETURN_INSTRUCTION);

		StackMemoryChange = Unit.StackOffset - start;

		Build(builder.ToString());
	}

	public override int GetStackOffsetChange()
	{
		return StackMemoryChange;
	}

	public override Result? GetDestinationDependency()
	{
		return Object;
	}

	public override InstructionType GetInstructionType()
	{
		return InstructionType.RETURN;
	}

	public override Result[] GetResultReferences()
	{
		return Object != null ? new[] { Result, Object } : new[] { Result };
	}
}