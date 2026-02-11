using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MeatSpeak.Server.Transport.Tls;
using Xunit;

namespace MeatSpeak.Server.Tests;

public class FileCertificateProviderTests
{
    [Fact]
    public void LoadsFromPfxFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Create a self-signed cert and save as PFX
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=pfxtest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
            var pfxBytes = cert.Export(X509ContentType.Pfx, "testpass");
            File.WriteAllBytes(tempFile, pfxBytes);

            var provider = new FileCertificateProvider(tempFile, password: "testpass");
            var loaded = provider.GetCertificate();

            Assert.NotNull(loaded);
            Assert.Equal("CN=pfxtest", loaded.Subject);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadsFromPemFiles()
    {
        var certFile = Path.GetTempFileName();
        var keyFile = Path.GetTempFileName();
        try
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest("CN=pemtest", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

            File.WriteAllText(certFile, cert.ExportCertificatePem());
            File.WriteAllText(keyFile, rsa.ExportRSAPrivateKeyPem());

            var provider = new FileCertificateProvider(certFile, keyPath: keyFile);
            var loaded = provider.GetCertificate();

            Assert.NotNull(loaded);
            Assert.Equal("CN=pemtest", loaded.Subject);
            Assert.True(loaded.HasPrivateKey);
        }
        finally
        {
            File.Delete(certFile);
            File.Delete(keyFile);
        }
    }
}
