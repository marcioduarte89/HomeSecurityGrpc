using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.Tasks;

namespace HomeSecurityServer.Services {

    public class NotificationService : NotificationDispatcherService.NotificationDispatcherServiceBase 
    {
        private readonly ILogger<NotificationService> logger;
        private readonly IConfiguration configuration;

        public NotificationService(ILogger<NotificationService> logger, IConfiguration configuration) {
            this.logger = logger;
            this.configuration = configuration;
        }

        public override Task<StatusMessage> AddNotification(NotificationMessage request, ServerCallContext context) 
        {
            logger.LogInformation("Notification received");
            logger.LogInformation($"Device Id: {request.DeviceId}");
            logger.LogInformation($"Motion detected on coordinate X: {request.CoordinateX} and Y: {request.CoordinateX}");
            logger.LogInformation($"\"Image\" binary: {request.Notification}");
            logger.LogInformation($"Notification time: {request.NotificationTime}");

            return Task.FromResult(new StatusMessage() 
            {
                Message = "Notification received"
            });
        }

        public override async Task SendDiagnostics(IAsyncStreamReader<DeviceStatus> requestStream, IServerStreamWriter<StatusMessage> responseStream, ServerCallContext context) 
        {
            await Task.Run(async () => 
            {
                while (await requestStream.MoveNext())
                {
                    var statusMessage = requestStream.Current;
                    logger.LogInformation($"Diagnostics received with device id: { statusMessage.DeviceId}, status: {statusMessage.IsActive}, time: {statusMessage.StatusTime} ");

                    await responseStream.WriteAsync(new StatusMessage() {
                        Message = "Message back to the client"
                    });
                }
            });
        }

        [AllowAnonymous]
        public override async Task<TokenResponse> CreateToken(TokenRequest request, ServerCallContext context)
        {
            // Just to illustrate authentication and creation of a valid jwt token - don't do this in prod (not even in dev!)
            if (this.configuration.GetValue<string>("Credentials:Username").Equals(request.Username) &&
                this.configuration.GetValue<string>("Credentials:Password").Equals(request.Password)) 
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateJwtSecurityToken(signingCredentials:
                    new SigningCredentials(
                        new SymmetricSecurityKey(
                            Encoding.Default.GetBytes(this.configuration.GetValue<string>("Credentials:PrivateKey"))),
                        SecurityAlgorithms.HmacSha256Signature));
                return new TokenResponse() 
                {
                    Expiration = Timestamp.FromDateTime(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc)),
                    Success = true,
                    Token = tokenHandler.WriteToken(token)
                };
            }

            return await Task.FromResult<TokenResponse>(null);
        }
    }
}
