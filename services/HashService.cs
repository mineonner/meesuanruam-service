using Microsoft.IdentityModel.Tokens;
using meesuanruam_service.model.request;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace meesuanruam_service.services
{
    public class HashService
    {
        private const int SaltSize = 24;
        private const int HashSize = 32;
        private const int Interrations = 10000;
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

        private IConfiguration _config;

        public HashService(IConfiguration config)
        {
            _config = config;
        }

        public string Hash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Interrations, Algorithm, HashSize);
            return $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";
        }

        public bool Verify(string password, string passwordHash)
        {
            string[] parts = passwordHash.Split('-');
            byte[] hash = Convert.FromHexString(parts[0]);
            byte[] salt = Convert.FromHexString(parts[1]);

            byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Interrations, Algorithm, HashSize);

            return CryptographicOperations.FixedTimeEquals(hash, inputHash);
        }

        public string createJwtToken(UserModel userInfo)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("email", userInfo.user_email),
                new Claim("org_unit_code", userInfo.org_unit_code),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(_config["Jwt:Issuer"],
                _config["Jwt:Issuer"],
                claims,
                expires: DateTime.Now.AddMinutes(480),
                signingCredentials: credentials
                );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// สร้าง token สำหรับฝังใน URL ดาวน์โหลดไฟล์แนบ
        /// จำเป็นเพราะ frontend เปิดไฟล์ด้วย &lt;a href&gt; ซึ่งแนบ Authorization header ไม่ได้
        ///
        /// อายุเท่ากับ JWT ของ session (480 นาที) ตั้งสั้นกว่านี้ไม่ได้เพิ่มความปลอดภัยจริง
        /// เพราะผู้ใช้แค่รีเฟรชหน้าก็ได้ลิงก์ใหม่ แต่ทำให้หน้าแบบประเมินที่คนกรอกนานเป็นชั่วโมง
        /// กดโหลดไฟล์แล้วได้ 401 ทั้งที่ยังล็อกอินอยู่
        /// </summary>
        public string createFileToken(long fileId, string orgUnitCode, string kind)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(_config["Jwt:Issuer"], _config["Jwt:Issuer"],
                new[]
                {
                    new Claim("fid", fileId.ToString()),
                    new Claim("org_unit_code", orgUnitCode),
                    // แยกว่า id นี้อยู่ตาราง FILE หรือ PROJECT_FILE กัน token ข้ามตารางกัน
                    new Claim("kind", kind),
                },
                expires: DateTime.UtcNow.AddMinutes(480),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>ตรวจลายเซ็นและวันหมดอายุ คืน null ถ้าใช้ไม่ได้</summary>
        public (long fileId, string orgUnitCode, string kind)? readFileToken(string token)
        {
            try
            {
                var parameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _config["Jwt:Issuer"],
                    ValidAudience = _config["Jwt:Issuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"])),
                    ClockSkew = TimeSpan.Zero
                };

                new JwtSecurityTokenHandler().ValidateToken(token, parameters, out SecurityToken validated);
                var payload = ((JwtSecurityToken)validated).Payload;

                return (Convert.ToInt64(payload["fid"]), (string)payload["org_unit_code"], (string)payload["kind"]);
            }
            catch
            {
                return null;
            }
        }

        public UserModel DecodingJwtToken(string token)
        {
            var payload = new JwtSecurityTokenHandler().ReadJwtToken(token).Payload;
            UserModel result = new UserModel()
            {
                user_email = (string)payload["email"],
                org_unit_code = payload.TryGetValue("org_unit_code", out var org) ? (string)org : null
            };

            return result;
        }

        public static string AesEncryptString(string key, string plainText)
        {
            byte[] iv = new byte[16];
            byte[] array;

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }

                        array = memoryStream.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(array);
        }

        public static string AesDecryptString(string key, string cipherText)
        {
            byte[] iv = new byte[16];
            byte[] buffer = Convert.FromBase64String(cipherText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(key);
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream memoryStream = new MemoryStream(buffer))
                {
                    using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }


    }
}
