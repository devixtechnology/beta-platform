namespace BetaPlatform.ViewModels.Api;

/// <summary>
/// A machine as the API presents it, with everything the platform knows about it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This one carries its id</b> — deliberately, and in contrast to <see cref="ProductResponse"/>,
/// which hides the product's record number on purpose. The two are not inconsistent: a product has
/// an external identity, the <em>code</em> the plant prints and files by, so its record number is a
/// detail a caller must never need. A machine has no such external code on this surface, and
/// <c>machineId</c> is already the identifier <see cref="CreateWorkOrderRequest.MachineId"/> asks
/// for. Withholding it here would leave a caller unable to raise a work order against a machine it
/// can see.
/// </para>
/// <para>
/// <see cref="MachineCode"/> is returned too, because it is what the plant paints on the machine —
/// but it is not an address on this API, and nothing resolves by it.
/// </para>
/// </remarks>
public class MachineResponse
{
    /// <summary>
    /// The machine's identifier — the value to send as <c>machineId</c> when raising a work order.
    /// </summary>
    public int MachineId { get; set; }

    /// <summary>Short code painted on the machine. Informational; nothing resolves by it.</summary>
    public string MachineCode { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public int MachineTypeId { get; set; }

    /// <summary>Primary (Arabic) type name. Null when the type record is missing.</summary>
    public string? MachineTypeName { get; set; }

    public string? MachineTypeNameEnglish { get; set; }

    /// <summary>The production line this machine's type belongs to. There is no separate
    /// production-line record — the grouping label lives on the type.</summary>
    public string? ProductionLine { get; set; }

    /// <summary>False for a retired machine. A deactivated machine is still returned rather than
    /// hidden, so a caller reconciling history can see it.</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The administrator's commissioning flag, stored on the machine. <b>Not</b> the live state —
    /// see <see cref="RunningState"/>. They are separate answers and reading this one as "is it
    /// running right now" is the mistake the two names exist to prevent.
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Live state derived by the same rule every screen uses: <c>Running</c>, <c>Stopped</c> or
    /// <c>Unknown</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An in-progress work order on the machine wins outright — it is reported <c>Running</c>
    /// whatever telemetry says. Otherwise the latest reading decides, and a machine that has never
    /// reported, or whose last reading has aged past the staleness threshold, is <c>Stopped</c>:
    /// <b>silence means not producing</b>. <c>Unknown</c> survives only for a fresh reading whose
    /// status the platform cannot interpret — it is rare, and it means "the machine said something
    /// we do not understand", not "we have not heard from it".
    /// </para>
    /// <para>
    /// Sent as the state's <em>name</em>, never an integer — a caller should not have to learn an
    /// internal numbering, and that numbering stays free to change. This mirrors how a work order
    /// reports its status.
    /// </para>
    /// </remarks>
    public string RunningState { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
