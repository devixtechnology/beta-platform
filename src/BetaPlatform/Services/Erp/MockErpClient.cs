using System.Text.Json;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services.Erp;

/// <summary>
/// Stands in for the ERP when <c>Erp:BaseUrl</c> is blank: every call is serialised, written to the
/// log, and answered as if the ERP had accepted it.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes the integration safe to ship before an ERP exists. The hooks in
/// <see cref="WorkOrderService"/> are live from day one and exercised by every start, hold and
/// finish; the only thing the missing configuration changes is whether the request leaves the
/// machine. When a base URL is filled in, nothing about the call sites changes.
/// </para>
/// <para>
/// The payload is logged at Information so the exact JSON an ERP would receive is visible without
/// turning on debug logging — during integration that log line is the thing you compare against the
/// ERP team's expectations.
/// </para>
/// </remarks>
public class MockErpClient : IErpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ILogger<MockErpClient> _logger;

    public MockErpClient(ILogger<MockErpClient> logger) => _logger = logger;

    public bool IsMock => true;

    public Task<ErpCallResult> NotifyStartedAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
        MockAsync(ErpEndpoints.Start, new MoEventRequest
        {
            MoReference = order.WorkOrderNumber
        });

    public Task<ErpCallResult> NotifyHeldAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
        MockAsync(ErpEndpoints.Hold, new MoEventRequest
        {
            MoReference = order.WorkOrderNumber
        });

    public Task<ErpCallResult> NotifyFinishedAsync(
        WorkOrder order,
        decimal actualProducedQty,
        IReadOnlyList<MoConsumedComponent> consumedComponents,
        CancellationToken cancellationToken = default) =>
        MockAsync(ErpEndpoints.Finish, new MoFinishRequest
        {
            MoReference = order.WorkOrderNumber,
            ActualProducedQty = actualProducedQty,
            ConsumedComponents = consumedComponents
        });

    private Task<ErpCallResult> MockAsync<TBody>(string path, TBody body)
    {
        _logger.LogInformation(
            "ERP integration is not configured (Erp:BaseUrl is blank) — mocking POST /{Path} with {Payload}",
            path, JsonSerializer.Serialize(body, JsonOptions));

        return Task.FromResult(ErpCallResult.MockedOk());
    }
}
