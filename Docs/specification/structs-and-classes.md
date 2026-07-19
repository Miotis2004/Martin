# Structs and classes

**Specification status:** Normative alpha specification
**Language version:** `0.1`
**Product version:** `0.1.0-alpha`
**Owner:** Type system

## Scope

This chapter specifies user-defined `struct` and `class` declarations in Martin `0.1`. Terminology and EBNF notation are defined in the [specification index](README.md#normative-terminology). General declaration, expression, and statement rules are specified in [declarations.md](declarations.md), [expressions.md](expressions.md), and [statements.md](statements.md).

## Declarations

A struct or class declaration introduces a named nominal type in the surrounding declaration scope.

```ebnf
struct-declaration ::= 'struct' identifier generic-parameters? inheritance-clause? type-body
class-declaration  ::= 'class' identifier generic-parameters? inheritance-clause? type-body
inheritance-clause ::= ':' type (',' type)*
type-body          ::= '{' type-member* '}'
type-member        ::= variable-declaration | initializer-declaration | function-declaration
```

The inheritance clause is used for explicit protocol conformance in the alpha language. Class inheritance is not supported. A type name must not conflict with another declaration in the same scope.

## Stored properties

Stored properties are declared with `let` or `var` inside the type body. A stored property has a name and an explicit type unless the declaration grammar for variables permits an initializer-derived type in the surrounding context.

A `let` stored property is immutable after initialization. A `var` stored property is mutable from mutating instance methods and from initialization code. Property access uses member selection:

```martin
struct Point {
    let x: Int
    let y: Int
}

let origin = Point(x: 0, y: 0)
print(origin.x)
```

## Initialization

A type may declare one or more `init` members. Constructor calls use the nominal type name followed by arguments. Argument labels must match the selected initializer or synthesized memberwise construction shape.

```martin
struct Counter {
    var value: Int

    init(value: Int) {
        self.value = value
    }
}

let counter = Counter(value: 1)
```

Initialization must definitely assign every stored property before the constructed value is used. A property must not be read through `self` before it is initialized. Throwing initializers are specified in [typed-errors.md](typed-errors.md#throwing-declarations).

## Methods and `self`

Instance methods are declared with `func` in the type body. Method bodies have an implicit `self` value whose type is the containing nominal type after generic substitution. Method calls use member selection and the ordinary call rules.

```martin
struct Greeter {
    let name: String

    func greeting() -> String {
        return name
    }
}
```

A mutating method must be declared with the `mutating` modifier when it assigns through `self` or changes stored state of a value type.

```martin
struct Tally {
    var value: Int

    mutating func increment() {
        value = value + 1
    }
}
```

A non-mutating method must not assign to stored state. Class instances have reference identity at runtime, but alpha method mutation rules still require the declaration to match the compiler's mutability checks.

## Value and reference behavior

Struct values have value semantics at Martin's language boundary. Assigning or passing a struct value produces an independent value according to the generated runtime representation. Class values have reference semantics: assigning or passing a class value copies the reference, and mutations through that reference are observed by all aliases.

Equality for user-defined structs and classes is not synthesized unless explicitly implemented by supported language features. Programs must not rely on undocumented host-language equality for nominal values.

## Protocol conformance

A struct or class conforms to a protocol only through an explicit inheritance clause, for example `struct File: Resettable`. The declaration must provide witnesses for all protocol requirements after applying generic substitution. Protocol rules are specified in [protocols.md](protocols.md).

## Generic nominal types

Structs and classes may declare generic parameters and constraints. A generic declaration is an open definition; a use outside the declaration body must be a constructed type such as `Box<Int>`, or a context where generic inference is explicitly supported. Constructed identity and substitution rules are specified in [generics.md](generics.md).

## Related samples

- [StructsAndClasses](../../samples/StructsAndClasses) demonstrates stored properties, synthesized construction, initializers, and methods.
- [Generics](../../samples/Generics) demonstrates a generic `struct` declaration and constructed type use.

## Deferred features

The following are not part of Martin `0.1` structs and classes:

- Class inheritance and overriding.
- Access control modifiers.
- Extensions.
- Static stored properties and static protocol requirements.
- Deinitializers.
- Reflection over fields or methods.
- Implicit derivation of equality, hashing, ordering, or printing.
