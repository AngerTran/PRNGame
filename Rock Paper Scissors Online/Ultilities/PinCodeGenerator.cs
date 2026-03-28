namespace Rock_Paper_Scissors_Online.Ultilities
{
    public static class PinCodeGenerator
    {
        private static readonly Random _random = new Random();
        private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string GeneratePinCode(int length = 6)
        {
            var buffer = new char[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = Chars[_random.Next(Chars.Length)];
            }
            return new string(buffer);
        }
    }
}
