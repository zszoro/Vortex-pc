using System.Diagnostics;
using System.Runtime.InteropServices;
using VORTEX.Core;

namespace VORTEX.Services;

public sealed class GuiAutomationService : IGuiAutomationService
{
    private const int SwRestore = 9;
    private const ushort VkControl = 0x11;
    private const ushort VkK = 0x4B;
    private const ushort VkReturn = 0x0D;
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFUnicode = 0x0004;

    public async Task PrepareDiscordMessageAsync(
        string recipient,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Controle de GUI está disponível somente no Windows.");
        if (string.IsNullOrWhiteSpace(recipient))
            throw new InvalidOperationException("Não consegui identificar o destinatário da mensagem.");
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("Não consegui identificar o texto da mensagem.");

        var handle = await WaitForDiscordWindowAsync(cancellationToken);
        ShowWindow(handle, SwRestore);
        SetForegroundWindow(handle);
        await Task.Delay(700, cancellationToken);

        SendChord(VkControl, VkK);
        await Task.Delay(250, cancellationToken);
        SendText(recipient);
        await Task.Delay(900, cancellationToken);
        SendKey(VkReturn);
        await Task.Delay(1000, cancellationToken);
        SendText(message);
    }

    public async Task ConfirmDiscordSendAsync(CancellationToken cancellationToken = default)
    {
        var handle = await WaitForDiscordWindowAsync(cancellationToken);
        ShowWindow(handle, SwRestore);
        SetForegroundWindow(handle);
        await Task.Delay(250, cancellationToken);
        SendKey(VkReturn);
    }

    private static async Task<IntPtr> WaitForDiscordWindowAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var process = Process.GetProcessesByName("Discord")
                .FirstOrDefault(candidate => candidate.MainWindowHandle != IntPtr.Zero);
            if (process != null) return process.MainWindowHandle;
            await Task.Delay(500, cancellationToken);
        }

        throw new InvalidOperationException("Discord abriu, mas nenhuma janela controlável foi encontrada.");
    }

    private static void SendChord(ushort modifier, ushort key)
    {
        SendKeyDown(modifier);
        SendKeyDown(key);
        SendKeyUp(key);
        SendKeyUp(modifier);
    }

    private static void SendKey(ushort key)
    {
        SendKeyDown(key);
        SendKeyUp(key);
    }

    private static void SendKeyDown(ushort key) => SendKeyboardInput(key, 0);
    private static void SendKeyUp(ushort key) => SendKeyboardInput(key, KeyEventFKeyUp);

    private static void SendKeyboardInput(ushort key, uint flags)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = key,
                    Scan = '\0',
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };
        SendInputs([input]);
    }

    private static void SendText(string text)
    {
        var inputs = new List<Input>(text.Length * 2);
        foreach (var character in text)
        {
            inputs.Add(UnicodeInput(character, false));
            inputs.Add(UnicodeInput(character, true));
        }
        SendInputs(inputs.ToArray());
    }

    private static Input UnicodeInput(char character, bool keyUp) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = 0,
                    Scan = character,
                    Flags = KeyEventFUnicode | (keyUp ? KeyEventFKeyUp : 0),
                    Time = 0,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };

    private static void SendInputs(Input[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
            throw new InvalidOperationException("O Windows recusou parte da automação de teclado.");
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public char Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
