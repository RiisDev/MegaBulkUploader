using MegaBulkUploader.Modules.Clients;

namespace MegaBulkUploader.Modules.Output
{
    // Credit to https://gist.github.com/DanielSWolf/0ab6a96899cc5377bf54

    public class ProgressBar : IDisposable, IProgress<(MegaCliWrapper.TransferData value, int uploaded, int count)>
    {
        private string _reportText = "";
        internal bool FailedStatus;
        private const int BlockCount = 50;
        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string Animation = @"|/-\";

        private readonly Timer _timer;
        private readonly Lock _lock = new();

        private double _currentProgress;
        private string _currentText = string.Empty;
        private bool _disposed;
        private int _animationIndex;

        public ProgressBar()
        {
            _timer = new Timer(TimerHandler!);

            if (!Console.IsOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Report((MegaCliWrapper.TransferData value, int uploaded, int count) progress)
        {
            Interlocked.Exchange(ref _currentProgress, progress.value.Progress);

            string fileName = Path.GetFileName(progress.value.Source);

            _reportText =
                $"Uploading file {progress.uploaded} of {progress.count}\n" +
                $"Source     : {fileName}\n" +
                $"Size       : {progress.value.Size}\n" +
                $"Destination: {progress.value.Destination}";
        }

        private void TimerHandler(object state)
        {
            lock (_lock)
            {
                if (_disposed) return;

                ConsoleColor oldColor = Console.ForegroundColor;

                int progressBlockCount = (int)(Math.Max(0, Math.Min(1, _currentProgress / 100)) * BlockCount);
                string text = $"[{GetProgressBarWithUnicode(progressBlockCount)}] {_currentProgress}% {Animation[_animationIndex++ % Animation.Length]} | {_reportText}";
                Console.ForegroundColor = _currentProgress switch
                {
                    < 10.0 => ConsoleColor.DarkRed,
                    < 25.0 => ConsoleColor.Red,
                    < 40.0 => ConsoleColor.DarkYellow,
                    < 60.0 => ConsoleColor.Yellow,
                    < 80.0 => ConsoleColor.DarkGreen,
                    _ => ConsoleColor.Green,
                }; 
                UpdateText(text);
                Console.ForegroundColor = oldColor;
                ResetTimer();
            }
        }

        private string GetProgressBarWithUnicode(int filledBlocks)
        {
            int halfBlockCount = 0;

            if (filledBlocks < BlockCount)
            {
                halfBlockCount = (int)((Math.Max(0, Math.Min(1, _currentProgress / 100)) * BlockCount - filledBlocks) * 2);
            }

            string progress = new ('\u2588', filledBlocks);

            if (halfBlockCount > 0)
            {
                progress += new string('\u2592', 1);
                halfBlockCount--;
            }

            progress += new string('\u2591', BlockCount - filledBlocks - halfBlockCount);

            return progress;
        }

        private void UpdateText(string text)
        {
            lock (_lock)
            {
	            Console.CursorVisible = false;

                int oldLines = _currentText.Split('\n').Length;

                for (int i = 0; i < oldLines; i++)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                }

                Console.WriteLine(text);
                _currentText = text;

                Console.CursorVisible = true;
            }
        }


        private void ResetTimer()
        {
            _timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
        }

        private void SetDone()
        {
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            UpdateText(string.Empty);
            UpdateText($"[{new string('\u2588', BlockCount)}] 100% | Completed\n");
            Console.ForegroundColor = oldColor;
        }

        public void DoFailed(string errorMessage)
        {
            FailedStatus = true;
            ConsoleColor oldColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            UpdateText($"Failed: {errorMessage}");
            Console.ForegroundColor = oldColor;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                _disposed = true;
                if (!FailedStatus)
                    SetDone();
            }
        }
    }

}
