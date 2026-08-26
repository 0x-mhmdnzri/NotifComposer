using Grpc.Core;
using IdentityService.Application.Services;
using IdentityService.Grpc;

namespace IdentityService.GrpcServices;

public class UserGrpcService : UserService.UserServiceBase
{
    private readonly UserAppService _userService;

    public UserGrpcService(UserAppService userService)
    {
        _userService = userService;
    }

    public override async Task<UserResponse> GetUserById(GetUserByIdRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user id"));

        var user = await _userService.GetByIdAsync(id, context.CancellationToken);
        if (user is null)
            throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        return new UserResponse
        {
            Id = user.Id.ToString(),
            FullName = user.FullName,
            Mobile = user.Mobile,
            IsActive = user.IsActive
        };
    }

    public override async Task<UserExistsResponse> UserExists(UserExistsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            return new UserExistsResponse { Exists = false, IsActive = false };

        var (exists, isActive) = await _userService.UserExistsAsync(id, context.CancellationToken);
        return new UserExistsResponse { Exists = exists, IsActive = isActive };
    }
}
