using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services.Erp;

/// <summary>
/// The upstream ERP, as Beta uses it: three notifications that a manufacturing order changed state.
/// </summary>
/// <remarks>
/// <para>
/// Beta is the <em>client</em> here. This is the opposite direction from the <c>api/v1/*</c>
/// Integration API of features 005/006, which is Beta being called. The two share no code and no
/// credentials.
/// </para>
/// <para>
/// Two implementations sit behind this: <see cref="HttpErpClient"/> when <c>Erp:BaseUrl</c> is set,
/// and <see cref="MockErpClient"/> when it is blank. The choice is made once, in DI.
/// </para>
/// <para>
/// <b>No method throws.</b> Every failure comes back as an unsuccessful <see cref="ErpCallResult"/>.
/// That result is not advisory. The work-order service sends the notification <em>before</em> it
/// commits and refuses the transition when the call did not succeed, so a work order never reaches
/// a state the ERP was not told about. An ERP that is down therefore stops starts, holds and
/// finishes: consistency with the ERP is chosen over shop-floor availability, deliberately.
/// </para>
/// </remarks>
public interface IErpClient
{
    /// <summary>True when calls are being logged rather than sent, because no ERP is configured.</summary>
    bool IsMock { get; }

    /// <summary>Tells the ERP an order began — sent on first start and again on every resume.</summary>
    Task<ErpCallResult> NotifyStartedAsync(WorkOrder order, CancellationToken cancellationToken = default);

    /// <summary>Tells the ERP an order was paused.</summary>
    Task<ErpCallResult> NotifyHeldAsync(WorkOrder order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tells the ERP an order completed, with what it actually produced.
    /// </summary>
    /// <param name="order">The order that just finished.</param>
    /// <param name="actualProducedQty">
    /// The latest <c>oee_data</c> total weight recorded against the order. Zero when the order
    /// produced no telemetry — which is a real answer, not a missing one.
    /// </param>
    /// <param name="cancellationToken">Cancels the outbound call.</param>
    Task<ErpCallResult> NotifyFinishedAsync(
        WorkOrder order,
        decimal actualProducedQty,
        CancellationToken cancellationToken = default);
}

/// <summary>The ERP paths this integration uses, relative to <c>Erp:BaseUrl</c>.</summary>
public static class ErpEndpoints
{
    public const string Start = "api/upward/v1/mo/start";
    public const string Hold = "api/upward/v1/mo/hold";
    public const string Finish = "api/upward/v1/mo/finish";

    /// <summary>The header the ERP authenticates with.</summary>
    public const string ApiKeyHeader = "X-API-Key";
}
