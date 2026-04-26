using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Roblox.Dto.Users;

namespace Roblox.Services
{
    public class RSASignService : ServiceBase, IService
    {
        public string SignScript(string script, bool is2048 = false, bool is2014 = false)
        {
            if (is2014)
            {
                const string pKey1024 = "MIICXAIBAAKBgQDCgU5llUKmhPCApGvU3iGKszvrcuiKQ/5+mg1nCwXoYvVAuED5Y+e3xJ9NczGO0mrePxVndpzvSB5IYrURuZmcrIU6PNiLwLzoxkGEf1xWypcSM4Lb5xfMuUsNaH+jaOzVsFy/IFMmazYyDkNQmhzBrtVjHZn/+hh8oriDTVv8RQIDAQABAoGAZBCJ+JDVfT2fTU9JenXc47JZ/UNchuV8JD2073IoU+m1Ktqf8q2HJG+vVPUSZduyxyvFIzlOe3uquKqvZLMLZz1fgHcYxJUpeAY5F5DRUG2ZbQpWrWRVvH7w1B5B4+GjxeFcFSouDuXQzye4MbFOi8d+zL56Zag8AvcDxM7wB3ECQQDZ0Hj2PumLfAqE3yVf+KmF6F4dqpJbvMwXNvYQc6/fYc3lYfTqxTyPDDlgu69GoAzDICf614ybsmXbDgiIiBoPAkEA5Jq2eWbqeiZ/MgnLkGy/lygrNBjMTOsrPSzChhMKvpq8btk128CQkbndCTIIdK5iz78pHano2v/tBtkSDu1oawJANubERpVO+riWUi2I1yrvV/BdIK8o2vS4oLVayoTOdMjLRCEvwalbfVcAc3B7Wprm/JvzV9fS+j+6Sr+7yOY9YwJBAMFglmcPxd1aX1JmssoTE+a71gAV0gxnCoaPLGXaCca+ghOKrmKb/C8peG7k4f5B3dg6rn8nUZCf2VNnoDz8Ws0CQBYF5vXDmkw++SG0g2FXheCgjR/VjUnDCjWYwiLtLIukL9McZAivER4V9YgvfKsf8LrTIzG0WNhzp6I31X/H7zE=";
                using RSA rsa = RSA.Create();
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(pKey1024), out _);
                byte[] sigBytes = rsa.SignData(Encoding.UTF8.GetBytes(script), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                
                return $"--rbxsig%{Convert.ToBase64String(sigBytes)}%";
            }

            var signature = SignData("\r\n" + script, is2048);
            return is2048 ? $"--rbxsig2%{signature}%" : $"--rbxsig%{signature}%";
        }

        public string SignData(string data, bool is2048 = false)
        {
            try
            {
                string Key = is2048 ? 
                    Path.Combine("RSA", "PrivateKey2020.pem") : 
                    Path.Combine("RSA", "PrivateKey.pem");
                
                if (!File.Exists(Key))
                {
                    throw new FileNotFoundException($"Private key not found!");
                }

                string PrivateKey = File.ReadAllText(Key);
                using RSA rsa = RSA.Create();
                rsa.ImportFromPem(PrivateKey.ToCharArray());

                byte[] DataBytes = Encoding.UTF8.GetBytes(data);
                byte[] SigBytes = rsa.SignData(DataBytes, HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
                
                return Convert.ToBase64String(SigBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RSA signing error: {ex.Message}");
                throw;
            }
        }

        public string GenerateClientTicket2020(UserInfo user, string membershipType, int accountAgeDays, string jobId, long placeId)
        {
            var DateTimeStr = DateTime.Now.ToString("M/d/yyyy h:mm:ss tt");
            var CharApp = $"{Configuration.BaseUrl}/v1.1/avatar-fetch?userId={user.userId}&placeId={placeId}";

            var FirstUnsigned = $"{user.userId}\n{user.username}\n{CharApp}\n{jobId}\n{DateTimeStr}";
            var FirstSigned = SignData(FirstUnsigned, true);

            var countryCode = "US";
            
            var SecondUnsigned = $"{DateTimeStr}\n" +
                $"{jobId}\n" +
                $"{user.userId}\n" +
                $"{user.userId}\n" +
                $"0\n" +
                $"{accountAgeDays}\n" +
                $"f\n" +
                $"{user.username.Length}\n" +
                $"{user.username}\n" +
                $"{membershipType.Length}\n" +
                $"{membershipType}\n" +
                $"{countryCode.Length}\n" +
                $"{countryCode}\n" +
                $"0\n\n" +
                $"{user.username.Length}\n" +
                $"{user.username}";

            var SecondSigned = SignData(SecondUnsigned, true);

            return $"{DateTimeStr};{FirstSigned};{SecondSigned};4";
        }
        public bool IsThreadSafe() => true;
        public bool IsReusable() => false;
    }
}
