using System;

namespace SilkEngine.Core;

public class ConsoleLogWriter : ILogWriter
{
    public void Write(string message) => Console.WriteLine(message);
}
