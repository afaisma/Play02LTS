using System.Collections;
using System.Security.Cryptography.X509Certificates;
using UnityEngine.Networking;

public class AcceptAllCertificatesHandler : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        // Always return true to accept all certificates, including self-signed ones
        return true;
    }
}