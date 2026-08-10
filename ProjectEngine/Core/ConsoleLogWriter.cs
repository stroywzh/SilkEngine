using System;

namespace ProjectEngine;

public class ConsoleLogWriter : ILogWriter
{
    public void Write(string message) => Console.WriteLine(message);
}
