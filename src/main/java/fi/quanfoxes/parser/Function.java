package fi.quanfoxes.parser;

import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;
import java.util.stream.Stream;

import fi.quanfoxes.AccessModifier;
import fi.quanfoxes.lexer.Flag;
import fi.quanfoxes.parser.nodes.VariableNode;

public class Function extends Context {
    public static final String IDENTIFIER = "function_%s";
    public static final String INDEXED_IDENTIFIER = "function_%s_%d";

    private int modifiers;
    private int index = -1;

    private Map<String, Variable> parameters = new HashMap<>();

    private Type result;

    private List<Node> usages = new ArrayList<>();

    public Function(Context context, String name, int modifiers, Type result) throws Exception {
        this.name = name;
        this.modifiers = modifiers;
        this.result = result;

        super.link(context);
        context.declare(this);
    }

    public Function(Context context, int modifiers) {
        this.modifiers = modifiers;
        super.link(context);
    }

    @Override
    public String getIdentifier() {
        int index = getIndex();

        if (index != -1) {
            return String.format(INDEXED_IDENTIFIER, name, index);
        }
        else {
            return String.format(IDENTIFIER, name);
        }
    }

    @Override
    public boolean isLocalVariableDeclared(String name) {
        return parameters.containsKey(name) || super.isLocalVariableDeclared(name);
    }

    @Override
    public boolean isVariableDeclared(String name) {
        return parameters.containsKey(name) || super.isVariableDeclared(name);
    }

    @Override
    public Variable getVariable(String name)  {
        if (parameters.containsKey(name)) {
            return parameters.get(name);
        }
        
        return super.getVariable(name);
    }

    @Override
    public Collection<Variable> getVariables() {
        return Stream.concat(super.getVariables().stream(), parameters.values().stream()).collect(Collectors.toList());
    }

    /**
     * Returns whether this functions is a static function
     * @return True if this function is a static functions, otherwise false
     */
    public boolean isStatic() {
        return Flag.has(modifiers, AccessModifier.STATIC);
    }

    /**
     * Returns whether this functions is a global function
     * @return True if this function is a global functions, otherwise false
     */
    public boolean isGlobal() {
        return getTypeParent() == null;
    }

    /**
     * Returns whether this functions is a member function
     * @return True if this function is a member functions, otherwise false
     */
    public boolean isMember() {
        return getTypeParent() != null;
    }

    /**
     * Returns the modifiers associated with this function
     * @return Modifiers associated with this function
     */
    public int getModifiers() {
        return modifiers;
    }

    public Function setParameters(Node node) {
        VariableNode parameter = (VariableNode)node.first();
        
        while (parameter != null) {
            Variable variable = parameter.getVariable();
            variable.setVariableType(VariableType.PARAMETER);

            parameters.put(variable.getName(), variable);

            parameter = (VariableNode)parameter.next();
        }

        return this;
    }

    public Collection<Variable> getLocals() {
        return super.getVariables();
    }

    public Collection<Variable> getParameters() {
        return parameters.values();
    }

    public List<Type> getParameterTypes() {
        return parameters.values().stream().map(Variable::getType).collect(Collectors.toList());
    }

    public int getParameterCount() {
        return parameters.size();
    }

    /**
     * Returns the memory required for the local variables
     * @return Memory required for the local variables
     */
    public int getLocalMemorySize() {
        return getVariables().stream().filter(v -> v.getVariableType() == VariableType.LOCAL).map(Variable::getType).mapToInt(Type::getSize).sum();
    }

    public void setReturnType(Type type) {
        this.result = type;
    }

    public Type getReturnType() {
        return result;
    }

    public void setIndex(int index) {
        this.index = index;
    }

    public int getIndex() {
        return index;
    }

    public boolean hasIndex() {
        return index != -1;
    }

    public void addUsage(Node node) {
        usages.add(node);
    }

    public List<Node> getUsages() {
        return usages;
    }
}