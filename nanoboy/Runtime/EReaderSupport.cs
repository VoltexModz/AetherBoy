using System;
using System.IO;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.Runtime;

public sealed record EReaderSnapshot(int QueuedCards, int CardsStarted, bool HasCard);

public static class EReaderInput
{
    public static byte[] ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > EReaderDotCode.MaximumFileLength || !EReaderDotCode.IsSupportedLength((int)stream.Length))
            throw new InvalidDataException("Unsupported e-Reader card size.");
        byte[] card = new byte[(int)stream.Length]; stream.ReadExactly(card);
        if (stream.ReadByte() != -1) throw new InvalidDataException("The e-Reader file changed during import.");
        return card;
    }
}

internal sealed class EReaderCardCommand : EmulationCommand
{
    private readonly byte[]? card;
    public EReaderCardCommand(byte[]? card)
    {
        if (card is not null && !EReaderDotCode.IsSupportedLength(card.Length))
            throw new InvalidDataException("Unsupported e-Reader card size.");
        this.card = card is null ? null : (byte[])card.Clone();
    }
    public override void Apply(SessionOwnerContext context)
    {
        if (context.Machine is not GbaProductionMachine machine)
            throw new NotSupportedException("e-Reader cards require an e-Reader cartridge in single-player mode.");
        if (card is null) machine.ClearEReaderCards(); else machine.QueueEReaderCard(card);
    }
}
