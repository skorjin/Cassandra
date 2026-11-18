namespace Cassandra.Domain.Models;

/// <summary>The selected translation model is missing; the player has to download one.</summary>
public sealed class ModelNotInstalledException : InvalidOperationException
{
    public ModelNotInstalledException()
        : base("Модель перевода не установлена. Откройте настройки и скачайте модель.")
    {
    }
}

/// <summary>A downloaded file failed its checksum; the partial data was discarded.</summary>
public sealed class ModelCorruptException(string file)
    : IOException($"Файл «{file}» повреждён при загрузке (не совпала контрольная сумма).");

/// <summary>Not enough free space for the download. Retrying cannot help.</summary>
public sealed class InsufficientDiskSpaceException(long neededBytes)
    : IOException($"Недостаточно места на диске: нужно ещё {neededBytes / 1048576} МБ.");
