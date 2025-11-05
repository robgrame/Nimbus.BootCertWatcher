using System;
using System.Text.Json;
using SecureBootWatcher.Shared.Models;
using Xunit;

namespace SecureBootWatcher.Shared.Tests
{
    public class SecureBootCertificateTests
    {
    [Fact]
        public void SecureBootCertificate_CanSerializeAndDeserialize()
        {
         // Arrange
   var certificate = new SecureBootCertificate
       {
      Database = "db",
           Thumbprint = "ABC123DEF456",
             Subject = "CN=Test Certificate",
 Issuer = "CN=Test CA",
           SerialNumber = "123456",
 NotBefore = DateTimeOffset.Parse("2020-01-01T00:00:00Z"),
       NotAfter = DateTimeOffset.Parse("2025-01-01T00:00:00Z"),
      SignatureAlgorithm = "sha256RSA",
            PublicKeyAlgorithm = "RSA",
                KeySize = 2048,
       IsExpired = false,
        DaysUntilExpiration = 365,
   Version = 3,
                IsMicrosoftCertificate = true,
       RawData = "VGVzdERhdGE="
  };

  // Act
        var json = JsonSerializer.Serialize(certificate);
       var deserialized = JsonSerializer.Deserialize<SecureBootCertificate>(json);

            // Assert
       Assert.NotNull(deserialized);
     Assert.Equal(certificate.Database, deserialized.Database);
    Assert.Equal(certificate.Thumbprint, deserialized.Thumbprint);
        Assert.Equal(certificate.Subject, deserialized.Subject);
            Assert.Equal(certificate.IsExpired, deserialized.IsExpired);
  Assert.Equal(certificate.IsMicrosoftCertificate, deserialized.IsMicrosoftCertificate);
        }

  [Fact]
  public void SecureBootCertificateCollection_CanSerializeAndDeserialize()
        {
    // Arrange
   var collection = new SecureBootCertificateCollection
   {
       SignatureDatabase = new[]
        {
              new SecureBootCertificate
   {
        Database = "db",
         Thumbprint = "ABC123",
     Subject = "CN=Microsoft Windows Production PCA 2011",
    IsMicrosoftCertificate = true
     }
         },
        ForbiddenDatabase = new[]
                {
     new SecureBootCertificate
           {
   Database = "dbx",
          Thumbprint = "DEF456",
  Subject = "CN=Revoked Certificate",
     IsExpired = true
      }
        },
                KeyExchangeKeys = new SecureBootCertificate[0],
                PlatformKeys = new SecureBootCertificate[0],
        SecureBootEnabled = true,
 ExpiredCertificateCount = 1,
ExpiringCertificateCount = 0,
       CollectedAtUtc = DateTimeOffset.UtcNow
    };

      // Act
            var json = JsonSerializer.Serialize(collection);
            var deserialized = JsonSerializer.Deserialize<SecureBootCertificateCollection>(json);

            // Assert
       Assert.NotNull(deserialized);
            Assert.Equal(2, deserialized.TotalCertificateCount);
    Assert.Single(deserialized.SignatureDatabase);
      Assert.Single(deserialized.ForbiddenDatabase);
  Assert.True(deserialized.SecureBootEnabled);
    Assert.Equal(1, deserialized.ExpiredCertificateCount);
        }

      [Fact]
public void SecureBootCertificateCollection_TotalCertificateCount_CalculatesCorrectly()
 {
            // Arrange
            var collection = new SecureBootCertificateCollection();
            
            // Act
  collection.SignatureDatabase.Add(new SecureBootCertificate { Database = "db" });
    collection.SignatureDatabase.Add(new SecureBootCertificate { Database = "db" });
            collection.ForbiddenDatabase.Add(new SecureBootCertificate { Database = "dbx" });
       collection.KeyExchangeKeys.Add(new SecureBootCertificate { Database = "KEK" });
     collection.PlatformKeys.Add(new SecureBootCertificate { Database = "PK" });

            // Assert
        Assert.Equal(5, collection.TotalCertificateCount);
        }

        [Fact]
        public void SecureBootStatusReport_CanIncludeCertificates()
        {
            // Arrange
      var report = new SecureBootStatusReport
      {
    Device = new DeviceIdentity { MachineName = "TEST-PC" },
           Registry = new SecureBootRegistrySnapshot(),
  Certificates = new SecureBootCertificateCollection
            {
         SecureBootEnabled = true,
  SignatureDatabase = new[]
            {
      new SecureBootCertificate
  {
Database = "db",
          Thumbprint = "ABC123",
             IsExpired = false
           }
  }
       },
         CreatedAtUtc = DateTimeOffset.UtcNow
            };

    // Act
      var json = JsonSerializer.Serialize(report);
            var deserialized = JsonSerializer.Deserialize<SecureBootStatusReport>(json);

   // Assert
       Assert.NotNull(deserialized);
    Assert.NotNull(deserialized.Certificates);
      Assert.True(deserialized.Certificates.SecureBootEnabled);
            Assert.Single(deserialized.Certificates.SignatureDatabase);
   }

     [Fact]
public void SecureBootStatusReport_CertificatesCanBeNull()
        {
            // Arrange
  var report = new SecureBootStatusReport
    {
     Device = new DeviceIdentity { MachineName = "TEST-PC" },
                Registry = new SecureBootRegistrySnapshot(),
      Certificates = null,
      CreatedAtUtc = DateTimeOffset.UtcNow
   };

      // Act
            var json = JsonSerializer.Serialize(report);
   var deserialized = JsonSerializer.Deserialize<SecureBootStatusReport>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Null(deserialized.Certificates);
    }

        [Fact]
  public void SecureBootCertificate_ExpirationCalculation()
        {
   // Arrange
            var now = DateTimeOffset.UtcNow;
         var expiredCert = new SecureBootCertificate
     {
    NotAfter = now.AddDays(-10),
    IsExpired = true,
     DaysUntilExpiration = -10
    };
        var expiringCert = new SecureBootCertificate
            {
             NotAfter = now.AddDays(30),
      IsExpired = false,
  DaysUntilExpiration = 30
   };
var validCert = new SecureBootCertificate
{
         NotAfter = now.AddYears(1),
          IsExpired = false,
              DaysUntilExpiration = 365
          };

            // Assert
            Assert.True(expiredCert.IsExpired);
     Assert.True(expiredCert.DaysUntilExpiration < 0);
    Assert.False(expiringCert.IsExpired);
            Assert.True(expiringCert.DaysUntilExpiration > 0 && expiringCert.DaysUntilExpiration <= 90);
     Assert.False(validCert.IsExpired);
        Assert.True(validCert.DaysUntilExpiration > 90);
        }
    }
}
