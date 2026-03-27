namespace MeshBoard.Contracts.CollectorEvents;

public enum CollectorDecryptStatus
{
    NotRequired = 0,
    Succeeded = 1,
    Failed = 2,
    SkippedNoKey = 3
}
