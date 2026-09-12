namespace GameboyAdvanced.Core.Serial;

/// <summary>
/// An emulation-thread-owned serial endpoint. Implementations may translate a
/// known accessory or game protocol, but network callbacks must never call this
/// contract or mutate the controller. The device scheduler owns completions.
/// </summary>
public interface ISerialPeer
{
    int PlayerId(SerialController controller);
    bool NormalInputHigh(SerialController controller);
    void RefreshStatus();
    void ClockNormal(SerialController controller);
    void BeginMultiplayer(SerialController controller);
    void CompleteMultiplayer(SerialController controller);
    void Cancel(SerialController controller);
}
