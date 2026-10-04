namespace AppTradingAlgoritmico.Domain.Enums;

/// <summary>
/// ftmo-group-simulation B2 (spec.md "The Response Envelope, Refusal Enum And Per-Kind Result Shape") —
/// why a group simulation, or one kind of it, produced no findings.
/// <para>
/// Zero-value choice: <see cref="InvalidRequest"/> is the generic, least-specific reason (the same
/// precedent as <see cref="FtmoSimulationRefusal.InvalidRequest"/>), so an unset field never reads as a
/// specific diagnosis such as "a member is missing a kind".
/// </para>
/// <para>
/// Group-wide (refuse the whole request, no kind runs): <see cref="InvalidRequest"/>,
/// <see cref="MemberNotFound"/>, <see cref="SharedInputsRefused"/>, <see cref="MixedSourceTimeZones"/>.
/// Kind-level (refuse one kind's panel): <see cref="MemberMissingKind"/>, <see cref="MemberRunRefused"/>,
/// <see cref="NoCommonWindow"/>, <see cref="MemberHasNoTradesInWindow"/>. No member of this enum affirms
/// an outcome: a refusal only says why nothing was computed.
/// </para>
/// </summary>
public enum FtmoGroupRefusal
{
    /// <summary>A present-but-unusable value (non-positive capital or risk, invalid source grid, no members). Group-wide. Also the CLR default.</summary>
    InvalidRequest = 0,

    /// <summary>The broker limits row refuses, or a zone cannot be resolved on this host (see <c>SharedRefusal</c>). Group-wide.</summary>
    SharedInputsRefused,

    /// <summary>A requested strategy id matches no strategy. Group-wide; the unknown ids are named.</summary>
    MemberNotFound,

    /// <summary>One or more members have no held run of this kind. Kind-level; lists every such member.</summary>
    MemberMissingKind,

    /// <summary>One or more members' runs refuse for a shipped <see cref="FtmoSimulationRefusal"/> reason, listed per member. Kind-level.</summary>
    MemberRunRefused,

    /// <summary>The members' source time zones are not all identical; each member is listed with its zone. Group-wide.</summary>
    MixedSourceTimeZones,

    /// <summary>The members' date ranges do not overlap for this kind. Kind-level; blames no member.</summary>
    NoCommonWindow,

    /// <summary>The window is non-empty but one or more members have no trades inside it. Kind-level.</summary>
    MemberHasNoTradesInWindow,
}
