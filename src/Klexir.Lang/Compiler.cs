using Klexir.Runtime;
using MonadicSharp;

namespace Klexir.Lang;

/// <summary>A compiled Klexir program: <see cref="Code"/> is the full bytecode (every closure's body laid out
/// before <see cref="EntryPoint"/>, the top-level expression's own code); run it with a <c>Klexir.Runtime.KlexirVm</c>.</summary>
public sealed record CompiledProgram(byte[] Code, int EntryPoint);

/// <summary>
/// Compiles a type-checked <see cref="TypedExpr"/> to <c>Klexir.Runtime</c> bytecode — currently the "core" subset
/// only (numbers, booleans, comparisons, <c>let</c>, <c>if</c>, closures, <c>let rec</c>); see the README's
/// "What can't Klexir do yet" for what isn't covered (String/List/Option/Result/record/union/plugins still only run
/// through <see cref="Evaluator"/>). Every emitted node leaves exactly one value on top of the stack beyond whatever
/// was there before it ran — the same invariant <see cref="Klexir.Runtime.KlexirVm"/>'s own opcodes rely on.
/// </summary>
public static class Compiler
{
    public static Result<CompiledProgram> Compile(TypedExpr expr)
    {
        var functions = new List<byte[]>();
        var mainBuilder = BytecodeBuilder.New();

        var result = Emit(expr, [], 0, mainBuilder, functions);
        if (result.IsFailure)
        {
            return Result<CompiledProgram>.Failure(result.Error);
        }

        mainBuilder.Halt();

        var functionsCode = functions.SelectMany(f => f).ToArray();
        var code = functionsCode.Concat(mainBuilder.Build()).ToArray();
        return Result<CompiledProgram>.Success(new CompiledProgram(code, functionsCode.Length));
    }

    private static Result<Unit> Emit(
        TypedExpr expr, IReadOnlyList<(string Name, Binding Binding)> env, int nextSlot,
        BytecodeBuilder builder, List<byte[]> functions)
    {
        switch (expr)
        {
            case TypedIntLiteral literal:
                builder.Push(literal.Value);
                return Result<Unit>.Success(Unit.Value);

            case TypedBoolLiteral literal:
                builder.Push(literal.Value ? 1 : 0);
                return Result<Unit>.Success(Unit.Value);

            case TypedIdentifier identifier:
                return EmitLoad(identifier.Name, env, builder);

            case TypedBinaryExpr binary:
                return EmitBinaryOperands(binary.Left, binary.Right, env, nextSlot, builder, functions)
                    .Bind(_ => EmitBinaryOp(binary.Operator, builder));

            case TypedComparisonExpr comparison:
                return EmitBinaryOperands(comparison.Left, comparison.Right, env, nextSlot, builder, functions)
                    .Bind(_ => EmitComparisonOp(comparison.Operator, builder));

            case TypedIfExpr ifExpr:
                return EmitIf(ifExpr, env, nextSlot, builder, functions);

            case TypedLetExpr let:
                return Emit(let.Value, env, nextSlot, builder, functions)
                    .Bind(_ => Emit(let.Body, Append(env, let.Name, new LocalBinding(nextSlot)), nextSlot + 1, builder, functions));

            case TypedFunExpr fun:
                return EmitClosure(env, self: null, fun.ParamName, fun.Body, builder, functions);

            case TypedAppExpr app:
                return Emit(app.Argument, env, nextSlot, builder, functions)
                    .Bind(_ => Emit(app.Function, env, nextSlot, builder, functions))
                    .Bind(_ =>
                    {
                        builder.CallIndirect(1);
                        return Result<Unit>.Success(Unit.Value);
                    });

            case TypedLetRecExpr letRec:
                return EmitClosure(env, self: letRec.Name, letRec.ParamName, letRec.FunctionBody, builder, functions)
                    .Bind(_ => Emit(letRec.LetBody, Append(env, letRec.Name, new LocalBinding(nextSlot)), nextSlot + 1, builder, functions));

            default:
                return Result<Unit>.Failure(Error.Create($"Compiler does not yet support '{expr.GetType().Name}'."));
        }
    }

    private static Result<Unit> EmitBinaryOperands(
        TypedExpr left, TypedExpr right, IReadOnlyList<(string Name, Binding Binding)> env, int nextSlot,
        BytecodeBuilder builder, List<byte[]> functions) =>
        Emit(left, env, nextSlot, builder, functions).Bind(_ => Emit(right, env, nextSlot, builder, functions));

    private static Result<Unit> EmitBinaryOp(BinaryOperator op, BytecodeBuilder builder)
    {
        switch (op)
        {
            case BinaryOperator.Add:
                builder.Add();
                break;
            case BinaryOperator.Sub:
                builder.Sub();
                break;
            case BinaryOperator.Mul:
                builder.Mul();
                break;
            case BinaryOperator.Div:
                builder.Div();
                break;
            default:
                return Result<Unit>.Failure(Error.Create($"Compiler does not support binary operator '{op}'."));
        }

        return Result<Unit>.Success(Unit.Value);
    }

