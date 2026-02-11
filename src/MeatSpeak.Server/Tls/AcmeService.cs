namespace MeatSpeak.Server.Tls;

using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using MeatSpeak.Server.Core.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class AcmeService : BackgroundService
{
    private readonly TlsConfig _tlsConfig;
    private readonly AcmeCertificateProvider _certProvider;
    private readonly IAcmeChallengeHandler _challengeHandler;
    private readonly ILogger<AcmeService> _logger;
    private readonly string _certsDir;
    private readonly string _accountKeyPath;

    private static readonly TimeSpan RenewalCheckInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan RenewalThreshold = TimeSpan.FromDays(30);

    public AcmeService(
        ServerConfig config,
        AcmeCertificateProvider certProvider,
        IAcmeChallengeHandler challengeHandler,
        ILogger<AcmeService> logger)
    {
        _tlsConfig = config.Tls;
        _certProvider = certProvider;
        _challengeHandler = challengeHandler;
        _logger = logger;
        _certsDir = Path.Combine(AppContext.BaseDirectory, "certs");
        _accountKeyPath = Path.Combine(_certsDir, "acme-account.pem");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        System.IO.Directory.CreateDirectory(_certsDir);

        // Try loading existing cert
        var pfxPath = Path.Combine(_certsDir, "acme-cert.pfx");
        if (File.Exists(pfxPath))
        {
            try
            {
                var existing = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, null);
                if (existing.NotAfter > DateTime.UtcNow.Add(RenewalThreshold))
                {
                    _certProvider.UpdateCertificate(existing);
                    _logger.LogInformation(
                        "Loaded existing ACME certificate, valid until {Expiry}", existing.NotAfter);
                }
                else
                {
                    existing.Dispose();
                    _logger.LogInformation("Existing ACME certificate expires soon, will renew");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load existing ACME certificate, will request new one");
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentCert = _certProvider.GetCertificate();
            if (currentCert == null || currentCert.NotAfter <= DateTime.UtcNow.Add(RenewalThreshold))
            {
                try
                {
                    await RequestCertificateAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ACME certificate request failed, will retry");
                }
            }

            try
            {
                await Task.Delay(RenewalCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RequestCertificateAsync(CancellationToken ct)
    {
        var acme = await GetOrCreateAcmeContextAsync();
        var domains = _tlsConfig.AcmeDomains;

        _logger.LogInformation("Requesting ACME certificate for domains: {Domains}", string.Join(", ", domains));

        var order = await acme.NewOrder(domains);
        var authorizations = await order.Authorizations();

        foreach (var authz in authorizations)
        {
            var authzResource = await authz.Resource();
            var domain = authzResource.Identifier.Value;

            if (_tlsConfig.AcmeChallengeType == "Dns01")
            {
                var dnsChallenge = await authz.Dns();
                var dnsTxt = acme.AccountKey.DnsTxt(dnsChallenge.Token);
                await _challengeHandler.PrepareAsync(domain, dnsChallenge.Token, dnsTxt);

                // Wait for DNS propagation
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                await dnsChallenge.Validate();
            }
            else
            {
                var httpChallenge = await authz.Http();
                var keyAuthz = httpChallenge.KeyAuthz;
                await _challengeHandler.PrepareAsync(domain, httpChallenge.Token, keyAuthz);

                await httpChallenge.Validate();
            }
        }

        // Wait for validation
        var orderResource = await WaitForOrderReadyAsync(order, ct);

        if (orderResource.Status == OrderStatus.Ready)
        {
            // Generate CSR and finalize
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo
            {
                CommonName = domains[0],
            }, privateKey);

            var pfxBytes = cert.ToPfx(privateKey).Build("acme-cert", string.Empty);
            var pfxPath = Path.Combine(_certsDir, "acme-cert.pfx");
            await File.WriteAllBytesAsync(pfxPath, pfxBytes, ct);

            var x509 = X509CertificateLoader.LoadPkcs12(pfxBytes, null);
            _certProvider.UpdateCertificate(x509);

            _logger.LogInformation(
                "ACME certificate provisioned, valid until {Expiry}", x509.NotAfter);
        }
        else
        {
            _logger.LogError("ACME order failed with status: {Status}", orderResource.Status);
        }

        // Cleanup challenges
        var auths = await order.Authorizations();
        foreach (var authz in auths)
        {
            var res = await authz.Resource();
            var domain = res.Identifier.Value;

            if (_tlsConfig.AcmeChallengeType == "Dns01")
            {
                var dnsChallenge = await authz.Dns();
                await _challengeHandler.CleanupAsync(domain, dnsChallenge.Token);
            }
            else
            {
                var httpChallenge = await authz.Http();
                await _challengeHandler.CleanupAsync(domain, httpChallenge.Token);
            }
        }
    }

    private async Task<AcmeContext> GetOrCreateAcmeContextAsync()
    {
        var directoryUri = _tlsConfig.AcmeStaging
            ? WellKnownServers.LetsEncryptStagingV2
            : WellKnownServers.LetsEncryptV2;

        if (File.Exists(_accountKeyPath))
        {
            var pemKey = await File.ReadAllTextAsync(_accountKeyPath);
            var accountKey = KeyFactory.FromPem(pemKey);
            var acme = new AcmeContext(directoryUri, accountKey);
            await acme.Account();
            _logger.LogInformation("Loaded existing ACME account");
            return acme;
        }

        var newAcme = new AcmeContext(directoryUri);
        await newAcme.NewAccount(_tlsConfig.AcmeEmail, termsOfServiceAgreed: true);

        var pem = newAcme.AccountKey.ToPem();
        await File.WriteAllTextAsync(_accountKeyPath, pem);
        _logger.LogInformation("Created new ACME account");

        return newAcme;
    }

    private static async Task<Order> WaitForOrderReadyAsync(IOrderContext order, CancellationToken ct)
    {
        for (int i = 0; i < 30; i++)
        {
            var resource = await order.Resource();
            if (resource.Status == OrderStatus.Ready || resource.Status == OrderStatus.Valid)
                return resource;
            if (resource.Status == OrderStatus.Invalid)
                return resource;

            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }

        return await order.Resource();
    }
}
