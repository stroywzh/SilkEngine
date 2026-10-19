using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace SilkEngine.Internal;

/// <summary>
/// Construction flow behind <see cref="GameObject.AddComponent{T}()"/>.
/// </summary>
/// <typeparam name="T">
/// Component type with a public constructor taking a <see cref="GameObject"/>.
/// </typeparam>
/// <remarks>
/// <para>The constructor requires the owning entity, so a component created through this flow is
/// bound from the first instruction and can never exist without an owner.</para>
/// <para>Constructor lookup and delegate compilation run once per closed type (first use); every
/// later call is a single delegate invocation with no reflection. Types without a public
/// <c>(GameObject)</c> constructor are rejected before any instance is created.</para>
/// </remarks>
internal static class ComponentFactory<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
>
    where T : Component
{
    private static readonly Lazy<Func<GameObject, T>> Factory = new(Compile);

    /// <summary>Creates a new <typeparamref name="T"/> bound to <paramref name="owner"/>.</summary>
    /// <param name="owner">Entity that will own the component.</param>
    /// <returns>A new component instance whose <see cref="Component.gameObject"/> is <paramref name="owner"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> does not declare a public constructor taking a <see cref="GameObject"/>.
    /// </exception>
    public static T Create(GameObject owner) => Factory.Value(owner);

    private static Func<GameObject, T> Compile()
    {
        ConstructorInfo? constructor = typeof(T).GetConstructor(new[] { typeof(GameObject) });
        if (constructor is null)
        {
            throw new InvalidOperationException(
                $"Component {typeof(T).Name} must declare a public constructor taking a GameObject; "
                    + "components are always created with their owning entity."
            );
        }

        ParameterExpression owner = Expression.Parameter(typeof(GameObject), "owner");
        return Expression
            .Lambda<Func<GameObject, T>>(Expression.New(constructor, owner), owner)
            .Compile();
    }
}
