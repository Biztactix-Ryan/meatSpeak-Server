namespace MeatSpeak.Server.Tls;

using System.Security.Cryptography.X509Certificates;
using MeatSpeak.Server.Transport.Tls;

public sealed class AcmeCertificateProvider : ICertificateProvider
{
    private volatile X509Certificate2? _certificate;

    public X509Certificate2? GetCertificate() => _certificate;

    public void UpdateCertificate(X509Certificate2 certificate)
    {
        var old = _certificate;
        _certificate = certificate;
        old?.Dispose();
    }
}
