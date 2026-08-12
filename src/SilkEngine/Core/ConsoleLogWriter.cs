using System;

namespace SilkEngine;

public class ConsoleLogWriter : ILogWriter
{
    public void Write(string message) => Console.WriteLine(message);
}
