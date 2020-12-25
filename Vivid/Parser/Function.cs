using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class Parameter
{
	public string Name { get; set; }
	public Position? Position { get; set; }
	public Type? Type { get; set; }

	public Parameter(string name, Type? type = Types.UNKNOWN)
	{
		Name = name;
		Type = type;
	}

	public Parameter(string name, Position? position, Type? type)
	{
		Name = name;
		Position = position;
		Type = type;
	}

	public override string ToString()
	{
		if (Type == null)
		{
			return $"{Name}: _";
		}

		return $"{Name}: {(Type.IsUnresolved ? "?" : Type.ToString())}";
	}
}

public class Function : Context
{
	public const string SELF_POINTER_IDENTIFIER = "this";

	public int Modifiers { get; set; }

	public Variable? Self { get; protected set; }
	public List<Parameter> Parameters { get; } = new List<Parameter>();
	public List<Token> Blueprint { get; private set; }
	public Position? Position { get; set; }

	public List<FunctionImplementation> Implementations { get; } = new List<FunctionImplementation>();

	public bool IsConstructor => this is Constructor;
	public bool IsImported => Flag.Has(Modifiers, AccessModifier.EXTERNAL);
	public bool IsExported => Flag.Has(Modifiers, AccessModifier.GLOBAL);
	public bool IsOutlined => Flag.Has(Modifiers, AccessModifier.OUTLINE);
	public bool IsResponsible => Flag.Has(Modifiers, AccessModifier.RESPONSIBLE);

	/// <summary>
	/// Creates a unimplemented function
	/// </summary>
	/// <param name="context">Context to link into</param>
	/// <param name="modifiers">Function access modifiers</param>
	/// <param name="name">Function name</param>
	/// <param name="blueprint">Function blueprint is used to create implementations of this function</param>
	public Function(Context context, int modifiers, string name, List<Token> blueprint)
	{
		Parent = context;
		Name = name;
		Prefix = Name.Length.ToString(CultureInfo.InvariantCulture);
		Modifiers = modifiers;
		Blueprint = blueprint;
	}

	/// <summary>
	/// Creates a unimplemented function
	/// </summary>
	/// <param name="context">Context to link into</param>
	/// <param name="modifiers">Function access modifiers</param>
	/// <param name="name">Function name</param>
	public Function(Context context, int modifiers, string name)
	{
		Parent = context;
		Name = name;
		Prefix = Name.Length.ToString(CultureInfo.InvariantCulture);
		Modifiers = modifiers;
		Blueprint = new List<Token>();
	}

	/// <summary>
	/// Creates a function with default implementation using the parameters and the return type
	/// </summary>
	/// <param name="modifiers">Function access modifiers</param>
	/// <param name="name">Function name</param>
	/// <param name="result">Function return type</param>
	/// <param name="parameters">Function parameters</param>
	public Function(int modifiers, string name, Type? result, params Parameter[] parameters)
	{
		Modifiers = modifiers;
		Name = name;
		Prefix = Name.Length.ToString(CultureInfo.InvariantCulture);
		Parameters = parameters.ToList();
		Blueprint = new List<Token>();

		var implementation = new FunctionImplementation(this, parameters.ToList(), result);
		Implementations.Add(implementation);

		implementation.Implement(Blueprint);
	}

	/// <summary>
	/// Declares a self pointer inside this function
	/// </summary>
	public void DeclareSelfPointer()
	{
		var type = IsConstructor ? VariableCategory.LOCAL : VariableCategory.PARAMETER;

		Self = new Variable(
			this,
			GetTypeParent(),
			type,
			Function.SELF_POINTER_IDENTIFIER,
			AccessModifier.PUBLIC

		) { IsSelfPointer = true, Position = Position };
	}

	/// <summary>
	/// Implements the function with parameter types
	/// </summary>
	/// <param name="types">Parameter types</param>
	/// <returns>Function implementation</returns>
	public virtual FunctionImplementation Implement(IEnumerable<Type> types)
	{
		// Pack parameters with names and types
		var parameters = Parameters.Zip(types, (a, b) => new Parameter(a.Name, a.Position, b)).ToList();

		// Create a function implementation
		var implementation = new FunctionImplementation(this, parameters, null, Parent);

		// Constructors must be set to return a link to the created object manually
		if (IsConstructor)
		{
			implementation.ReturnType = GetTypeParent() ?? throw new ApplicationException("Constructor missing type parent");
		}

		// Add the created implementation to the list
		Implementations.Add(implementation);

		implementation.Implement(Blueprint.Select(i => (Token)i.Clone()).ToList());

		return implementation;
	}

	/// </summary>
	/// Returns whether there are enough parameters to call this function
	/// </summary>
	public virtual bool Passes(List<Type> parameters)
	{
		if (parameters.Count != Parameters.Count)
		{
			return false;
		}

		for (var i = 0; i < Parameters.Count; i++)
		{
			if (Parameters[i].Type == null)
			{
				continue;
			}

			if (Resolver.GetSharedType(Parameters[i].Type, parameters[i]) == null)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// Tries to find function implementation with the given parameter
	/// </summary>
	/// <param name="parameter">Parameter type used in filtering</param>
	public FunctionImplementation? Get(Type parameter)
	{
		return Get(new List<Type> { parameter });
	}

	/// <summary>
	/// Tries to find function implementation with the given parameters
	/// </summary>
	/// <param name="parameter">Parameter types used in filtering</param>
	public virtual FunctionImplementation? Get(List<Type> parameters)
	{
		// Implementation should not be made if any of the parameters has a fixed type but it's unresolved
		if (Parameters.Any(i => i.Type != null && i.Type.IsUnresolved))
		{
			return null;
		}

		var types = Parameters.Zip(parameters).Select(i => i.First.Type ?? i.Second).ToList();
		var implementation = Implementations.Find(f => f.ParameterTypes.SequenceEqual(types));

		if (implementation != null || IsImported)
		{
			return implementation;
		}

		return Parameters.Count != parameters.Count ? null : Implement(types);
	}

	public override Variable? GetSelfPointer()
	{
		return Self;
	}

	protected override void OnMangle(Mangle mangle)
	{
		mangle += Name.Length.ToString(CultureInfo.InvariantCulture) + Name;
	}

	public override string ToString()
	{
		return (IsImported ? "import" : string.Empty) + Name + $"({string.Join(", ", Parameters)})";
	}

	public override bool Equals(object? other)
	{
		return other is Function function &&
			   EqualityComparer<List<Context>>.Default.Equals(Subcontexts, function.Subcontexts) &&
			   EqualityComparer<Dictionary<string, Variable>>.Default.Equals(Variables, function.Variables) &&
			   EqualityComparer<Dictionary<string, FunctionList>>.Default.Equals(Functions, function.Functions) &&
			   EqualityComparer<Dictionary<string, Type>>.Default.Equals(Types, function.Types) &&
			   EqualityComparer<Dictionary<string, Label>>.Default.Equals(Labels, function.Labels) &&
			   Modifiers == function.Modifiers &&
			   EqualityComparer<List<Parameter>>.Default.Equals(Parameters, function.Parameters) &&
			   EqualityComparer<List<FunctionImplementation>>.Default.Equals(Implementations, function.Implementations);
	}

	public override int GetHashCode()
	{
		HashCode hash = new HashCode();
		hash.Add(Subcontexts);
		hash.Add(Variables);
		hash.Add(Functions);
		hash.Add(Types);
		hash.Add(Labels);
		hash.Add(Modifiers);
		hash.Add(Parameters);
		hash.Add(Implementations);
		return hash.ToHashCode();
	}
}