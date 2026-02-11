using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using MeatSpeak.Server.Tls;
using Xunit;

namespace MeatSpeak.Server.Tests;

public class AcmeCertificateProviderTests
{
    private static X509Certificate2 CreateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var pfx = cert.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, null);
    }

    [Fact]
    public void GetCertificate_InitiallyReturnsNull()
    {
        var provider = new AcmeCertificateProvider();
        Assert.Null(provider.GetCertificate());
    }

    [Fact]
    public void UpdateCertificate_SetsCertificate()
    {
        var provider = new AcmeCertificateProvider();
        using var cert = CreateSelfSignedCert();

        provider.UpdateCertificate(cert);

        var result = provider.GetCertificate();
        Assert.NotNull(result);
        Assert.Equal("CN=test", result.Subject);
    }

    [Fact]
    public void UpdateCertificate_ReplacesPreviousCert()
    {
        var provider = new AcmeCertificateProvider();

        using var rsa1 = RSA.Create(2048);
        var req1 = new CertificateRequest("CN=first", rsa1, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert1 = req1.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var pfx1 = cert1.Export(X509ContentType.Pfx);
        var first = X509CertificateLoader.LoadPkcs12(pfx1, null);

        using var rsa2 = RSA.Create(2048);
        var req2 = new CertificateRequest("CN=second", rsa2, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var cert2 = req2.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
        var pfx2 = cert2.Export(X509ContentType.Pfx);
        var second = X509CertificateLoader.LoadPkcs12(pfx2, null);

        provider.UpdateCertificate(first);
        Assert.Equal("CN=first", provider.GetCertificate()!.Subject);

        provider.UpdateCertificate(second);
        Assert.Equal("CN=second", provider.GetCertificate()!.Subject);
    }
}
