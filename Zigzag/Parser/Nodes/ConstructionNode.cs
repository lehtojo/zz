public class ConstructionNode : Node, IResolvable, IType
{
	public Node Parameters => Last;
	public Type Type => GetConstructor()?.GetTypeParent();

	public ConstructionNode(Node constructor)
	{
		Add(constructor);
	}

	public FunctionImplementation GetConstructor()
	{
		if (First.GetNodeType() == NodeType.FUNCTION_NODE)
		{
			var constructor = (FunctionNode)First;
			return constructor.Function;
		}

		return null;
	}

	public Type GetConstructionType()
	{
		var constructor = GetConstructor();

		if (constructor != null)
		{
			return constructor.GetTypeParent();
		}

		return null;
	}

	public Node Resolve(Context context)
	{
		if (First is IResolvable resolvable)
		{
			var resolved = resolvable.Resolve(context);
			First.Replace(resolved);
		}

		return null;
	}

	public Type GetType()
	{
		return GetConstructionType();
	}

	public override NodeType GetNodeType()
	{
		return NodeType.CONSTRUCTION_NODE;
	}

	public Status GetStatus()
	{
		return Status.OK;
	}
}