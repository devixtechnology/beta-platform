using System.Text.Json.Serialization;

namespace BetaPlatform.Services.Erp;

/// <summary>
/// The bodies the upstream ERP expects on <c>/api/upward/v1/mo/*</c>, exactly as the Beta Postman
/// collection documents them. Snake-case names are the ERP's, not ours, so every property is named
/// explicitly rather than left to a serializer policy — the wire shape is a contract someone else
/// owns and must not drift with a settings change on our side.
/// </summary>
/// <remarks>
/// A work order in Beta is a manufacturing order ("MO") to the ERP, named by its reference only:
/// <c>mo_reference</c> is <see cref="Data.Entities.WorkOrder.WorkOrderNumber"/>.
/// <para>
/// <c>mo_id</c> is deliberately <b>not</b> sent. The ERP's <c>mo_id</c> is its own MO id, and the
/// ERP resolves it before <c>mo_reference</c>; Beta's <c>WorkOrderId</c> is a different number, so
/// sending it made the ERP act on whichever MO happened to share that id (Beta work order 15,
/// <c>WH/MO/00047</c>, was resolved as <c>WH/MO/00015</c>).
/// </para>
/// </remarks>
public class MoEventRequest
{
    [JsonPropertyName("mo_reference")]
    public string MoReference { get; set; } = string.Empty;
}

/// <summary>The <c>mo/finish</c> body: the event plus what was actually produced and consumed.</summary>
public class MoFinishRequest : MoEventRequest
{
    /// <summary>
    /// What the line actually made, taken from the latest <c>oee_data</c> reading tagged with this
    /// order — the same figure the Work Order details screen shows as its output weight.
    /// </summary>
    [JsonPropertyName("actual_produced_qty")]
    public decimal ActualProducedQty { get; set; }

    /// <summary>
    /// Per-product consumption: the order's input weights totalled per product code. Inputs recorded
    /// before inputs named a product carry none and are left out rather than guessed.
    /// </summary>
    [JsonPropertyName("consumed_components")]
    public IReadOnlyList<MoConsumedComponent> ConsumedComponents { get; set; } =
        Array.Empty<MoConsumedComponent>();
}

/// <summary>One material a manufacturing order consumed, and how much of it.</summary>
public class MoConsumedComponent
{
    /// <summary>Beta's <b>product code</b> (e.g. <c>M10030</c>): the ERP creates products in Beta and
    /// keys them by that code. Named <c>product_id</c> on the wire because that is the ERP's name.</summary>
    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("actual_consumed_qty")]
    public decimal ActualConsumedQty { get; set; }
}

/// <summary>
/// How an outbound ERP call ended. Returned rather than thrown: no ERP failure may stop an operator
/// starting, holding or finishing a work order, so the caller is told what happened and carries on.
/// </summary>
/// <param name="Success">True when the ERP accepted the call, or when it was mocked.</param>
/// <param name="Mocked">True when no ERP is configured and the payload was logged, not sent.</param>
/// <param name="StatusCode">The HTTP status the ERP answered with, when it answered at all.</param>
/// <param name="Error">Why the call failed, for the log. The operator is told only that the ERP
/// could not be called, never this.</param>
public record ErpCallResult(bool Success, bool Mocked, int? StatusCode = null, string? Error = null)
{
    public static ErpCallResult Sent(int statusCode) => new(true, false, statusCode);

    public static ErpCallResult MockedOk() => new(true, true);

    public static ErpCallResult Failed(string error, int? statusCode = null) =>
        new(false, false, statusCode, error);

    /// <summary>No token stored yet — the call was not attempted.</summary>
    public static ErpCallResult NotCredentialed() =>
        new(false, false, null, "No ERP API token is stored. Set one on the ERP Settings screen.");
}
