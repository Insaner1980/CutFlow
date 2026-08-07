using Windows.System;

namespace CutFlow.Utilities;

public static class GlobalShortcutRouter
{
    public static bool ShouldCreateNewProject(
        bool alreadyHandled,
        bool editableControlFocused,
        bool controlDown,
        VirtualKey key) =>
        !alreadyHandled && !editableControlFocused && controlDown && key == VirtualKey.N;
}
