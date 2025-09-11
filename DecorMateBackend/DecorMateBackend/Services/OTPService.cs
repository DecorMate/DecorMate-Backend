namespace DecorMate_Backend_Web_app.Services
{
    public class OTPService
    {
        public static string GenerateOtp(int length = 6)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var data = new byte[length];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(data);
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                var idx = data[i] % chars.Length;
                result[i] = chars[idx];
            }
            return new string(result);
        }
    }
}
