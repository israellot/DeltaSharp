namespace DeltaSharp.Format;

public readonly ref struct DeltaCommand
{
    public readonly DeltaCommandEnum Command { get; }

    public readonly ulong Position { get; }

    public readonly ulong Length { get; }

    public DeltaCommand(DeltaCommandEnum command, ulong position, ulong length)
    {
        Command = command;
        Position = position;
        Length = length;
    }

}

public enum DeltaCommandEnum
{
    Length,
    Insert,
    Copy,
    Checksum
}
