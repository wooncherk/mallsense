using MallSense.Application.DTOs;

namespace MallSense.Application.Interfaces;

public interface INavigationService
{
    Task<RouteResponse> CalculateRouteAsync(RouteRequest request, CancellationToken cancellationToken = default);
    Task<RouteOverviewResponse> GenerateRouteOverviewAsync(RouteRequest request, CancellationToken cancellationToken = default);
    Task<LocationRecoveryResponse> RecoverLocationAsync(LocationRecoveryRequest request, CancellationToken cancellationToken = default);
}
