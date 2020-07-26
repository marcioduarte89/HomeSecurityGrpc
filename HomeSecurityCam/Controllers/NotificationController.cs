using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using HomeSecurityServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace HomeSecurityCam.Controllers 
{
    [ApiController]
    [Route("[controller]")]
    public class NotificationController : ControllerBase 
    {

        private readonly ILogger<NotificationController> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private NotificationDispatcherService.NotificationDispatcherServiceClient _notificationClient;

        public NotificationController(ILogger<NotificationController> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        protected NotificationDispatcherService.NotificationDispatcherServiceClient NotificationClient
        {
            get
            {
                if (_notificationClient == null)
                {

                    var httpHandler = new HttpClientHandler();
                    // Return `true` to allow certificates that are untrusted/invalid
                    httpHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    var channel = GrpcChannel.ForAddress("https://localhost:5001", new GrpcChannelOptions(){ HttpHandler = httpHandler });
                    
                    _notificationClient = new NotificationDispatcherService.NotificationDispatcherServiceClient(channel);
                }

                return _notificationClient;
            }
        }

        [HttpGet]
        public async Task<IActionResult> Get(int coordinateX, int coordinateY)
        {
            var token = _httpContextAccessor.HttpContext.Request.Headers["Authorization"].ToString();
            var deviceIdHeader = _httpContextAccessor.HttpContext.Request.Headers["deviceId"].ToString();

            if (!int.TryParse(deviceIdHeader, out var deviceId))
            {
                return BadRequest();
            }

            var notification = new NotificationMessage()
            {
                CoordinateX = coordinateX,
                CoordinateY = coordinateY,
                DeviceId = deviceId,
                Notification = "Image binary",
                NotificationTime = Timestamp.FromDateTime(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc))
            };

            var result = await NotificationClient.AddNotificationAsync(notification, new Metadata {{ "Authorization", token }});

            return Ok(result.Message);
        }

        [HttpGet("diagnostic")]
        public async Task<string> SendDiagnostic()
        {
            using (var sendDiagnostic = NotificationClient.SendDiagnostics())
            {
                Task.Run(async () =>
                {
                    while (await sendDiagnostic.ResponseStream.MoveNext())
                    {
                        var message = sendDiagnostic.ResponseStream.Current.Message;

                        _logger.LogInformation($"Diagnostics sent back from the server: { message }");
                    }
                });

                for (int i = 0; i < 5; i++) {
                    await sendDiagnostic.RequestStream.WriteAsync(new DeviceStatus() {
                        DeviceId = 1,
                        IsActive = true,
                        StatusTime = Timestamp.FromDateTime(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Utc))
                    });
                    await Task.Delay(2000);
                }

                await sendDiagnostic.RequestStream.CompleteAsync();
            }

            return "Diagnostic sent";
        }

        [HttpGet("token")]
        public async Task<string> CreateToken()
        {
            var result = await NotificationClient.CreateTokenAsync(new TokenRequest() 
            {
                Password = "somePassword",
                Username = "user1"
            });

            return result.Token;
        }
    }
}
