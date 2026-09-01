using System.ComponentModel.DataAnnotations;
using BetaPlatform.ViewModels.Api;
using Xunit;

namespace BetaPlatform.Tests;

/// <summary>
/// Request-shape validation for the integration API (005 FR-015, FR-025).
/// </summary>
/// <remarks>
/// This is one of the parts of the contract-only slice that is genuinely enforced today, so it is
/// worth pinning: a caller is told exactly which field is wrong, and the lengths match the stored
/// columns so nothing gets as far as a truncation failure.
/// </remarks>
public class ApiValidationTests
{
    /// <summary>Runs the data annotations the way [ApiController] does, and reports the fields at fault.</summary>
    private static IReadOnlyList<string> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).Distinct().ToList();
    }

    private static CreateProductRequest ValidProduct() => new()
    {
        ProductCode = "RM-STEEL-01",
        ProductName = "لفائف صلب",
        ProductNameEnglish = "Steel Coil",
        Category = "Raw Material",
        Unit = "kg"
    };

    [Fact]
    public void Valid_Product_Passes()
    {
        Assert.Empty(Validate(ValidProduct()));
    }

    [Fact]
    public void Product_Requires_Code_Name_And_Unit()
    {
        // The empty-body case from quickstart check 5: all three must be named at once, so a caller
        // fixes its payload in one round trip rather than three.
        var invalid = Validate(new CreateProductRequest());

        Assert.Contains(nameof(CreateProductRequest.ProductCode), invalid);
        Assert.Contains(nameof(CreateProductRequest.ProductName), invalid);
        Assert.Contains(nameof(CreateProductRequest.Unit), invalid);
    }

    [Fact]
    public void Product_Optional_Fields_May_Be_Omitted()
    {
        var request = ValidProduct();
        request.ProductNameEnglish = null;
        request.Category = null;

        Assert.Empty(Validate(request));
    }

    [Theory]
    [InlineData(51, nameof(CreateProductRequest.ProductCode))]
    [InlineData(201, nameof(CreateProductRequest.ProductName))]
    public void Product_Rejects_Over_Length_Required_Fields(int length, string field)
    {
        var request = ValidProduct();
        var tooLong = new string('x', length);

        if (field == nameof(CreateProductRequest.ProductCode)) request.ProductCode = tooLong;
        else request.ProductName = tooLong;

        Assert.Contains(field, Validate(request));
    }

    [Fact]
    public void Product_Rejects_Over_Length_Unit_And_Category_And_English_Name()
    {
        // Lengths mirror the products table exactly (data-model.md), so the API refuses what the
        // column would silently truncate.
        var request = ValidProduct();
        request.Unit = new string('x', 21);
        request.Category = new string('x', 101);
        request.ProductNameEnglish = new string('x', 201);

        var invalid = Validate(request);

        Assert.Contains(nameof(CreateProductRequest.Unit), invalid);
        Assert.Contains(nameof(CreateProductRequest.Category), invalid);
        Assert.Contains(nameof(CreateProductRequest.ProductNameEnglish), invalid);
    }

    [Fact]
    public void Product_Boundary_Lengths_Are_Accepted()
    {
        // Exactly at the limit must pass — an off-by-one here would reject legitimate codes.
        var request = ValidProduct();
        request.ProductCode = new string('x', 50);
        request.ProductName = new string('x', 200);
        request.Unit = new string('x', 20);
        request.Category = new string('x', 100);
        request.ProductNameEnglish = new string('x', 200);

        Assert.Empty(Validate(request));
    }

    // ---- Work orders (US4) ----

    private static CreateWorkOrderRequest ValidWorkOrder() => new()
    {
        WorkOrderNumber = "WO-2026-0142",
        InputProductCodes = ["RM-STEEL-01", "RM-PAINT-02"],
        OutputProductCode = "FG-PANEL-07",
        PlannedStartTime = new DateTime(2026, 8, 29, 6, 0, 0),
        QtyToManufacture = 1200.5m
    };

    [Fact]
    public void Valid_Work_Order_Passes()
    {
        Assert.Empty(Validate(ValidWorkOrder()));
    }

    [Fact]
    public void Work_Order_Accepts_A_Single_Input_Code()
    {
        // The list is the shape; one entry is an ordinary order, not a special case.
        var request = ValidWorkOrder();
        request.InputProductCodes = ["RM-STEEL-01"];

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Work_Order_Requires_Number_Inputs_Output_And_Planned_Start()
    {
        var invalid = Validate(new CreateWorkOrderRequest());

        Assert.Contains(nameof(CreateWorkOrderRequest.WorkOrderNumber), invalid);
        Assert.Contains(nameof(CreateWorkOrderRequest.InputProductCodes), invalid);
        Assert.Contains(nameof(CreateWorkOrderRequest.OutputProductCode), invalid);
        Assert.Contains(nameof(CreateWorkOrderRequest.PlannedStartTime), invalid);
    }

    [Fact]
    public void Work_Order_Rejects_An_Empty_Input_List()
    {
        // An order consuming nothing is the same class of mistake as one manufacturing nothing, so
        // an empty list is refused rather than read as "no inputs" (FR-042).
        var request = ValidWorkOrder();
        request.InputProductCodes = [];

        Assert.Contains(nameof(CreateWorkOrderRequest.InputProductCodes), Validate(request));
    }

    [Fact]
    public void Work_Order_Rejects_A_Blank_Input_Code()
    {
        var request = ValidWorkOrder();
        request.InputProductCodes = ["RM-STEEL-01", "   "];

        Assert.Contains(nameof(CreateWorkOrderRequest.InputProductCodes), Validate(request));
    }

    [Theory]
    [InlineData("rm-steel-01")]
    [InlineData(" RM-STEEL-01 ")]
    public void Work_Order_Rejects_A_Repeated_Input_Code(string repeat)
    {
        // Case and padding are not part of a code's identity (research R9), so these are repeats.
        // A repeat carries no information — the contract attaches no quantity to an input — so it
        // can only be a mistake (FR-042).
        var request = ValidWorkOrder();
        request.InputProductCodes = ["RM-STEEL-01", repeat];

        Assert.Contains(nameof(CreateWorkOrderRequest.InputProductCodes), Validate(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void Work_Order_Rejects_A_Non_Positive_Quantity(double quantity)
    {
        // Zero is rejected as well as negative: an order to manufacture nothing is a mistake worth
        // catching at the edge rather than discovering on the shop floor.
        var request = ValidWorkOrder();
        request.QtyToManufacture = (decimal)quantity;

        Assert.Contains(nameof(CreateWorkOrderRequest.QtyToManufacture), Validate(request));
    }

    [Fact]
    public void Work_Order_Accepts_An_Output_That_Repeats_An_Input()
    {
        // A rework or re-packing order legitimately consumes and produces the same product, so this
        // must NOT be refused as a likely typo (US4 §4). The no-repeats rule governs the input list
        // only; input and output are two different fields.
        var request = ValidWorkOrder();
        request.InputProductCodes = ["RM-STEEL-01", "RM-PAINT-02"];
        request.OutputProductCode = "RM-STEEL-01";

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Work_Order_Rejects_Only_The_Quantity_When_Only_The_Quantity_Is_Wrong()
    {
        // The exact quickstart check 7 case: the output repeating an input, quantity zero. The
        // caller must be told about the quantity and nothing else, or it will "fix" the codes that
        // were fine.
        var request = ValidWorkOrder();
        request.InputProductCodes = ["RM-STEEL-01"];
        request.OutputProductCode = "RM-STEEL-01";
        request.QtyToManufacture = 0m;

        Assert.Equal([nameof(CreateWorkOrderRequest.QtyToManufacture)], Validate(request));
    }

    [Fact]
    public void Work_Order_Optional_Figures_May_Be_Omitted()
    {
        var request = ValidWorkOrder();
        request.MachineId = null;
        request.HourRate = null;
        request.LineSetupTimeMinutes = null;
        request.WorkstationCapabilityPerHour = null;

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void Work_Order_Rejects_Negative_Optional_Figures()
    {
        var request = ValidWorkOrder();
        request.HourRate = -1m;
        request.LineSetupTimeMinutes = -1;
        request.WorkstationCapabilityPerHour = -1m;
        request.MachineId = 0;

        var invalid = Validate(request);

        Assert.Contains(nameof(CreateWorkOrderRequest.HourRate), invalid);
        Assert.Contains(nameof(CreateWorkOrderRequest.LineSetupTimeMinutes), invalid);
        Assert.Contains(nameof(CreateWorkOrderRequest.WorkstationCapabilityPerHour), invalid);
        Assert.Contains(nameof(CreateWorkOrderRequest.MachineId), invalid);
    }

    [Fact]
    public void Work_Order_Rejects_An_Over_Length_Number()
    {
        var request = ValidWorkOrder();
        request.WorkOrderNumber = new string('x', 51);

        Assert.Contains(nameof(CreateWorkOrderRequest.WorkOrderNumber), Validate(request));
    }

    // ---- Sign-in ----

    [Fact]
    public void Login_Requires_A_Well_Formed_Email_And_A_Password()
    {
        var invalid = Validate(new LoginRequest { Email = "not-an-email" });

        Assert.Contains(nameof(LoginRequest.Email), invalid);
        Assert.Contains(nameof(LoginRequest.Password), invalid);
    }

    [Fact]
    public void Login_Does_Not_Enforce_The_Password_Policy()
    {
        // A three-character password is a WRONG password (401), not a malformed request (400).
        // Validating length here would confirm the policy exists and invite probing (FR-004).
        Assert.Empty(Validate(new LoginRequest { Email = "user@beta.local", Password = "abc" }));
    }
}