    private static Result<Unit> EmitComparisonOp(ComparisonOperator op, BytecodeBuilder builder)
    {
        switch (op)
        {
            case ComparisonOperator.LessThan:
                builder.Lt();
                break;
            case ComparisonOperator.GreaterThan:
                builder.Gt();
                break;
            case ComparisonOperator.Equal:
                builder.Eq();
                break;
            case ComparisonOperator.LessThanOrEqual:
                builder.Le();
                break;
            case ComparisonOperator.GreaterThanOrEqual:
                builder.Ge();
                break;
            default:
                return Result<Unit>.Failure(Error.Create($"Compiler does not support comparison operator '{op}'."));
        }

        return Result<Unit>.Success(Unit.Value);
    }

    private static Result<Unit> EmitIf(
        TypedIfExpr ifExpr, IReadOnlyList<(string Name, Binding Binding)> env, int nextSlot,
        BytecodeBuilder builder, List<byte[]> functions)
    {
        var conditionResult = Emit(ifExpr.Condition, env, nextSlot, builder, functions);
        if (conditionResult.IsFailure)
        {
            return conditionResult;
        }

        var elseJump = builder.JumpIfZeroPlaceholder();

        var thenResult = Emit(ifExpr.Then, env, nextSlot, builder, functions);
        if (thenResult.IsFailure)
        {
            return thenResult;
        }

        var endJump = builder.JumpPlaceholder();
        builder.PatchInt32(elseJump, builder.CurrentAddress);

        var elseResult = Emit(ifExpr.Else, env, nextSlot, builder, functions);
        if (elseResult.IsFailure)
        {
            return elseResult;
        }

        builder.PatchInt32(endJump, builder.CurrentAddress);
        return Result<Unit>.Success(Unit.Value);
    }

    private static Result<Unit> EmitLoad(string name, IReadOnlyList<(string Name, Binding Binding)> env, BytecodeBuilder builder)
    {
        for (var i = env.Count - 1; i >= 0; i--)
        {
            if (env[i].Name != name)
            {
                continue;
            }

            switch (env[i].Binding)
            {
                case LocalBinding local:
                    builder.LoadLocal(local.Slot);
                    break;
                case UpvalueBinding upvalue:
                    builder.LoadLocal(1).LoadField(upvalue.FieldIndex);
                    break;
                case SelfBinding:
                    builder.LoadLocal(1);
                    break;
            }

            return Result<Unit>.Success(Unit.Value);
        }

        return Result<Unit>.Failure(Error.Create($"Compiler: unbound identifier '{name}'."));
    }

    /// <summary>
    /// Compiles a closure — a plain <c>fun</c> (<paramref name="self"/> null) or a <c>let rec</c>'s function
    /// (<paramref name="self"/> its own name, resolved as <see cref="SelfBinding"/> rather than an upvalue, so
    /// recursion needs no capture of "myself"). Captures every name currently in <paramref name="env"/> — simplest
    /// correct policy for this compiler; a real free-variable analysis would capture only what's referenced, at the
    /// cost of one unused closure field per uncaptured-but-visible outer name. Leaves exactly the new closure's
    /// handle on <paramref name="outerBuilder"/>'s stack.
    /// </summary>
    private static Result<Unit> EmitClosure(
        IReadOnlyList<(string Name, Binding Binding)> env, string? self, string paramName, TypedExpr body,
        BytecodeBuilder outerBuilder, List<byte[]> functions)
    {
        var captures = env;

        var innerEnv = new List<(string Name, Binding Binding)>();
        for (var i = 0; i < captures.Count; i++)
        {
            innerEnv.Add((captures[i].Name, new UpvalueBinding(1 + i)));
        }

        innerEnv.Add((paramName, new LocalBinding(0)));
        if (self is not null)
        {
            innerEnv.Add((self, new SelfBinding()));
        }

        var innerBuilder = BytecodeBuilder.New();
        var bodyResult = Emit(body, innerEnv, 2, innerBuilder, functions);
        if (bodyResult.IsFailure)
        {
            return bodyResult;
        }

        innerBuilder.Ret();

        var address = functions.Sum(f => f.Length);
        functions.Add(innerBuilder.Build());

        outerBuilder.NewObj(1 + captures.Count);
        outerBuilder.Dup();
        outerBuilder.Push(address);
        outerBuilder.StoreField(0);

        for (var i = 0; i < captures.Count; i++)
        {
            outerBuilder.Dup();
            var loadResult = EmitLoad(captures[i].Name, env, outerBuilder);
            if (loadResult.IsFailure)
            {
                return loadResult;
            }

            outerBuilder.StoreField(1 + i);
        }

        return Result<Unit>.Success(Unit.Value);
    }

    private static IReadOnlyList<(string Name, Binding Binding)> Append(
        IReadOnlyList<(string Name, Binding Binding)> env, string name, Binding binding) =>
        [.. env, (name, binding)];
}

internal abstract record Binding;

internal sealed record LocalBinding(int Slot) : Binding;

internal sealed record UpvalueBinding(int FieldIndex) : Binding;

internal sealed record SelfBinding : Binding;
