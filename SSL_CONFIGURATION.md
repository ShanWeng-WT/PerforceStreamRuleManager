# SSL/TLS Configuration Guide

## Overview

The Perforce Stream Manager supports secure connections to Perforce servers using SSL/TLS. This document explains how to configure SSL connections and handle certificate validation.

## Basic SSL Connection

To connect to a Perforce server using SSL, prefix your server address with `ssl:`:

```
Server: ssl:perforce.example.com
Port: 1666
```

## Certificate Validation

The application uses P4API.NET for Perforce connectivity. By default, SSL connections use the Windows certificate store for certificate validation.

### Trusted Certificates (Signed by CA)

If your Perforce server uses a certificate signed by a trusted Certificate Authority (CA):
1. No additional configuration is needed
2. The connection will work automatically
3. The certificate is validated against the Windows certificate store

### Self-Signed Certificates

If your Perforce server uses a self-signed certificate, you have two options:

#### Option 1: Add to Windows Certificate Store (Recommended)

1. Obtain the server's certificate file (.cer or .crt)
2. Double-click the certificate file
3. Click "Install Certificate"
4. Select "Local Machine" (requires administrator privileges)
5. Place the certificate in "Trusted Root Certification Authorities"
6. Complete the wizard

#### Option 2: Use P4 Trust Command

1. Open a command prompt
2. Run: `p4 -p ssl:your-server:port trust -y`
3. This adds the server's fingerprint to P4's trust store
4. The application will honor this trust

Example:
```batch
p4 -p ssl:perforce.example.com:1666 trust -y
```

## Security Settings

The application includes the following security settings (in AppSettings):

- **ValidateSslCertificates** (default: true)
  - Whether to validate SSL/TLS certificates
  - It's strongly recommended to keep this enabled

- **TrustedCertificateFingerprints** (default: empty)
  - Reserved for future use
  - Will allow manual fingerprint validation when P4API.NET supports it

## Troubleshooting

### Connection Fails with SSL Error

If you get an SSL-related error when connecting:

1. **Verify the server address**: Ensure it starts with `ssl:`
2. **Check certificate trust**:
   - Run `p4 -p ssl:your-server:port info` to test the connection
   - If you get a fingerprint prompt, accept it with `p4 trust -y`
3. **Check certificate validity**: Ensure the server certificate hasn't expired
4. **Verify hostname**: The certificate's CN (Common Name) should match the server hostname

### Certificate Warnings

The application logs SSL connection information. Check the log file at:
```
%APPDATA%\PerforceStreamManager\application.log
```

Look for entries like:
```
INFO: SSL connection detected. Using system certificate store for validation.
```

## Future Enhancements

The development team is working on:
- Programmatic certificate fingerprint validation
- Certificate details viewer in the UI
- Import/export of trusted certificate fingerprints
- Per-connection certificate overrides

These features are pending availability of enhanced SSL APIs in P4API.NET.

## Security Best Practices

1. **Always use SSL** for production Perforce servers
2. **Use CA-signed certificates** when possible (Let's Encrypt is free)
3. **Keep ValidateSslCertificates enabled** to prevent MITM attacks
4. **Regularly update certificates** before they expire
5. **Review trusted certificates** periodically

## Additional Resources

- [Perforce SSL Documentation](https://www.perforce.com/manuals/p4sag/Content/P4SAG/ssl.overview.html)
- [P4API.NET Documentation](https://www.perforce.com/manuals/p4api.net/)
- [Windows Certificate Management](https://docs.microsoft.com/en-us/windows/win32/seccrypto/certificate-manager-tool)
