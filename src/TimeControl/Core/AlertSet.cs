using System.Numerics;

namespace TimeControl.Core;

/// <summary>A set of alerts: the ones switched on, or the ones that started since the last check.</summary>
public readonly record struct AlertSet(ulong Bits)
{
    public static readonly AlertSet None = default;

    public bool IsEmpty => Bits == 0;

    public static AlertSet Of(params Alert[] alerts)
    {
        var set = None;

        foreach (var alert in alerts) set = set.With(alert);

        return set;
    }

    public bool Has(Alert alert) => (Bits & Bit(alert)) != 0;

    /// <param name="isIn">Whether the alert goes in; false leaves the set as it is, which reads well beside a setting.</param>
    public AlertSet With(Alert alert, bool isIn = true) => isIn ? new AlertSet(Bits | Bit(alert)) : this;

    public AlertSet Without(Alert alert) => new(Bits & ~Bit(alert));

    /// <summary>The alerts in both sets.</summary>
    public AlertSet Within(AlertSet other) => new(Bits & other.Bits);

    /// <summary>The alert that comes first in the order of <see cref="Alert"/>, or null for an empty set.</summary>
    public Alert? First() => IsEmpty ? null : (Alert)BitOperations.TrailingZeroCount(Bits);

    private static ulong Bit(Alert alert) => 1UL << (int)alert;
}
