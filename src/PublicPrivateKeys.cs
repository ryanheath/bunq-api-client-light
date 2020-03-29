using System;
using System.Security.Cryptography;
using System.Text;

namespace BunqClientLight
{
    public class SignSignature
    {
        private readonly RSA rsaPrivate;
        private readonly RSA rsaPublic;

        public SignSignature(string privatePem, string publicPem)
        {
            rsaPrivate = PublicPrivateKeys.CreateRSAFromPrivateKey(privatePem);
            rsaPublic = PublicPrivateKeys.CreateRSAFromPublicKey(publicPem);
        }

        public string SignData(byte[] bytes)
        {
            var signature = rsaPrivate.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return Convert.ToBase64String(signature);
        }

        public bool VerifyData(byte[] bytes, string signature)
        {
            var signatureBytes = Convert.FromBase64String(signature);
            return rsaPublic.VerifyData(bytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    public static class PublicPrivateKeys
    {
        const int KeySize = 2048;

        public static (string publicPem, string privatePem) Initialize()
        {
            var rsa = RSA.Create(KeySize);
            var publicPem = MakePem(rsa.ExportSubjectPublicKeyInfo(), "PUBLIC KEY");
            var privatePem = MakePem(rsa.ExportRSAPrivateKey(), "RSA PRIVATE KEY");

            return (publicPem, privatePem);
        }

        public static RSA CreateRSAFromPublicKey(string publicPem)
        {
            var rsa = RSA.Create(KeySize);
            rsa.ImportSubjectPublicKeyInfo(PemToBer(publicPem, "PUBLIC KEY"), out _);
            return rsa;
        }

        public static RSA CreateRSAFromPrivateKey(string privatePem)
        {
            var rsa = RSA.Create(KeySize);
            rsa.ImportRSAPrivateKey(PemToBer(privatePem, "RSA PRIVATE KEY"), out _);
            return rsa;
        }

        private static byte[] PemToBer(string pem, string header)
        {
            var begin = $"-----BEGIN {header}-----";
            var end = $"-----END {header}-----";

            int beginIdx = pem.IndexOf(begin);
            int base64Start = beginIdx + begin.Length;
            int endIdx = pem.IndexOf(end, base64Start);

            return Convert.FromBase64String(pem.Substring(base64Start, endIdx - base64Start));
        }
        private static string MakePem(byte[] ber, string header)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"-----BEGIN {header}-----");

            var base64 = Convert.ToBase64String(ber);

            for (var offset = 0; offset < base64.Length; offset += 64)
            {
                var lineEnd = Math.Min(offset + 64, base64.Length);
                builder.Append(base64, offset, lineEnd - offset).AppendLine();
            }

            builder.AppendLine($"-----END {header}-----");
            return builder.ToString();
        }
    }
}