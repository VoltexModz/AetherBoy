using AetherBoy.Runtime.Netplay;

namespace AetherBoy.Desktop;

internal static class LinuxOnlineProbePresentation
{
    internal static (string Headline, string Detail) Describe(OnlineProbeSnapshot? state)
    {
        if (state is null) return (global::AetherBoy.Runtime.Localization.UiText.Get("Both PCs must select this test, not a game room."), global::AetherBoy.Runtime.Localization.UiText.Get("Create a test room or enter your friend's test code."));
        if (state.Stopping) return (global::AetherBoy.Runtime.Localization.UiText.Get("Closing the test connection…"), global::AetherBoy.Runtime.Localization.UiText.Get("Please wait before starting another test."));
        return state.Phase switch
        {
            OnlineProbePhase.Passed => state.Active
                ? (global::AetherBoy.Runtime.Localization.UiText.Get("This PC passed the connection test."), global::AetherBoy.Runtime.Localization.UiText.Get("Keep the test open until both PCs report success, then end it."))
                : (global::AetherBoy.Runtime.Localization.UiText.Get("This PC passed; the connection is closed."), global::AetherBoy.Runtime.Localization.UiText.Get("This does not confirm a game link or success on the other PC.")),
            OnlineProbePhase.Cancelled => (global::AetherBoy.Runtime.Localization.UiText.Get("Test cancelled."), global::AetherBoy.Runtime.Localization.UiText.Get("Create a new test room to try again.")),
            OnlineProbePhase.Testing => (global::AetherBoy.Runtime.Localization.UiText.Get("Data channel open. Checking data in both directions…"), global::AetherBoy.Runtime.Localization.UiText.Get("The test checks 32, 256, 1024 and 4096-byte messages.")),
            OnlineProbePhase.Failed => Failure(state.Failure),
            _ => state.Connection.Stage switch
            {
                "remote-answer" => (global::AetherBoy.Runtime.Localization.UiText.Get("Test room ready. Share its code with your friend."), global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for the second PC to join.")),
                "remote-offer" => (global::AetherBoy.Runtime.Localization.UiText.Get("Test room found."), global::AetherBoy.Runtime.Localization.UiText.Get("Waiting for the host's invitation.")),
                "relay-preparation" or "ice-gathering" => (global::AetherBoy.Runtime.Localization.UiText.Get("Room server confirmed the test room."), global::AetherBoy.Runtime.Localization.UiText.Get("Checking relay connection paths.")),
                "publish-description" => (global::AetherBoy.Runtime.Localization.UiText.Get("Exchanging connection details automatically…"), global::AetherBoy.Runtime.Localization.UiText.Get("Keep AetherBoy open on both PCs.")),
                "data-channel-open" => (global::AetherBoy.Runtime.Localization.UiText.Get("The other PC was found."), global::AetherBoy.Runtime.Localization.UiText.Get("Opening the encrypted data channel.")),
                _ => (global::AetherBoy.Runtime.Localization.UiText.Get("Contacting the room server…"), global::AetherBoy.Runtime.Localization.UiText.Get("Keep AetherBoy open on both PCs.")),
            },
        };
    }

    private static (string, string) Failure(OnlineProbeFailure failure) => failure switch
    {
        OnlineProbeFailure.AccessKey => (global::AetherBoy.Runtime.Localization.UiText.Get("The room server rejected the access key."), global::AetherBoy.Runtime.Localization.UiText.Get("Check the room server key on both PCs, not the TURN password.")),
        OnlineProbeFailure.RoomMissing => (global::AetherBoy.Runtime.Localization.UiText.Get("Test room not found or expired."), global::AetherBoy.Runtime.Localization.UiText.Get("Create a new test room and check the code.")),
        OnlineProbeFailure.RoomMismatch => (global::AetherBoy.Runtime.Localization.UiText.Get("Room occupied or using a different test profile."), global::AetherBoy.Runtime.Localization.UiText.Get("Both PCs must choose Test without a game.")),
        OnlineProbeFailure.ServerProfile => (global::AetherBoy.Runtime.Localization.UiText.Get("The room server rejected this test profile."), global::AetherBoy.Runtime.Localization.UiText.Get("Check whether it supports transport-probe-v1.")),
        OnlineProbeFailure.RateLimit => (global::AetherBoy.Runtime.Localization.UiText.Get("Too many connection attempts."), global::AetherBoy.Runtime.Localization.UiText.Get("Wait a minute, then create a new test room.")),
        OnlineProbeFailure.Timeout => (global::AetherBoy.Runtime.Localization.UiText.Get("The connection test timed out."), global::AetherBoy.Runtime.Localization.UiText.Get("Check the last confirmed stage and report; the cause is not yet known.")),
        OnlineProbeFailure.Integrity => (global::AetherBoy.Runtime.Localization.UiText.Get("The data check failed."), global::AetherBoy.Runtime.Localization.UiText.Get("Compare both builds and test settings, then review the report.")),
        _ => (global::AetherBoy.Runtime.Localization.UiText.Get("The connection test failed."), global::AetherBoy.Runtime.Localization.UiText.Get("Check the report; server, native library or relay may be involved.")),
    };
}
