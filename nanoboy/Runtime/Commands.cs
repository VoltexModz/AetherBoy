using System;
using System.Threading.Tasks;
using nanoboy.Core;

namespace AetherBoy.Runtime
{
    internal sealed class SessionOwnerContext
    {
        public SessionOwnerContext(IEmulationMachine machine)
        {
            Machine = machine ?? throw new ArgumentNullException(nameof(machine));
        }

        public IEmulationMachine Machine { get; }
        public bool IsPaused { get; set; }
        public bool IsTurboEnabled { get; set; }
        public bool StopRequested { get; set; }
        public bool TimelineChanged { get; set; }
    }

    internal abstract class EmulationCommand
    {
        private readonly TaskCompletionSource completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => completion.Task;
        public abstract void Apply(SessionOwnerContext context);

        public virtual void Succeed() => completion.TrySetResult();
        public virtual void Fail(Exception exception) => completion.TrySetException(exception);
    }

    internal abstract class EmulationCommand<TResult> : EmulationCommand
    {
        private readonly TaskCompletionSource<TResult> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private TResult? result;

        public new Task<TResult> Completion => completion.Task;

        protected void SetResult(TResult value) => result = value;
        public override void Succeed() => completion.TrySetResult(result!);
        public override void Fail(Exception exception) => completion.TrySetException(exception);
    }

    internal sealed class SetButtonsCommand : EmulationCommand
    {
        private readonly GameBoyButtons pressedButtons;
        public SetButtonsCommand(GameBoyButtons pressedButtons) => this.pressedButtons = pressedButtons;
        public override void Apply(SessionOwnerContext context) => context.Machine.SetButtons(pressedButtons);
    }

    internal sealed class SetGameBoyAdvanceButtonsCommand : EmulationCommand
    {
        private readonly GameBoyAdvanceButtons pressedButtons;

        public SetGameBoyAdvanceButtonsCommand(GameBoyAdvanceButtons pressedButtons) =>
            this.pressedButtons = pressedButtons;

        public override void Apply(SessionOwnerContext context) =>
            context.Machine.SetGameBoyAdvanceButtons(pressedButtons);
    }

    internal sealed class ConfigureCommand : EmulationCommand
    {
        private readonly EmulatorConfiguration configuration;
        public ConfigureCommand(EmulatorConfiguration configuration) => this.configuration = configuration;
        public override void Apply(SessionOwnerContext context) => context.Machine.Configure(configuration);
    }

    internal sealed class SetPaletteCommand : EmulationCommand
    {
        private readonly int paletteIndex;
        public SetPaletteCommand(int paletteIndex) => this.paletteIndex = paletteIndex;
        public override void Apply(SessionOwnerContext context) => context.Machine.SetPalette(paletteIndex);
    }

    internal sealed class SetPausedCommand : EmulationCommand
    {
        private readonly bool isPaused;
        public SetPausedCommand(bool isPaused) => this.isPaused = isPaused;
        public override void Apply(SessionOwnerContext context) => context.IsPaused = isPaused;
    }

    internal sealed class SetTurboCommand : EmulationCommand
    {
        private readonly bool isEnabled;
        public SetTurboCommand(bool isEnabled) => this.isEnabled = isEnabled;
        public override void Apply(SessionOwnerContext context) => context.IsTurboEnabled = isEnabled;
    }

    internal sealed class ResetCommand : EmulationCommand
    {
        public override void Apply(SessionOwnerContext context)
        {
            context.Machine.Reset();
            context.TimelineChanged = true;
        }
    }

    internal sealed class CaptureStateCommand : EmulationCommand<byte[]>
    {
        public override void Apply(SessionOwnerContext context) =>
            SetResult(context.Machine.CaptureState());
    }

    internal sealed class RestoreStateCommand : EmulationCommand
    {
        private readonly byte[] state;

        public RestoreStateCommand(byte[] state)
        {
            this.state = state == null
                ? throw new ArgumentNullException(nameof(state))
                : (byte[])state.Clone();
        }

        public override void Apply(SessionOwnerContext context)
        {
            context.Machine.RestoreState(state);
            context.TimelineChanged = true;
        }
    }

    internal sealed class RewindCommand : EmulationCommand<bool>
    {
        public override void Apply(SessionOwnerContext context)
        {
            bool rewound = context.Machine.Rewind();
            if (rewound)
            {
                context.TimelineChanged = true;
            }

            SetResult(rewound);
        }
    }

    internal sealed class AddCheatCommand : EmulationCommand<CheatSnapshot>
    {
        private readonly string name;
        private readonly string code;

        public AddCheatCommand(string name, string code)
        {
            this.name = name;
            this.code = code;
        }

        public override void Apply(SessionOwnerContext context) =>
            SetResult(context.Machine.AddCheat(name, code));
    }

    internal sealed class RemoveCheatCommand : EmulationCommand<bool>
    {
        private readonly Guid id;
        public RemoveCheatCommand(Guid id) => this.id = id;
        public override void Apply(SessionOwnerContext context) => SetResult(context.Machine.RemoveCheat(id));
    }

    internal sealed class ToggleCheatCommand : EmulationCommand<bool>
    {
        private readonly Guid id;
        public ToggleCheatCommand(Guid id) => this.id = id;
        public override void Apply(SessionOwnerContext context) => SetResult(context.Machine.ToggleCheat(id));
    }

    internal sealed class ShutdownCommand : EmulationCommand
    {
        public override void Apply(SessionOwnerContext context) => context.StopRequested = true;
    }
}
