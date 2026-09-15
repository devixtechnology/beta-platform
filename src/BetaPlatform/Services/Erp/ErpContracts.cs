using System.Text.Json.Serialization;

namespace BetaPlatform.Services.Erp;

/// <summary>
/// The bodies the upstream ERP expects on <c>/api/upward/v1/mo/*</c>, exactly as the Beta Postman
/// collection documents them. Snake-case names are the ERP's, not ours, so every property is named
/// explicitly rather than left to a serializer policy — the wire shape is a contract someone else
/// owns and must not drift with a settings change on our side.
/// </summary>
/// <remarks>
/// A work order in Beta is a manufacturing order ("MO") to the ERP:
/// <c>mo_id</c> is <see cref="Data.Entities.WorkOrder.WorkOrderId"/> and <c>mo_reference</c> is
/// <see cref="Data.Entities.WorkOrder.WorkOrderNumber"/>.
/// </remarks>
public class MoEventRequest
{
    [JsonPropertyName("mo_id")]
    public int MoId { get; set; }

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
    /// Per-product consumption. <b>Always empty</b>, and deliberately so: Beta has nowhere to get
    /// these numbers from. <c>work_order_inputs</c> records fed-in weights and names no product;
    /// <c>work_order_input_products</c> names products and carries no quantity, which 005 research
    /// R13 declined on purpose. Splitting the total weight across the input products would be an
    /// invented number, and an invented number in an ERP is worse than an absent one. The field is
    /// carried anyway so the shape the ERP expects is present, and so filling it becomes a change
    /// of one line here on the day Beta can record consumption per product.
    /// </summary>
    [JsonPropertyName("consumed_components")]
    public IReadOnlyList<MoConsumedComponent> ConsumedComponents { get; set; } =
        Array.Empty<MoConsumedComponent>();
}

/// <summary>One material a manufacturing order consumed, and how much of it.</summary>
public class MoConsumedComponent
{
    [JsonPropertyName("product_id")]
    public int ProductId { get; set; }

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
