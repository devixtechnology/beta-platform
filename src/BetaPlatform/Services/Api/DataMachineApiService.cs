using BetaPlatform.Data.Entities;
using BetaPlatform.ViewModels.Api;

namespace BetaPlatform.Services.Api;

/// <summary>
/// The machine list, answered from the platform's own records.
/// </summary>
/// <remarks>
/// Delegates to <see cref="IMachineService"/> for the machines and to
/// <see cref="IMachineStatusService"/> for their live state — the same pair the Machines screen
/// uses, so the API and the screen can never disagree about whether a machine is running.
/// </remarks>
public class DataMachineApiService : IMachineApiService
{
    private readonly IMachineService _machines;
    private readonly IMachineStatusService _status;

    public DataMachineApiService(IMachineService machines, IMachineStatusService status)
    {
        _machines = machines;
        _status = status;
    }

    public async Task<ApiResult<IReadOnlyList<MachineResponse>>> GetAllAsync(bool activeOnly)
    {
        // Both reads include the machine type, so the type name and production line come along
        // without a second query per row.
        var machines = activeOnly
            ? await _machines.GetActiveAsync()
            : await _machines.GetAllAsync();

        // One lookup for the whole list — never one query per machine.
        var states = await _status.GetStatesAsync(machines.Select(m => m.MachineId).ToList());

        IReadOnlyList<MachineResponse> response = machines
            .Select(m => ToResponse(m, states.TryGetValue(m.MachineId, out var state)
                ? state
                // A machine the status lookup has nothing for is Unknown, not missing from the list.
                : MachineRunningState.Unknown))
            .ToList();

        // An empty fleet is an empty list, never a 404.
        return ApiResult<IReadOnlyList<MachineResponse>>.Ok(response);
    }

    private static MachineResponse ToResponse(Machine machine, MachineRunningState state) => new()
    {
        // Present on purpose — see the remarks on MachineResponse. It is the value a caller sends
        // back as machineId when raising a work order.
        MachineId = machine.MachineId,

        MachineCode = machine.MachineCode,
        MachineName = machine.MachineName,
        MachineTypeId = machine.MachineTypeId,
        MachineTypeName = machine.MachineType?.Name,
        MachineTypeNameEnglish = machine.MachineType?.NameEnglish,
        ProductionLine = machine.MachineType?.ProductionLine,
        IsActive = machine.IsActive,

        // The stored administrator flag, kept distinct from the derived live state below.
        IsRunning = machine.IsRunning,

        // The name, not the enum's integer.
        RunningState = state.ToString(),

        CreatedAt = machine.CreatedAt
    };
}
