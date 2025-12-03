using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SecureBootDashboard.Api.Configuration;
using SecureBootDashboard.Api.Middleware;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace SecureBootDashboard.Api.Tests.Middleware
{
    public class ClientCertificateAuthenticationMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_WhenAuthenticationDisabled_ShouldCallNext()
        {
            // Arrange
            var options = new ClientCertificateAuthenticationOptions { Enabled = false };
            var optionsMock = new Mock<IOptions<ClientCertificateAuthenticationOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            var loggerMock = new Mock<ILogger<ClientCertificateAuthenticationMiddleware>>();
            var nextCalled = false;
            RequestDelegate next = (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new ClientCertificateAuthenticationMiddleware(next, loggerMock.Object, optionsMock.Object);
            var context = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled, "Next middleware should be called when authentication is disabled");
        }

        [Fact]
        public async Task InvokeAsync_WhenCertificateRequiredButNotProvided_ShouldReturn401()
        {
            // Arrange
            var options = new ClientCertificateAuthenticationOptions 
            { 
                Enabled = true,
                RequireClientCertificate = true 
            };
            var optionsMock = new Mock<IOptions<ClientCertificateAuthenticationOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            var loggerMock = new Mock<ILogger<ClientCertificateAuthenticationMiddleware>>();
            var nextCalled = false;
            RequestDelegate next = (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new ClientCertificateAuthenticationMiddleware(next, loggerMock.Object, optionsMock.Object);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.False(nextCalled, "Next middleware should not be called when certificate is required but not provided");
            Assert.Equal(401, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenCertificateNotRequiredAndNotProvided_ShouldCallNext()
        {
            // Arrange
            var options = new ClientCertificateAuthenticationOptions 
            { 
                Enabled = true,
                RequireClientCertificate = false 
            };
            var optionsMock = new Mock<IOptions<ClientCertificateAuthenticationOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            var loggerMock = new Mock<ILogger<ClientCertificateAuthenticationMiddleware>>();
            var nextCalled = false;
            RequestDelegate next = (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new ClientCertificateAuthenticationMiddleware(next, loggerMock.Object, optionsMock.Object);
            var context = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled, "Next middleware should be called when certificate is not required and not provided");
        }

        [Fact]
        public async Task InvokeAsync_WhenValidCertificateProvided_ShouldCallNext()
        {
            // Arrange
            var options = new ClientCertificateAuthenticationOptions 
            { 
                Enabled = true,
                RequireClientCertificate = true,
                ValidateValidityPeriod = true,
                ValidateCertificateChain = false
            };
            var optionsMock = new Mock<IOptions<ClientCertificateAuthenticationOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            var loggerMock = new Mock<ILogger<ClientCertificateAuthenticationMiddleware>>();
            var nextCalled = false;
            RequestDelegate next = (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            // Create a self-signed certificate for testing
            var cert = CreateSelfSignedCertificate();

            var middleware = new ClientCertificateAuthenticationMiddleware(next, loggerMock.Object, optionsMock.Object);
            var context = new DefaultHttpContext();
            context.Connection.ClientCertificate = cert;

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled, "Next middleware should be called when valid certificate is provided");
        }

        [Fact]
        public async Task InvokeAsync_WhenExpiredCertificateProvided_ShouldReturn401()
        {
            // Arrange
            var options = new ClientCertificateAuthenticationOptions 
            { 
                Enabled = true,
                RequireClientCertificate = true,
                ValidateValidityPeriod = true
            };
            var optionsMock = new Mock<IOptions<ClientCertificateAuthenticationOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            var loggerMock = new Mock<ILogger<ClientCertificateAuthenticationMiddleware>>();
            var nextCalled = false;
            RequestDelegate next = (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            // Create an expired certificate
            var cert = CreateSelfSignedCertificate(notBefore: DateTime.UtcNow.AddDays(-2), notAfter: DateTime.UtcNow.AddDays(-1));

            var middleware = new ClientCertificateAuthenticationMiddleware(next, loggerMock.Object, optionsMock.Object);
            var context = new DefaultHttpContext();
            context.Connection.ClientCertificate = cert;
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.False(nextCalled, "Next middleware should not be called when expired certificate is provided");
            Assert.Equal(401, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_WhenThumbprintNotInAllowedList_ShouldReturn401()
        {
            // Arrange
            var cert = CreateSelfSignedCertificate();
            var options = new ClientCertificateAuthenticationOptions 
            { 
                Enabled = true,
                RequireClientCertificate = true,
                ValidateValidityPeriod = false,
                ValidateCertificateChain = false,
                AllowedCertificateThumbprints = new List<string> { "1234567890ABCDEF" } // Different thumbprint
            };
            var optionsMock = new Mock<IOptions<ClientCertificateAuthenticationOptions>>();
            optionsMock.Setup(o => o.Value).Returns(options);

            var loggerMock = new Mock<ILogger<ClientCertificateAuthenticationMiddleware>>();
            var nextCalled = false;
            RequestDelegate next = (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new ClientCertificateAuthenticationMiddleware(next, loggerMock.Object, optionsMock.Object);
            var context = new DefaultHttpContext();
            context.Connection.ClientCertificate = cert;
            context.Response.Body = new MemoryStream();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.False(nextCalled, "Next middleware should not be called when thumbprint is not in allowed list");
            Assert.Equal(401, context.Response.StatusCode);
        }

        private static X509Certificate2 CreateSelfSignedCertificate(
            string subject = "CN=TestCertificate",
            DateTime? notBefore = null,
            DateTime? notAfter = null)
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var request = new CertificateRequest(
                subject,
                rsa,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            var cert = request.CreateSelfSigned(
                notBefore ?? DateTime.UtcNow.AddDays(-1),
                notAfter ?? DateTime.UtcNow.AddYears(1));

            return cert;
        }
    }
}
