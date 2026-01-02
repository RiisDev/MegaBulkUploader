namespace MegaBulkUploader.Modules.Output
{
    public class Log(string categoryName)
    {
        private readonly Lock _lock = new();

		public void LogWarning(string message)
		{
			lock (_lock)
			{
				Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
				Console.Write("[");
				Console.ForegroundColor = ConsoleColor.White;
				Console.Write(categoryName);
				Console.ResetColor();
				Console.Write("] ");
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.Write("info");
				Console.ResetColor();
				Console.WriteLine($": {message}");
			}
		}

		public void LogInformation(string message)
        {
            lock (_lock)
            {
                Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(categoryName);
                Console.ResetColor();
                Console.Write("] ");
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("info");
                Console.ResetColor();
                Console.WriteLine($": {message}");
            }
        }

        public void LogError(string message)
        {
            lock (_lock)
            {
                Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                Console.Write("[");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(categoryName);
                Console.ResetColor();
                Console.Write("] ");
                if (OperatingSystem.IsLinux())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }
                else
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.Black;
                }
                Console.Write("fail");
                Console.ResetColor();
                Console.WriteLine($": {message}");
            }
        }

    }
}
