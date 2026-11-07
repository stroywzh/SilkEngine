namespace SilkEngine;

/// <summary>How the engine treats the content bootstrap (scan, import, catalog) at startup.</summary>
public enum AssetStartupMode
{
    /// <summary>Log bootstrap failures and start anyway; individual loads may fail later.</summary>
    BestEffort = 0,

    /// <summary>Stop the engine and surface the failure when the content bootstrap fails.</summary>
    Strict = 1,
}
