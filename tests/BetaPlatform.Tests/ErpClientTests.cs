using System.Net;
using System.Text.Json;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services.Erp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The outbound ERP client: the URL it posts to, the header it authenticates with, the exact JSON
/// the ERP receives, and the promise that nothing it does can throw at the caller.
/// </summary>
public class ErpClientTests
{
    private static WorkOrder Order() => new()
    {
        WorkOrderId = 15,
        WorkOrderNumber = "MO/00123"
    };

    /// <summary>Captures the outbound request instead of sending it, and answers with a fixed status.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public CapturingHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "{}")
        {
            _status = status;
            _body = body;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("the ERP host could not be reached");
    }

    /// <summary>A settings store with a fixed answer, so these tests need no database.</summary>
    private sealed class StubSettings : IErpSettingsService
    {
        private readonly string? _token;
        public StubSettings(string? token) => _token = token;

        public Task<ErpSetting> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ErpSetting { ApiToken = _token });

        public Task<string?> GetApiTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_token);

        public Task SaveTokenAsync(string? apiToken, string? updatedBy, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static HttpErpClient Build(
        HttpMessageHandler handler,
        string? token = "token-abc",
        string? baseUrl = "https://erp.example.com") =>
        new(
            new HttpClient(handler),
            new StubSettings(token),
            Options.Create(new ErpOptions { BaseUrl = baseUrl }),
            NullLogger<HttpErpClient>.Instance);

    [Theory]
    [InlineData("https://erp.example.com")]
    [InlineData("https://erp.example.com/")] // a trailing slash must not produce a doubled one
    public async Task Start_Posts_To_The_Documented_Path(string baseUrl)
    {
        var handler = new CapturingHandler();

        await Build(handler, baseUrl: baseUrl).NotifyStartedAsync(Order());

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://erp.example.com/api/upward/v1/mo/start", handler.Request.RequestUri!.ToString());
    }

    [Fact]
    public async Task Hold_And_Finish_Post_To_Their_Own_Paths()
    {
        var hold = new CapturingHandler();
        await Build(hold).NotifyHeldAsync(Order());
        Assert.Equal("https://erp.example.com/api/upward/v1/mo/hold", hold.Request!.RequestUri!.ToString());

        var finish = new CapturingHandler();
        await Build(finish).NotifyFinishedAsync(Order(), 98.5m);
        Assert.Equal("https://erp.example.com/api/upward/v1/mo/finish", finish.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task The_Stored_Token_Is_Sent_As_The_X_Api_Key_Header()
    {
        var handler = new CapturingHandler();

        await Build(handler, token: "ge5SBOPcD2Srmb").NotifyStartedAsync(Order());

        Assert.True(handler.Request!.Headers.TryGetValues("X-API-Key", out var values));
        Assert.Equal("ge5SBOPcD2Srmb", Assert.Single(values!));
    }

    [Fact]
    public async Task The_Event_Body_Carries_The_Work_Order_Id_And_Number_As_Mo_Fields()
    {
        var handler = new CapturingHandler();

        await Build(handler).NotifyStartedAsync(Order());

        using var json = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(15, json.RootElement.GetProperty("mo_id").GetInt32());
        Assert.Equal("MO/00123", json.RootElement.GetProperty("mo_reference").GetString());
    }

    [Fact]
    public async Task The_Finish_Body_Carries_The_Produced_Quantity_And_An_Empty_Component_List()
    {
        var handler = new CapturingHandler();

        await Build(handler).NotifyFinishedAsync(Order(), 98.5m);

        using var json = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal(15, json.RootElement.GetProperty("mo_id").GetInt32());
        Assert.Equal(98.5m, json.RootElement.GetProperty("actual_produced_qty").GetDecimal());

        // Present but empty — Beta has no per-product consumption figure to put here, and an
        // invented split would be worse than an absent one. See MoFinishRequest.
        var components = json.RootElement.GetProperty("consumed_components");
        Assert.Equal(JsonValueKind.Array, components.ValueKind);
        Assert.Equal(0, components.GetArrayLength());
    }

    [Fact]
    public async Task No_Stored_Token_Means_The_Call_Is_Not_Attempted()
    {
        var handler = new CapturingHandler();

        var result = await Build(handler, token: null).NotifyStartedAsync(Order());

        Assert.False(result.Success);
        Assert.Null(handler.Request); // nothing was sent
    }

    [Fact]
    public async Task A_Refusal_Comes_Back_As_A_Result_Not_An_Exception()
    {
        var handler = new CapturingHandler(HttpStatusCode.Unauthorized, "bad key");

        var result = await Build(handler).NotifyStartedAsync(Order());

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task An_Unreachable_Erp_Comes_Back_As_A_Result_Not_An_Exception()
    {
        // The whole point of the client: a shop floor keeps running when the ERP is down.
        var result = await Build(new ThrowingHandler()).NotifyStartedAsync(Order());

        Assert.False(result.Success);
        Assert.Contains("could not be reached", result.Error);
    }

    [Fact]
    public async Task The_Mock_Client_Succeeds_Without_Sending_Anything()
    {
        var mock = new MockErpClient(NullLogger<MockErpClient>.Instance);

        Assert.True(mock.IsMock);
        foreach (var result in new[]
                 {
                     await mock.NotifyStartedAsync(Order()),
                     await mock.NotifyHeldAsync(Order()),
                     await mock.NotifyFinishedAsync(Order(), 98.5m)
                 })
        {
            Assert.True(result.Success);
            Assert.True(result.Mocked);
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("https://erp.example.com", false)]
    public void A_Blank_Base_Url_Is_What_Selects_The_Mock(string? baseUrl, bool expectMock)
    {
        // This is the condition Program.cs branches on when it resolves IErpClient.
        Assert.Equal(expectMock, !new ErpOptions { BaseUrl = baseUrl }.IsConfigured);
    }
}
