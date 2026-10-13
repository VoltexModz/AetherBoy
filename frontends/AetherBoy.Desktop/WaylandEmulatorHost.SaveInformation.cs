namespace AetherBoy.Desktop;

internal enum LinuxSaveInformationState { NoGame, Loading, Empty, Saved, Unavailable }

internal static class LinuxSaveInformation
{
    internal static LinuxSaveInformationState Resolve(bool hasGame, bool loading, LinuxStateCard? card, string? inspectionError)
    {
        if (!hasGame) return LinuxSaveInformationState.NoGame;
        if (inspectionError is not null) return LinuxSaveInformationState.Unavailable;
        if (loading) return LinuxSaveInformationState.Loading;
        if (card is null || card.Error is not null) return LinuxSaveInformationState.Unavailable;
        return card.Exists ? LinuxSaveInformationState.Saved : LinuxSaveInformationState.Empty;
    }
}
