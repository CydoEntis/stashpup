namespace StashPup.Core.Interfaces;

public interface IResult
{
    bool Success { get; }
    string? ErrorMessage { get; }
}