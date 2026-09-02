using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SilkEngine.Assets.Database;

/// <summary>
/// 构建产物磁盘缓存：按 BuildKey 原子写入/校验读取序列化派生字节。
/// 文件布局（小端）：magic + 格式版本 + BuildKey 长度 + BuildKey + 数据长度 + 数据 + SHA-256 摘要；
/// 任何校验不符或文件缺失均视为 cache miss，且只读写 cache 目录，绝不改动源资产。
/// 写入语义：先写 <c>&lt;key&gt;.bin.tmp</c> 并 flush 落盘，再原子替换为 <c>&lt;key&gt;.bin</c>（同卷原子替换），
/// 失败时清理临时文件，不留半成品。cache 目录在构造时自动创建。
/// </summary>
public sealed class BuildArtifactStore
{
    private const string BinaryExtension = ".bin";
    private const string TempExtension = ".bin.tmp";
    private const int FormatVersion = 1;
    private const int DigestSize = 32;

    // 定长头：magic(4) + 格式版本(4) + BuildKey 长度(4) + 数据长度(8)
    private const int FixedHeaderSize = 4 + 4 + 4 + 8;

    /// <summary>文件 magic（识别构建产物文件）</summary>
    private static ReadOnlySpan<byte> Magic => "SKAB"u8;

    private readonly string _cacheDirectory;

    /// <summary>创建构建产物缓存存储（cache 目录自动创建）</summary>
    /// <param name="cacheDirectory">缓存目录（完整物理路径）</param>
    /// <exception cref="ArgumentException">cacheDirectory 为 null/空白时抛出</exception>
    public BuildArtifactStore(string cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
            throw new ArgumentException("缓存目录不能为空或空白。", nameof(cacheDirectory));
        _cacheDirectory = Path.GetFullPath(cacheDirectory);
        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// 原子写入构建产物：先写临时文件并 flush 落盘，再原子替换为正式文件；
    /// 失败时清理临时文件并重抛，不留下半成品。
    /// </summary>
    /// <param name="buildKey">构建键（空/空白抛 <see cref="ArgumentException"/>；同时决定缓存文件名）</param>
    /// <param name="bytes">序列化派生字节</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>写入完成的 Task</returns>
    public async Task SaveAsync(string buildKey, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildKey);
        var targetPath = Path.Combine(_cacheDirectory, buildKey + BinaryExtension);
        var tempPath = Path.Combine(_cacheDirectory, buildKey + TempExtension);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await WriteAsync(stream, hash, Magic.ToArray(), cancellationToken).ConfigureAwait(false);
                await WriteInt32Async(stream, hash, FormatVersion, cancellationToken).ConfigureAwait(false);

                var keyBytes = Encoding.UTF8.GetBytes(buildKey);
                await WriteInt32Async(stream, hash, keyBytes.Length, cancellationToken).ConfigureAwait(false);
                await WriteAsync(stream, hash, keyBytes, cancellationToken).ConfigureAwait(false);

                await WriteInt64Async(stream, hash, bytes.Length, cancellationToken).ConfigureAwait(false);
                await WriteAsync(stream, hash, bytes, cancellationToken).ConfigureAwait(false);

                var digest = new byte[DigestSize];
                hash.GetHashAndReset(digest);
                await stream.WriteAsync(digest, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>按 BuildKey 加载构建产物；未命中抛 <see cref="InvalidOperationException"/></summary>
    /// <param name="buildKey">构建键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>派生字节（可经 <see cref="ReadOnlyMemory{T}.ToArray"/> 转为数组）</returns>
    /// <exception cref="InvalidOperationException">缓存未命中（文件缺失或完整性校验失败）</exception>
    public async Task<ReadOnlyMemory<byte>> LoadAsync(string buildKey, CancellationToken cancellationToken)
        => await TryLoadAsync(buildKey, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"构建产物缓存未命中：{buildKey}");

    /// <summary>按 BuildKey 尝试加载构建产物；缺失或损坏一律视为 miss 返回 null（不抛、不改动任何文件）</summary>
    /// <param name="buildKey">构建键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>派生字节；缓存 miss 返回 null</returns>
    /// <exception cref="ArgumentException">buildKey 为 null/空白时抛出</exception>
    public async Task<ReadOnlyMemory<byte>?> TryLoadAsync(string buildKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildKey);
        var path = Path.Combine(_cacheDirectory, buildKey + BinaryExtension);
        byte[] file;
        try
        {
            file = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            return ValidateAndSlice(file, buildKey);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>
    /// 校验文件头（magic/格式版本/BuildKey/长度）与整体 SHA-256 摘要后切出数据段；
    /// 任一项不符抛 <see cref="InvalidDataException"/>。
    /// </summary>
    private static ReadOnlyMemory<byte> ValidateAndSlice(byte[] file, string buildKey)
    {
        const int prefixSize = 4 + 4 + 4; // magic + version + keyLength
        if (file.Length < prefixSize + 8 + DigestSize)
            throw new InvalidDataException("构建产物文件过短");

        if (!file.AsSpan(0, 4).SequenceEqual(Magic))
            throw new InvalidDataException("构建产物文件头 magic 不符");
        if (BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(4, 4)) != FormatVersion)
            throw new InvalidDataException("构建产物格式版本不兼容");

        var storedKeyLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(8, 4));
        if (storedKeyLength < 0 || file.Length < prefixSize + storedKeyLength + 8 + DigestSize)
            throw new InvalidDataException("构建产物 BuildKey 长度非法");

        var keyBytes = Encoding.UTF8.GetBytes(buildKey);
        if (storedKeyLength != keyBytes.Length
            || !file.AsSpan(prefixSize, storedKeyLength).SequenceEqual(keyBytes))
        {
            throw new InvalidDataException("构建产物 BuildKey 与请求不一致");
        }

        var dataLengthOffset = prefixSize + storedKeyLength;
        var dataLength = BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(dataLengthOffset, 8));
        if (dataLength < 0 || file.Length != dataLengthOffset + 8 + dataLength + DigestSize)
            throw new InvalidDataException("构建产物数据长度与文件不符");

        var dataStart = (int)(dataLengthOffset + 8);
        var contentEnd = dataStart + (int)dataLength;
        Span<byte> digest = stackalloc byte[DigestSize];
        SHA256.HashData(file.AsSpan(0, contentEnd), digest);
        if (!CryptographicOperations.FixedTimeEquals(digest, file.AsSpan(contentEnd)))
            throw new InvalidDataException("构建产物 SHA-256 校验不符");

        return new ReadOnlyMemory<byte>(file, dataStart, (int)dataLength);
    }

    private static async ValueTask WriteInt32Async(
        FileStream stream, IncrementalHash hash, int value, CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        await WriteAsync(stream, hash, buffer, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask WriteInt64Async(
        FileStream stream, IncrementalHash hash, long value, CancellationToken cancellationToken)
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        await WriteAsync(stream, hash, buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>写入并同步累加进内容摘要</summary>
    private static async ValueTask WriteAsync(
        FileStream stream, IncrementalHash hash, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        hash.AppendData(bytes.Span);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}