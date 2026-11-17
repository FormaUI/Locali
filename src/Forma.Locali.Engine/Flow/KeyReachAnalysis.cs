namespace Forma.Locali.Flow;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

/// <summary>Reads, through a method's control flow graph, what the method does with the text one call binds to the parameter a target in its body takes as its key.</summary>
/// <remarks>
/// The call's constant arguments are carried through the graph: a condition they settle is followed the one way it goes, and a
/// condition they leave open is followed both ways. A path to the target that an open condition on the key can steer away from, a
/// write of the key before the target, or a target in a handler, a lambda or a local function, leaves the call unknown; an open
/// condition on anything else leaves the target as reachable as a lookup written under it.
/// </remarks>
internal sealed class KeyReachAnalysis
{
    private const string IsNullOrEmptyMethod = "IsNullOrEmpty";
    private const string IsNullOrWhiteSpaceMethod = "IsNullOrWhiteSpace";
    private const string ContainsMethod = "Contains";
    private const string StartsWithMethod = "StartsWith";
    private const string EndsWithMethod = "EndsWith";
    private const string LengthProperty = "Length";
    private const string EmptyField = "Empty";

    private readonly ControlFlowGraph _graph;
    private readonly IParameterSymbol _key;
    private readonly HashSet<ISymbol> _volatile;

    private KeyReachAnalysis(ControlFlowGraph graph, IParameterSymbol key, HashSet<ISymbol> volatileSymbols)
    {
        _graph = graph;
        _key = key;
        _volatile = volatileSymbols;
    }

    /// <summary>What a method does with the key one call writes, on the way to a target in its body.</summary>
    /// <param name="graph">The control flow graph of the body holding the target.</param>
    /// <param name="target">The lookup, or the call passing the key on, from the body's own operation tree.</param>
    /// <param name="key">The parameter of the method the target takes as its key.</param>
    /// <param name="arguments">What the call passes each parameter, by ordinal; a parameter it passes nothing known reads as unknown.</param>
    /// <param name="isKeyShared">Whether the key was already handed to state or code the flow does not follow before the call.</param>
    public static KeyReachResult Of(ControlFlowGraph graph, IOperation target, IParameterSymbol key, IReadOnlyDictionary<int, CallValue> arguments, bool isKeyShared)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(arguments);

        if (key.RefKind != RefKind.None
            || key.ContainingSymbol is not IMethodSymbol method
            || Locate(graph, target) is not var (block, index, copy)
            || IsInHandler(block.EnclosingRegion))
        {
            return KeyReachResult.Unknown;
        }

        var volatileSymbols = Volatile(Root(target));
        if (volatileSymbols.Contains(key))
        {
            return KeyReachResult.Unknown;
        }

        var entry = new PathState { IsKeyShared = isKeyShared };
        foreach (var parameter in method.Parameters)
        {
            var isKey = SymbolEqualityComparer.Default.Equals(parameter, key);
            var value = arguments.TryGetValue(parameter.Ordinal, out var bound) ? bound : CallValue.Unknown(isKey);
            entry.Symbols[parameter] = value with { DependsOnKey = value.DependsOnKey || isKey };
        }

