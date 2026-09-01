using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using BetaPlatform.Controllers.Api;
using BetaPlatform.Services.Api;
using BetaPlatform.ViewModels.Api;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// The outcome-to-status-code map (005 SC-003, FR-034).
/// </summary>
/// <remarks>
/// <para>
/// These tests are the reason the deferred responses cannot be quietly reinvented later. The sample
/// services in this slice never return <see cref="ApiOutcome.NotFound"/>,
/// <see cref="ApiOutcome.Conflict"/> or <see cref="ApiOutcome.Invalid"/> — but the contract
/// specifies all three, so the controller must already answer them correctly. Here they are driven
/// directly through a stub service.
/// </para>
/// <para>
/// If the behaviour slice ever needs to <em>add</em> one of these branches rather than merely reach
/// it, this feature was built wrong and these tests are where that shows up.
/// </para>
/// </remarks>
public class ApiOutcomeMappingTests
{
    /// <summary>Returns whatever outcome a test asks for, so unreachable branches become reachable.</summary>
    private sealed class StubProductApiService : IProductApiService
    {
        private readonly ApiResult<ProductResponse> _single;

        public StubProductApiService(ApiResult<ProductResponse> single) => _single = single;

        public Task<ApiResult<IReadOnlyList<ProductResponse>>> GetAllAsync(bool activeOnly) =>
            Task.FromResult(ApiResult<IReadOnlyList<ProductResponse>>.Ok([]));

        public Task<ApiResult<ProductResponse>> GetByCodeAsync(string productCode) =>
            Task.FromResult(_single);

        public Task<ApiResult<ProductResponse>> CreateAsync(CreateProductRequest request) =>
            Task.FromResult(_single);
    }

    /// <summary>
    /// A controller wired up enough to produce real ProblemDetails — the factory is normally
    /// resolved from the request's services, which a bare unit test has none of.
    /// </summary>
    private static ProductsApiController NewController(ApiResult<ProductResponse> result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        var provider = services.BuildServiceProvider();

        return new ProductsApiController(new StubProductApiService(result))
        {
            ProblemDetailsFactory = provider.GetRequiredService<ProblemDetailsFactory>(),
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = provider }
            }
        };
    }

    [Fact]
    public async Task Success_Maps_To_200_With_The_Product()
    {
        var product = new ProductResponse { ProductCode = "RM-1", ProductName = "x", Unit = "kg", IsActive = true };

        var response = await NewController(ApiResult<ProductResponse>.Ok(product)).GetByCode("RM-1");

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(product, ok.Value);
    }

    [Fact]
    public async Task NotFound_Maps_To_404()
    {
        // Unreachable in this slice — the sample catalogue always resolves. Specified all the same.
        var result = ApiResult<ProductResponse>.NotFound("No product exists with code 'RM-999'.");

        var response = await NewController(result).GetByCode("RM-999");

        var problem = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.IsType<ProblemDetails>(problem.Value);
    }

    [Fact]
    public async Task Conflict_Maps_To_409_Not_400()
    {
        // A duplicate code is a well-formed request the data disagrees with. Keeping it out of the
        // 400 family is what lets a caller retry with a new code instead of re-checking its payload.
        var result = ApiResult<ProductResponse>.Conflict("A product with this code already exists.");

        var response = await NewController(result).GetByCode("RM-1");

        var problem = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task Invalid_Maps_To_400_And_Names_The_Offending_Field()
    {
        // FR-023: when a request carries a list of input codes and one output code, the caller must
        // be told which entry failed — so an input names its position, not just its field.
        var result = ApiResult<ProductResponse>.Invalid("inputProductCodes[1]", "No product exists with code 'RM-999'.");

        var response = await NewController(result).GetByCode("RM-999");

        var problem = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);

        var validation = Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.True(validation.Errors.ContainsKey("inputProductCodes[1]"));
        Assert.Contains("RM-999", validation.Errors["inputProductCodes[1]"][0]);
    }

    [Fact]
    public async Task Every_Outcome_Maps_To_A_Distinct_Status_Code()
    {
        // SC-003 in one assertion: a caller branches on the code alone, never on message text.
        var codes = new List<int?>();

        foreach (var result in new[]
                 {
                     ApiResult<ProductResponse>.NotFound("x"),
                     ApiResult<ProductResponse>.Conflict("x"),
                     ApiResult<ProductResponse>.Invalid("field", "x")
                 })
        {
            var response = await NewController(result).GetByCode("any");
            codes.Add(Assert.IsAssignableFrom<ObjectResult>(response).StatusCode);
        }

        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public async Task Create_Success_Maps_To_201_Pointing_At_The_Product_Code()
    {
        var product = new ProductResponse { ProductCode = "RM-NEW-01", ProductName = "x", Unit = "kg", IsActive = true };
        var controller = NewController(ApiResult<ProductResponse>.Ok(product));

        var response = await controller.Create(new CreateProductRequest());

        var created = Assert.IsType<CreatedAtActionResult>(response);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        // The Location must address the product by CODE — there is no id to point at (FR-022).
        Assert.Equal("RM-NEW-01", created.RouteValues!["productCode"]);
    }

    [Fact]
    public async Task Create_Duplicate_Code_Maps_To_409()
    {
        // Unreachable in this slice — the sample service stores nothing, so no code is ever taken.
        // The contract promises this response, so the action must already produce it.
        var result = ApiResult<ProductResponse>.Conflict("A product with this code already exists.");

        var response = await NewController(result).Create(new CreateProductRequest());

        var problem = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task Empty_Catalogue_Is_An_Empty_List_Not_A_404()
    {
        var controller = NewController(ApiResult<ProductResponse>.Ok(new ProductResponse()));

        var response = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<ProductResponse>>(ok.Value));
    }
}
