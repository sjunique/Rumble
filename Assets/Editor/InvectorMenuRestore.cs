using UnityEditor;
using Invector.vCharacterController;

public static class InvectorMenuRestore
{
    [MenuItem("Tools/Invector/Open Welcome Window")]
    public static void OpenWelcomeWindow()
    {
        vInvectorWelcomeWindow.Open();
    }
}