        return new KeyReachAnalysis(graph, key, volatileSymbols).Walk(entry, block, index, copy);
    }

    private KeyReachResult Walk(PathState entry, BasicBlock target, int targetIndex, IOperation targetCopy)
    {
        var blocks = _graph.Blocks;
        var states = new PathState?[blocks.Length];
        var successors = new List<int>[blocks.Length];
        var choices = new List<Choice>[blocks.Length];
        var queued = new bool[blocks.Length];
        var pending = new Queue<int>();
        PathState? atTarget = null;
        var isTargetThrowDependent = false;

        void Merge(int ordinal, PathState incoming)
        {
            if (states[ordinal] is not { } existing)
            {
                states[ordinal] = incoming;
            }
            else if (!existing.Absorb(incoming))
            {
                return;
            }

            if (!queued[ordinal])
            {
                queued[ordinal] = true;
                pending.Enqueue(ordinal);
            }
        }

        int Follow(ControlFlowBranch? branch, PathState state)
        {
            if (branch?.Destination is not { } destination)
            {
                return -1;
            }

            var next = state.Copy();
            foreach (var region in branch.FinallyRegions)
            {
                Havoc(next, Operations(region), state);
            }

            Merge(destination.Ordinal, next);

            return destination.Ordinal;
        }

        Merge(0, entry);
        while (pending.TryDequeue(out var ordinal))
        {
            queued[ordinal] = false;
            var block = blocks[ordinal];
            var entered = states[ordinal]!;
            var state = entered.Copy();
            var isTarget = ordinal == target.Ordinal;

            for (var position = 0; position < block.Operations.Length; position++)
            {
                if (isTarget && position == targetIndex)
                {
                    atTarget = state.Copy();
                }

                Apply(block.Operations[position], state);
            }

            if (isTarget && targetIndex == block.Operations.Length)
            {
                atTarget = state.Copy();
            }

            var normal = new List<int>();
            var blockChoices = new List<Choice>();
            if (block.BranchValue is { } branchValue && block.ConditionalSuccessor is { } conditional)
            {
                var decision = WritesAnything(branchValue) ? CallValue.Unknown(true) : Evaluate(branchValue, state);
                Apply(branchValue, state);
                if (decision is { IsConstant: true, Constant: bool value })
                {
                    normal.Add(Follow((block.ConditionKind == ControlFlowConditionKind.WhenTrue) == value ? conditional : block.FallThroughSuccessor, state));
                }
                else
                {
                    normal.Add(Follow(conditional, state));
                    normal.Add(Follow(block.FallThroughSuccessor, state));
                    blockChoices.Add(new Choice([.. normal], decision.DependsOnKey));
                }
            }
            else
            {
                if (block.BranchValue is { } value)
                {
                    Apply(value, state);
                }

                normal.Add(Follow(block.FallThroughSuccessor, state));
            }

            // Any operation of a block in a try may throw into the handlers around it, through the finally regions between.
            var handlers = Handlers(block);
            if (handlers.Count > 0)
            {
                IOperation[] operations = [.. block.Operations, .. block.BranchValue is { } last ? [last] : Array.Empty<IOperation>()];
                var thrown = entered.Copy();
                thrown.Absorb(state);
                Havoc(thrown, operations, null);
                foreach (var (handler, finallies) in handlers)
                {
                    var caught = thrown.Copy();
                    foreach (var region in finallies)
                    {
                        Havoc(caught, Operations(region), thrown);
                    }

                    Merge(handler, caught);
                }

                blockChoices.Add(new Choice([.. normal, .. handlers.Select(handler => handler.Ordinal)], ReadsKey(operations, entered) || ReadsKey(operations, state)));
                if (isTarget)
                {
                    isTargetThrowDependent = ReadsKey(block.Operations.Take(targetIndex), entered);
                }
            }

            successors[ordinal] = [.. normal.Where(next => next >= 0), .. handlers.Select(handler => handler.Ordinal)];
            choices[ordinal] = blockChoices;
        }

        if (atTarget is null)
        {
            return KeyReachResult.Skips;
        }

        // The blocks a path to the target runs through; an open condition on the key among them can steer the call away from it.
        var reaching = new HashSet<int> { target.Ordinal };
        for (var grew = true; grew;)
        {
            grew = false;
            for (var ordinal = 0; ordinal < blocks.Length; ordinal++)
            {
                if (states[ordinal] is not null && !reaching.Contains(ordinal) && successors[ordinal].Any(reaching.Contains))
                {
                    grew = reaching.Add(ordinal);
                }
            }
        }

        foreach (var ordinal in reaching)
        {
            if (ordinal == target.Ordinal)
            {
                if (isTargetThrowDependent && Handlers(target).Any(handler => !reaching.Contains(handler.Ordinal)))
                {
                    return KeyReachResult.Unknown;
                }

                continue;
            }

            if (choices[ordinal].Any(choice => choice.DependsOnKey && choice.Destinations.Any(next => next < 0 || !reaching.Contains(next))))
            {
                return KeyReachResult.Unknown;
            }
        }

        var holding = targetIndex < target.Operations.Length ? target.Operations[targetIndex] : target.BranchValue!;
        if (atTarget.IsKeyWritten || WrittenSymbols([holding]).Contains(_key, SymbolEqualityComparer.Default))
        {
            return KeyReachResult.Unknown;
        }

        var arguments = new Dictionary<int, CallValue>();
        if (targetCopy is IInvocationOperation invocation)
        {
            foreach (var argument in invocation.Arguments)
            {
                if (argument.Parameter is { } parameter)
                {
                    arguments[parameter.Ordinal] = Evaluate(argument.Value, atTarget);
                }
            }
        }

        return new KeyReachResult(KeyReach.Reads, arguments, atTarget.IsKeyShared);
    }

    private void Apply(IOperation operation, PathState state)
    {
        switch (operation)
        {
            case IExpressionStatementOperation statement:
                Apply(statement.Operation, state);
                break;
            case IFlowCaptureOperation capture:
                {
                    var value = Evaluate(capture.Value, state);
                    Effects(capture.Value, state);
                    state.Captures[capture.Id] = value;
                    break;
                }

            case ISimpleAssignmentOperation { IsRef: false, Target: ILocalReferenceOperation or IParameterReferenceOperation } assignment:
                {
                    var value = Evaluate(assignment.Value, state);
                    Effects(assignment.Value, state);
                    Write(assignment.Target, value, state);
                    break;
                }

            default:
                Effects(operation, state);
                break;
        }
    }

    // What an operation does besides its value: the locals and parameters it writes, and whether it hands the key on.
    private void Effects(IOperation root, PathState state)
    {
        foreach (var operation in root.DescendantsAndSelf())
        {
            switch (operation)
            {
                case IDeconstructionAssignmentOperation deconstruction:
                    {
                        var dependsOnKey = Evaluate(deconstruction.Value, state).DependsOnKey;
                        foreach (var target in deconstruction.Target.DescendantsAndSelf().Where(target => Referenced(target) is not null))
                        {
                            Write(target, CallValue.Unknown(dependsOnKey), state);
                        }

                        break;
                    }

                case ISimpleAssignmentOperation assignment:
                    Write(assignment.Target, CallValue.Unknown(Evaluate(assignment.Value, state).DependsOnKey), state);
                    break;
                case IAssignmentOperation assignment:
                    Write(assignment.Target, CallValue.Unknown(Evaluate(assignment.Target, state).DependsOnKey || Evaluate(assignment.Value, state).DependsOnKey), state);
                    break;
                case IIncrementOrDecrementOperation increment:
                    Write(increment.Target, CallValue.Unknown(Evaluate(increment.Target, state).DependsOnKey), state);
                    break;
                case IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument:
                    Write(argument.Value, CallValue.Unknown(argument.Parent is { } call && Inputs(call).Any(input => Evaluate(input, state).DependsOnKey)), state);
                    break;
                case IInvocationOperation { TargetMethod.MethodKind: MethodKind.LocalFunction }:
                    state.IsKeyShared = true;
                    break;
                case IInvocationOperation or IObjectCreationOperation or IPropertyReferenceOperation or IDynamicInvocationOperation or IDynamicMemberReferenceOperation or IDynamicObjectCreationOperation
                    when !IsPure(operation) && Inputs(operation).Any(input => Evaluate(input, state).DependsOnKey):
                    state.IsKeyShared = true;
                    break;
                case IFlowAnonymousFunctionOperation function when HoldsKey(_graph, function, state):
                    state.IsKeyShared = true;
                    break;
                default:
                    break;
            }
        }
    }

    private void Write(IOperation target, CallValue value, PathState state)
    {
        switch (target)
        {
            case ILocalReferenceOperation local:
                state.Symbols[local.Local] = value;
                break;
            case IParameterReferenceOperation parameter:
                state.Symbols[parameter.Parameter] = value;
                state.IsKeyWritten |= SymbolEqualityComparer.Default.Equals(parameter.Parameter, _key);
                break;
            case IFlowCaptureReferenceOperation capture:
                state.Captures[capture.Id] = value;
                break;
            case IDeclarationExpressionOperation declaration:
                Write(declaration.Expression, value, state);
                break;
            default:
                state.IsKeyShared |= value.DependsOnKey;
                break;
        }
    }

    // What operations write may hold anything after them, and a finally region reading the key, run on the way out, may hand it on.
    private void Havoc(PathState state, IReadOnlyList<IOperation> operations, PathState? reading)
    {
        foreach (var symbol in WrittenSymbols(operations))
        {
            state.Symbols[symbol] = CallValue.Unknown(true);
            state.IsKeyWritten |= SymbolEqualityComparer.Default.Equals(symbol, _key);
        }

        state.IsKeyShared |= reading is not null && ReadsKey(operations, reading);
    }

    private bool ReadsKey(IEnumerable<IOperation> operations, PathState state)
        => operations.SelectMany(operation => operation.DescendantsAndSelf()).Any(operation => operation switch
        {
            IParameterReferenceOperation or ILocalReferenceOperation or IFlowCaptureReferenceOperation => Evaluate(operation, state).DependsOnKey,
            IInvocationOperation or IPropertyReferenceOperation or IFieldReferenceOperation => state.IsKeyShared,
            _ => false,
        });

    private CallValue Evaluate(IOperation operation, PathState state)
    {
        if (operation.ConstantValue is { HasValue: true, Value: var constant })
        {
            return CallValue.Of(constant, false);
        }

        return operation switch
        {
            IParameterReferenceOperation parameter => Read(parameter.Parameter, state),
            ILocalReferenceOperation local => Read(local.Local, state),
            IFlowCaptureReferenceOperation capture => state.Captures.TryGetValue(capture.Id, out var captured) ? captured : CallValue.Unknown(true),
            IConversionOperation { Conversion: { IsUserDefined: false } conversion } converted when Keeps(conversion, converted) => Evaluate(converted.Operand, state),
            IParenthesizedOperation parenthesized => Evaluate(parenthesized.Operand, state),
            IUnaryOperation { OperatorKind: UnaryOperatorKind.Not, OperatorMethod: null } not => Not(Evaluate(not.Operand, state)),
            IBinaryOperation { IsCompareText: false } binary when binary.OperatorMethod is null || binary.OperatorMethod.ContainingType.SpecialType == SpecialType.System_String
                => Binary(binary, Evaluate(binary.LeftOperand, state), Evaluate(binary.RightOperand, state)),
            IIsNullOperation isNull => Map(Evaluate(isNull.Operand, state), value => value is null),
            IIsPatternOperation pattern => Match(pattern.Pattern, Evaluate(pattern.Value, state), state),
            IFieldReferenceOperation { Field: { IsStatic: true, Name: EmptyField, ContainingType.SpecialType: SpecialType.System_String } } => CallValue.Of(string.Empty, false),
            IPropertyReferenceOperation { Property: { Name: LengthProperty, ContainingType.SpecialType: SpecialType.System_String }, Instance: { } text }
                => Map(Evaluate(text, state), value => value is string known ? known.Length : null, rejectNull: true),
            IInvocationOperation { TargetMethod.ContainingType.SpecialType: SpecialType.System_String } invocation when Text(invocation, state) is { } known => known,
            IInvocationOperation { TargetMethod.MethodKind: MethodKind.LocalFunction } => CallValue.Unknown(true),
            _ => CallValue.Unknown(state.IsKeyShared || operation.ChildOperations.Any(child => Evaluate(child, state).DependsOnKey)),
        };
    }

    // A conversion keeps a value it does not change: one between references, into or out of a box or a nullable, or an integer
    // widening into a wider integer; any other turns the value into one the flow does not compute.
    private static bool Keeps(CommonConversion conversion, IConversionOperation converted)
        => !conversion.IsNumeric || conversion.IsImplicit && IsInteger(converted.Type) && IsInteger(converted.Operand.Type);

    private static bool IsInteger(ITypeSymbol? type)
        => type?.SpecialType is SpecialType.System_Char or SpecialType.System_SByte or SpecialType.System_Byte or SpecialType.System_Int16
            or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64;

    // A primary constructor's parameter is the object's state; a local no path assigned, as a pattern declares one, may hold the key.
    private CallValue Read(ISymbol symbol, PathState state)
        => _volatile.Contains(symbol) ? CallValue.Unknown(true)
            : state.Symbols.TryGetValue(symbol, out var value) ? value
            : symbol is IParameterSymbol { ContainingSymbol: var owner } && !SymbolEqualityComparer.Default.Equals(owner, _key.ContainingSymbol) ? CallValue.Unknown(state.IsKeyShared)
            : CallValue.Unknown(true);

    // What string's own methods return for text the flow knows, where they compare ordinally.
    private CallValue? Text(IInvocationOperation invocation, PathState state)
    {
        var method = invocation.TargetMethod;
        CallValue[] values = [.. invocation.Arguments.Select(argument => Evaluate(argument.Value, state))];
        var instance = invocation.Instance is { } receiver ? Evaluate(receiver, state) : (CallValue?)null;
        var dependsOnKey = instance?.DependsOnKey == true || values.Any(value => value.DependsOnKey);
        if (instance is { IsConstant: false } || values.Any(value => !value.IsConstant))
        {
            return CallValue.Unknown(dependsOnKey);
        }

        object?[] known = [.. values.Select(value => value.Constant)];
        object? result = (method.IsStatic, method.Name, instance?.Constant, known) switch
        {
            (true, IsNullOrEmptyMethod, _, [var text]) when text is null or string => string.IsNullOrEmpty((string?)text),
            (true, IsNullOrWhiteSpaceMethod, _, [var text]) when text is null or string => string.IsNullOrWhiteSpace((string?)text),
            (false, ContainsMethod, string text, [string part]) => text.Contains(part, StringComparison.Ordinal),
            (false, ContainsMethod, string text, [char part]) => text.Contains(part, StringComparison.Ordinal),
            (false, StartsWithMethod, string text, [char part]) => text.Length > 0 && text[0] == part,
            (false, EndsWithMethod, string text, [char part]) => text.Length > 0 && text[^1] == part,
            (false, StartsWithMethod, string text, [string part, int comparison]) when comparison == (int)StringComparison.Ordinal => text.StartsWith(part, StringComparison.Ordinal),
            (false, EndsWithMethod, string text, [string part, int comparison]) when comparison == (int)StringComparison.Ordinal => text.EndsWith(part, StringComparison.Ordinal),
            _ => null,
        };

        return result is null ? null : CallValue.Of(result, dependsOnKey);
    }

    private CallValue Match(IPatternOperation pattern, CallValue value, PathState state)
        => pattern switch
        {
            IConstantPatternOperation constant => Equal(value, Evaluate(constant.Value, state), negated: false, value.DependsOnKey),
            INegatedPatternOperation negated => Not(Match(negated.Pattern, value, state)),
            IDiscardPatternOperation => CallValue.Of(true, value.DependsOnKey),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or or BinaryOperatorKind.And } binary
                => Logical(binary.OperatorKind == BinaryOperatorKind.And, Match(binary.LeftPattern, value, state), Match(binary.RightPattern, value, state)),
            IDeclarationPatternOperation { MatchesNull: var matchesNull } when value is { IsConstant: true, Constant: null } => CallValue.Of(matchesNull, value.DependsOnKey),
            ITypePatternOperation or IRecursivePatternOperation when value is { IsConstant: true, Constant: null } => CallValue.Of(false, value.DependsOnKey),
            _ => CallValue.Unknown(value.DependsOnKey || state.IsKeyShared),
        };

    private static CallValue Binary(IBinaryOperation binary, CallValue left, CallValue right)
    {
        var dependsOnKey = left.DependsOnKey || right.DependsOnKey;

        // Visual Basic compares text with Nothing as it compares it with "".
        if (binary.Language == LanguageNames.VisualBasic
            && binary.LeftOperand.Type?.SpecialType == SpecialType.System_String
            && binary.RightOperand.Type?.SpecialType == SpecialType.System_String)
        {
            left = left is { IsConstant: true, Constant: null } ? left with { Constant = string.Empty } : left;
            right = right is { IsConstant: true, Constant: null } ? right with { Constant = string.Empty } : right;
        }

        return binary.OperatorKind switch
        {
            BinaryOperatorKind.ConditionalAnd => Logical(true, left, right),
            BinaryOperatorKind.ConditionalOr => Logical(false, left, right),
            BinaryOperatorKind.And when left.Constant is bool or null && right.Constant is bool or null => Logical(true, left, right),
            BinaryOperatorKind.Or when left.Constant is bool or null && right.Constant is bool or null => Logical(false, left, right),
            BinaryOperatorKind.Equals or BinaryOperatorKind.ObjectValueEquals => Equal(left, right, negated: false, dependsOnKey),
            BinaryOperatorKind.NotEquals or BinaryOperatorKind.ObjectValueNotEquals => Equal(left, right, negated: true, dependsOnKey),
            BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual
                when left.IsConstant && right.IsConstant && CallValue.Integer(left.Constant) is { } first && CallValue.Integer(right.Constant) is { } second
                => CallValue.Of(
                    binary.OperatorKind switch
                    {
                        BinaryOperatorKind.LessThan => first < second,
                        BinaryOperatorKind.LessThanOrEqual => first <= second,
                        BinaryOperatorKind.GreaterThan => first > second,
                        _ => first >= second,
                    },
                    dependsOnKey),
            _ => CallValue.Unknown(dependsOnKey),
        };
    }

    private static CallValue Equal(CallValue left, CallValue right, bool negated, bool dependsOnKey)
        => left.IsConstant && right.IsConstant
            ? CallValue.Of(CallValue.Same(left.Constant, right.Constant) != negated, dependsOnKey || left.DependsOnKey || right.DependsOnKey)
            : CallValue.Unknown(dependsOnKey || left.DependsOnKey || right.DependsOnKey);

    // Either side settles a conjunction it makes false and a disjunction it makes true.
    private static CallValue Logical(bool isAnd, CallValue left, CallValue right)
    {
        var dependsOnKey = left.DependsOnKey || right.DependsOnKey;
        if (left is { IsConstant: true, Constant: bool first } && first != isAnd)
        {
            return CallValue.Of(first, left.DependsOnKey);
        }

        if (right is { IsConstant: true, Constant: bool second } && second != isAnd)
        {
            return CallValue.Of(second, right.DependsOnKey);
        }

        return left is { IsConstant: true, Constant: bool } && right is { IsConstant: true, Constant: bool }
            ? CallValue.Of(isAnd, dependsOnKey)
            : CallValue.Unknown(dependsOnKey);
    }

    private static CallValue Not(CallValue value)
        => value is { IsConstant: true, Constant: bool known } ? CallValue.Of(!known, value.DependsOnKey) : CallValue.Unknown(value.DependsOnKey);

    private static CallValue Map(CallValue value, Func<object?, object?> map, bool rejectNull = false)
        => value.IsConstant && !(rejectNull && value.Constant is null) && map(value.Constant) is { } mapped
            ? CallValue.Of(mapped, value.DependsOnKey)
            : CallValue.Unknown(value.DependsOnKey);

    private static bool IsPure(IOperation operation)
        => operation switch
        {
            IInvocationOperation { TargetMethod.ContainingType.SpecialType: SpecialType.System_String or SpecialType.System_Char } => true,
            IPropertyReferenceOperation { Property.ContainingType.SpecialType: SpecialType.System_String } => true,
            _ => false,
        };

    private static IEnumerable<IOperation> Inputs(IOperation operation)
        => operation switch
        {
            IInvocationOperation invocation => [.. invocation.Instance is { } instance ? [instance] : Array.Empty<IOperation>(), .. invocation.Arguments.Select(argument => argument.Value)],
            IObjectCreationOperation creation => creation.Arguments.Select(argument => argument.Value),
            IPropertyReferenceOperation property => [.. property.Instance is { } instance ? [instance] : Array.Empty<IOperation>(), .. property.Arguments.Select(argument => argument.Value)],
            _ => operation.ChildOperations,
        };

    // A lambda holding the key, or a value computed from it, may hand it anywhere it is passed.
    private bool HoldsKey(ControlFlowGraph graph, IFlowAnonymousFunctionOperation function, PathState state)
    {
        var body = graph.GetAnonymousFunctionControlFlowGraph(function);

        return body.Blocks
            .SelectMany(block => block.Operations.Concat(block.BranchValue is { } last ? [last] : []))
            .SelectMany(operation => operation.DescendantsAndSelf())
            .Any(operation => operation switch
            {
                IParameterReferenceOperation or ILocalReferenceOperation => Evaluate(operation, state).DependsOnKey,
                IFlowAnonymousFunctionOperation nested => HoldsKey(body, nested, state),
                _ => false,
            });
    }

    private IReadOnlyList<IOperation> Operations(ControlFlowRegion region)
        => [.. _graph.Blocks
            .Skip(region.FirstBlockOrdinal)
            .Take(region.LastBlockOrdinal - region.FirstBlockOrdinal + 1)
            .SelectMany(block => block.Operations.Concat(block.BranchValue is { } last ? [last] : []))];

    // The one place the target sits in the graph: its block, and the operation of the block holding it, where the branch's value counts last.
    private static (BasicBlock Block, int Index, IOperation Copy)? Locate(ControlFlowGraph graph, IOperation target)
    {
        (BasicBlock Block, int Index, IOperation Copy)? found = null;
        foreach (var block in graph.Blocks)
        {
            for (var index = 0; index <= block.Operations.Length; index++)
            {
                var operation = index < block.Operations.Length ? block.Operations[index] : block.BranchValue;
                foreach (var copy in operation?.DescendantsAndSelf() ?? [])
                {
                    if (copy.Syntax == target.Syntax && copy.Kind == target.Kind)
                    {
                        if (found is not null)
                        {
                            return null;
                        }

                        found = (block, index, copy);
                    }
                }
            }
        }

        return found;
    }

    // A block in a try may throw into the handlers of every try around it, running the finally regions it leaves on the way.
    private static List<(int Ordinal, IReadOnlyList<ControlFlowRegion> Finallies)> Handlers(BasicBlock block)
    {
        var handlers = new List<(int Ordinal, IReadOnlyList<ControlFlowRegion> Finallies)>();
        var finallies = new List<ControlFlowRegion>();
        for (var region = block.EnclosingRegion; region?.EnclosingRegion is { } parent; region = parent)
        {
            if (region.Kind != ControlFlowRegionKind.Try)
            {
                continue;
            }

            if (parent.Kind == ControlFlowRegionKind.TryAndCatch)
            {
                handlers.AddRange(parent.NestedRegions.Where(nested => nested != region).Select(nested => (nested.FirstBlockOrdinal, (IReadOnlyList<ControlFlowRegion>)[.. finallies])));
            }
            else if (parent.Kind == ControlFlowRegionKind.TryAndFinally)
            {
                finallies.AddRange(parent.NestedRegions.Where(nested => nested.Kind == ControlFlowRegionKind.Finally));
            }
        }

        return handlers;
    }

    private static bool IsInHandler(ControlFlowRegion? region)
    {
        for (; region is not null; region = region.EnclosingRegion)
        {
            if (region.Kind is ControlFlowRegionKind.Catch or ControlFlowRegionKind.Filter or ControlFlowRegionKind.FilterAndHandler
                or ControlFlowRegionKind.Finally or ControlFlowRegionKind.ErroneousBody)
            {
                return true;
            }
        }

        return false;
    }

    private static IOperation Root(IOperation operation)
    {
        while (operation.Parent is { } parent)
        {
            operation = parent;
        }

        return operation;
    }

    // A local or parameter a lambda or a local function writes, or one aliased by reference, may change between any two operations.
    private static HashSet<ISymbol> Volatile(IOperation root)
    {
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var operation in root.DescendantsAndSelf())
        {
            switch (operation)
            {
                case IAnonymousFunctionOperation or ILocalFunctionOperation:
                    symbols.UnionWith(WrittenSymbols(operation.ChildOperations));
                    break;
                case ISimpleAssignmentOperation { IsRef: true, Value: var aliased } when Referenced(aliased) is { } symbol:
                    symbols.Add(symbol);
                    break;
                case IVariableDeclaratorOperation { Symbol.IsRef: true, Initializer.Value: var aliased } when Referenced(aliased) is { } symbol:
                    symbols.Add(symbol);
                    break;
                case IAddressOfOperation { Reference: var aliased } when Referenced(aliased) is { } symbol:
                    symbols.Add(symbol);
                    break;
                default:
                    break;
            }
        }

        return symbols;
    }

    private static HashSet<ISymbol> WrittenSymbols(IEnumerable<IOperation> operations)
    {
        var symbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var operation in operations.SelectMany(operation => operation.DescendantsAndSelf()))
        {
            IEnumerable<IOperation> targets = operation switch
            {
                IDeconstructionAssignmentOperation deconstruction => deconstruction.Target.DescendantsAndSelf(),
                IAssignmentOperation assignment => [assignment.Target],
                IIncrementOrDecrementOperation increment => [increment.Target],
                IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } argument => [argument.Value],
                _ => [],
            };

            symbols.UnionWith(targets.Select(Referenced).OfType<ISymbol>());
        }

        return symbols;
    }

    private static bool WritesAnything(IOperation operation)
        => WrittenSymbols([operation]).Count > 0
            || operation.DescendantsAndSelf().Any(descendant => descendant is IAssignmentOperation or IIncrementOrDecrementOperation);

    private static ISymbol? Referenced(IOperation operation)
        => operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IDeclarationExpressionOperation declaration => Referenced(declaration.Expression),
            _ => null,
        };

    // The alternatives a block chooses between where it cannot tell which it takes, and whether the choice turns on the key.
    private sealed record Choice(IReadOnlyList<int> Destinations, bool DependsOnKey);

    // What the locals, parameters and captured values hold on the paths reaching a block, joined where paths meet.
    private sealed class PathState
    {
        public Dictionary<ISymbol, CallValue> Symbols { get; private init; } = [with(SymbolEqualityComparer.Default)];

        public Dictionary<CaptureId, CallValue> Captures { get; private init; } = [];

        public bool IsKeyWritten { get; set; }

        public bool IsKeyShared { get; set; }

        public PathState Copy()
            => new()
            {
                Symbols = new(Symbols, SymbolEqualityComparer.Default),
                Captures = [with(Captures)],
                IsKeyWritten = IsKeyWritten,
                IsKeyShared = IsKeyShared,
            };

        // Joins another path into this one, and says whether anything changed.
        public bool Absorb(PathState incoming)
        {
            var changed = Absorb(Symbols, incoming.Symbols) | Absorb(Captures, incoming.Captures);
            if (incoming.IsKeyWritten && !IsKeyWritten)
            {
                IsKeyWritten = changed = true;
            }

            if (incoming.IsKeyShared && !IsKeyShared)
            {
                IsKeyShared = changed = true;
            }

            return changed;
        }

        // A value one path sets and the other does not is unknown where they meet.
        private static bool Absorb<TName>(Dictionary<TName, CallValue> own, Dictionary<TName, CallValue> incoming)
            where TName : notnull
        {
            var changed = false;
            foreach (var name in own.Keys.Union(incoming.Keys, own.Comparer).ToList())
            {
                var joined = own.TryGetValue(name, out var existing) && incoming.TryGetValue(name, out var arriving)
                    ? CallValue.Join(existing, arriving)
                    : CallValue.Unknown(true);
                if (!own.TryGetValue(name, out var previous) || previous != joined)
                {
                    own[name] = joined;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
