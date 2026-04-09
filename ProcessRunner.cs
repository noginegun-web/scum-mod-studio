using System.Diagnostics;

namespace ScumPakWizard;

internal static class ProcessRunner
{
    public static ProcessRunResult Run(
        string fileName,
        string arguments,
        string? workingDirectory,
        Action<string>? onStdOutLine,
        int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outTail = new TailBuffer(80);
        var errTail = new TailBuffer(80);

        process.Start();
        var errTask = Task.Run(() =>
        {
            string? line;
            while ((line = process.StandardError.ReadLine()) is not null)
            {
                errTail.Add(line);
            }
        });

        string? outLine;
        while ((outLine = process.StandardOutput.ReadLine()) is not null)
        {
            outTail.Add(outLine);
            onStdOutLine?.Invoke(outLine);
        }

        if (!process.WaitForExit(timeoutMs))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignored
            }

            throw new TimeoutException($"Время ожидания процесса истекло: {Path.GetFileName(fileName)}");
        }

        errTask.GetAwaiter().GetResult();
        return new ProcessRunResult(process.ExitCode, outTail.Joined(), errTail.Joined());
    }

    private sealed class TailBuffer
    {
        private readonly int _maxLines;
        private readonly Queue<string> _lines = new();

        public TailBuffer(int maxLines)
        {
            _maxLines = maxLines;
        }

        public void Add(string line)
        {
            _lines.Enqueue(line);
            while (_lines.Count > _maxLines)
            {
                _lines.Dequeue();
            }
        }

        public string Joined()
        {
            return string.Join(Environment.NewLine, _lines);
        }
    }
}
