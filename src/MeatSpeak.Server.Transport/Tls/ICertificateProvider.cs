namespace MeatSpeak.Server.Transport.Tls;

using System.Security.Cryptography.X509Certificates;

public interface ICertificateProvider
{
    X509Certificate2? GetCertificate();
}

public sealed class FileCertificateProvider : ICertificateProvider
{
    private readonly X509Certificate2? _certificate;

    public FileCertificateProvider(string certPath, string? keyPath = null, string? password = null)
    {
        if (keyPath != null)
        {
            // PEM cert + separate key file
            _certificate = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            // Export and re-import so the cert is usable with SslStream on all platforms
            var exported = _certificate.Export(X509ContentType.Pfx);
            _certificate.Dispose();
            _certificate = X509CertificateLoader.LoadPkcs12(exported, null);
        }
        else
        {
            // PFX file
            _certificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, password);
        }
    }

    public X509Certificate2? GetCertificate() => _certificate;
}
