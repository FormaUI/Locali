namespace Forma.Locali.Sources;

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Microsoft.CodeAnalysis;

/// <summary>Reads the type a property's getter returns from an assembly's compiled code, read as data and never loaded.</summary>
internal static class CompiledGetters
{
    private const byte Nop = 0x00;
    private const byte LoadThis = 0x02;
    private const byte LoadLocal0 = 0x06;
    private const byte LoadLocal3 = 0x09;
    private const byte StoreLocal0 = 0x0A;
    private const byte StoreLocal3 = 0x0D;
    private const byte Call = 0x28;
    private const byte Return = 0x2A;
    private const byte BranchShort = 0x2B;
    private const byte LoadToken = 0xD0;

    /// <summary>What the getter returns: the instance's own type, a type named by its assembly and metadata name, or null where its code returns neither in one straight run.</summary>
    /// <param name="assemblyPath">The assembly holding the getter's compiled code.</param>
    /// <param name="getter">The getter, as a project reads it from that assembly's metadata.</param>
    public static Returned? Read(string assemblyPath, IMethodSymbol getter)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);
        ArgumentNullException.ThrowIfNull(getter);

        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var reader = new PEReader(stream);
            if (!reader.HasMetadata)
            {
                return null;
            }

            var metadata = reader.GetMetadataReader();

            return FindMethod(metadata, getter) is { RelativeVirtualAddress: > 0 } method
                ? Interpret(metadata, reader.GetMethodBody(method.RelativeVirtualAddress).GetILReader())
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    // A reference assembly is emitted apart from the one that runs, so the getter is found by its type's and its own names rather than by its token.
    private static MethodDefinition? FindMethod(MetadataReader metadata, IMethodSymbol getter)
    {
        var typeName = MetadataName(getter.ContainingType);
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            if (FullName(metadata, type) != typeName)
            {
                continue;
            }

            foreach (var methodHandle in type.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                if (metadata.StringComparer.Equals(method.Name, getter.MetadataName) && ParameterCount(metadata.GetBlobReader(method.Signature)) == 0)
                {
                    return method;
                }
            }
        }

        return null;
    }

    // The code is read as one straight run from its start to its one return, a branch to the instruction after it aside, as a
    // compiler emits `=> GetType()` and `=> typeof(X)` with or without optimization.
    private static Returned? Interpret(MetadataReader metadata, BlobReader code)
    {
        var stack = new Stack<Value>();
        var locals = new Dictionary<int, Value>();

        while (code.RemainingBytes > 0)
        {
            var opCode = code.ReadByte();
            switch (opCode)
            {
                case Nop:
                    break;
                case LoadThis:
                    stack.Push(Value.This);
                    break;
                case >= StoreLocal0 and <= StoreLocal3 when stack.Count > 0:
                    locals[opCode - StoreLocal0] = stack.Pop();
                    break;
                case >= LoadLocal0 and <= LoadLocal3 when locals.TryGetValue(opCode - LoadLocal0, out var local):
                    stack.Push(local);
                    break;
                case BranchShort when code.ReadSByte() == 0:
                    break;
                case LoadToken:
                    stack.Push(new Value(ValueKind.TypeHandle, ReadTypeName(metadata, MetadataTokens.EntityHandle(code.ReadInt32()))));
                    break;
                case Call when Called(metadata, MetadataTokens.EntityHandle(code.ReadInt32())) is { } called && stack.TryPop(out var argument):
                    stack.Push(called switch
                    {
                        ("System", "Object", "GetType") when argument.Kind == ValueKind.This => Value.InstanceType,
                        ("System", "Type", "GetTypeFromHandle") when argument is { Kind: ValueKind.TypeHandle, Type: { } named } => new Value(ValueKind.Type, named),
                        _ => Value.Unknown,
                    });
                    break;
                case Return when code.RemainingBytes == 0 && stack.Count == 1:
                    return stack.Pop() switch
                    {
                        { Kind: ValueKind.InstanceType } => new Returned(null),
                        { Kind: ValueKind.Type, Type: { } named } => new Returned(named),
                        _ => null,
                    };
                default:
                    return null;
            }
        }

        return null;
    }

    // A call is named by its declaring type's namespace and name and its own name, where it takes the one value its receiver or
    // its one parameter gives it.
    private static (string Namespace, string Type, string Method)? Called(MetadataReader metadata, EntityHandle handle)
    {
        if (handle.Kind != HandleKind.MemberReference)
        {
            return null;
        }

        var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
        if (member.Parent.Kind != HandleKind.TypeReference)
        {
            return null;
        }

        var signature = metadata.GetBlobReader(member.Signature);
        var header = signature.ReadSignatureHeader();
        var parameters = ParameterCount(metadata.GetBlobReader(member.Signature));
        if (parameters + (header.IsInstance ? 1 : 0) != 1)
        {
            return null;
        }

        var parent = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);

        return (metadata.GetString(parent.Namespace), metadata.GetString(parent.Name), metadata.GetString(member.Name));
    }

    private static int? ParameterCount(BlobReader signature)
    {
        var header = signature.ReadSignatureHeader();
        if (header.Kind != SignatureKind.Method)
        {
            return null;
        }

        if (header.IsGeneric)
        {
            signature.ReadCompressedInteger();
        }

        return signature.ReadCompressedInteger();
    }

    // A generic type is read by its open definition, as the runtime's factory reads it.
    private static TypeName? ReadTypeName(MetadataReader metadata, EntityHandle handle)
        => handle.Kind switch
        {
            HandleKind.TypeDefinition => new(null, FullName(metadata, metadata.GetTypeDefinition((TypeDefinitionHandle)handle))),
            HandleKind.TypeReference => ReadReference(metadata, (TypeReferenceHandle)handle),
            HandleKind.TypeSpecification => ReadGenericDefinition(metadata, (TypeSpecificationHandle)handle),
            _ => null,
        };

    private static TypeName? ReadReference(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var reference = metadata.GetTypeReference(handle);
        var name = metadata.GetString(reference.Name);

        return reference.ResolutionScope.Kind switch
        {
            HandleKind.AssemblyReference => new(
                metadata.GetString(metadata.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name),
                Join(metadata.GetString(reference.Namespace), name)),
            HandleKind.TypeReference when ReadReference(metadata, (TypeReferenceHandle)reference.ResolutionScope) is { } outer => outer with { MetadataName = $"{outer.MetadataName}+{name}" },
            HandleKind.ModuleDefinition => new(null, Join(metadata.GetString(reference.Namespace), name)),
            _ => null,
        };
    }

    private static TypeName? ReadGenericDefinition(MetadataReader metadata, TypeSpecificationHandle handle)
    {
        var signature = metadata.GetBlobReader(metadata.GetTypeSpecification(handle).Signature);
        if (signature.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
        {
            return null;
        }

        // The instance names its definition after the byte saying whether it is a class or a value type.
        signature.ReadSignatureTypeCode();

        return signature.ReadTypeHandle() is { IsNil: false } definition && definition.Kind != HandleKind.TypeSpecification
            ? ReadTypeName(metadata, definition)
            : null;
    }

    private static string FullName(MetadataReader metadata, TypeDefinition type)
    {
        var name = metadata.GetString(type.Name);
        var declaring = type.GetDeclaringType();

        return declaring.IsNil
            ? Join(metadata.GetString(type.Namespace), name)
            : $"{FullName(metadata, metadata.GetTypeDefinition(declaring))}+{name}";
    }

    private static string MetadataName(INamedTypeSymbol type)
        => type.ContainingType is { } outer
            ? $"{MetadataName(outer)}+{type.MetadataName}"
            : Join(type.ContainingNamespace is { IsGlobalNamespace: false } space ? space.ToDisplayString() : string.Empty, type.MetadataName);

    private static string Join(string space, string name)
        => space.Length > 0 ? $"{space}.{name}" : name;

    private enum ValueKind
    {
        Unknown,
        This,
        TypeHandle,
        Type,
        InstanceType,
    }

    // A value the code pushes: the instance, a type's handle or the type itself, the instance's own type, or anything else.
    private readonly record struct Value(ValueKind Kind, TypeName? Type = null)
    {
        public static Value Unknown { get; } = new(ValueKind.Unknown);

        public static Value This { get; } = new(ValueKind.This);

        public static Value InstanceType { get; } = new(ValueKind.InstanceType);
    }

    /// <summary>A type a getter's compiled code names, by the assembly declaring it and its metadata name.</summary>
    /// <param name="AssemblyName">The name of the assembly declaring the type, or null where the getter's own assembly does.</param>
    /// <param name="MetadataName">The type's namespace and name, a nested type's after its declaring type's and a plus.</param>
    internal sealed record TypeName(string? AssemblyName, string MetadataName);

    /// <summary>What a getter's compiled code returns: a type it names, or the instance's own type where the name is null.</summary>
    /// <param name="Name">The type the getter names, or null where it returns the instance's own type.</param>
    internal sealed record Returned(TypeName? Name);
}
