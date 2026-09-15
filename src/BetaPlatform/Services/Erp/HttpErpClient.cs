using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using BetaPlatform.Data.Entities;

namespace BetaPlatform.Services.Erp;

/// <summary>
/// Sends manufacturing-order events to a real ERP over HTTP. Registered only when
/// <c>Erp:BaseUrl</c> is set; otherwise <see cref="MockErpClient"/> takes its place.
/// </summary>
/// <remarks>
/// The credential is read from the database on every call rather than captured once, so an
/// administrator rotating the token on the ERP Settings screen takes effect on the next work-order
/// event — no restart, no cache to invalidate.
/// </remarks>
public class HttpErpClient : IErpClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // The ERP's names are pinned per-property on the contract types, so no naming policy is
        // applied here — the wire shape must not be a serializer setting away from changing.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    private readonly HttpClient _http;
    private readonly IErpSettingsService _settings;
    private readonly ErpOptions _options;
    private readonly ILogger<HttpErpClient> _logger;

    public HttpErpClient(
        HttpClient http,
        IErpSettingsService settings,
        IOptions<ErpOptions> options,
        ILogger<HttpErpClient> logger)
    {
        _http = http;
        _settings = settings;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsMock => false;

    public Task<ErpCallResult> NotifyStartedAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
        PostAsync(ErpEndpoints.Start, Event(order), order, cancellationToken);

    public Task<ErpCallResult> NotifyHeldAsync(WorkOrder order, CancellationToken cancellationToken = default) =>
        PostAsync(ErpEndpoints.Hold, Event(order), order, cancellationToken);

    public Task<ErpCallResult> NotifyFinishedAsync(
        WorkOrder order,
        decimal actualProducedQty,
        CancellationToken cancellationToken = default) =>
        PostAsync(
            ErpEndpoints.Finish,
            new MoFinishRequest
            {
                MoId = order.WorkOrderId,
                MoReference = order.WorkOrderNumber,
                ActualProducedQty = actualProducedQty

                // ConsumedComponents stays at its empty default — see MoFinishRequest for why.
            },
            order,
            cancellationToken);

    private static MoEventRequest Event(WorkOrder order) =>
        new() { MoId = order.WorkOrderId, MoReference = order.WorkOrderNumber };

    private async Task<ErpCallResult> PostAsync<TBody>(
        string path,
        TBody body,
        WorkOrder order,
        CancellationToken cancellationToken)
    {
        var token = await _settings.GetApiTokenAsync(cancellationToken);
        if (token is null)
        {
            _logger.LogWarning(
                "ERP call to {Path} for work order {WorkOrderNumber} was not sent: no API token is stored.",
                path, order.WorkOrderNumber);
            return ErpCallResult.NotCredentialed();
        }

        var url = BuildUrl(path);
        var json = JsonSerializer.Serialize(body, JsonOptions);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.TryAddWithoutValidation(ErpEndpoints.ApiKeyHeader, token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "ERP accepted {Path} for work order {WorkOrderNumber} (mo_id {MoId}) with {Status}.",
                    path, order.WorkOrderNumber, order.WorkOrderId, status);
                return ErpCallResult.Sent(status);
            }

            // The body is read only to put a reason in the log. Nothing branches on it: the ERP's
            // error shape is not documented and guessing at it would be a second contract we do
            // not have.
            var reason = await SafeReadAsync(response, cancellationToken);
            _logger.LogError(
                "ERP refused {Path} for work order {WorkOrderNumber} (mo_id {MoId}) with {Status}: {Reason}",
                path, order.WorkOrderNumber, order.WorkOrderId, status, reason);
            return ErpCallResult.Failed(reason, status);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out. Distinguished from a caller-cancelled request so the log says which.
            _logger.LogError(ex,
                "ERP call to {Path} for work order {WorkOrderNumber} timed out after {Timeout}.",
                path, order.WorkOrderNumber, _options.Timeout);
            return ErpCallResult.Failed($"The ERP did not answer within {_options.Timeout.TotalSeconds:0} s.");
        }
        catch (Exception ex)
        {
            // Deliberately broad: no transport failure leaves this class as an exception, it
            // leaves as a failed result. What that failure means for the work order is the
            // caller's decision, not this class's.
            _logger.LogError(ex,
                "ERP call to {Path} for work order {WorkOrderNumber} failed.",
                path, order.WorkOrderNumber);
            return ErpCallResult.Failed(ex.Message);
        }
    }

    private string BuildUrl(string path) =>
        $"{(_options.BaseUrl ?? string.Empty).TrimEnd('/')}/{path}";

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "no response body"
                : body.Length > 500 ? body[..500] : body;
        }
        catch
        {
            return response.ReasonPhrase ?? "unreadable response body";
        }
    }
}
